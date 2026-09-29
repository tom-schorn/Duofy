"""The household book — what actually happened.

The plan says how the month was meant to go. The book says how it went. The two
touch at exactly one point: a booking **can** be assigned to a position, but it
does not have to be. An unplanned purchase belongs in the book all the same.

Bookings are **private** by default, even inside a shared household. The shared
plan shows a fill level like `127.50 of 600` — not which shops the money went to.
"""

import uuid
from datetime import date
from decimal import Decimal

from fastapi import APIRouter, Depends, HTTPException, Query, status
from sqlalchemy import func, select
from sqlalchemy.exc import IntegrityError
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.auth import current_active_user
from app.core.permissions import Area, load_owned, load_position, require, require_level
from app.core.scope import HouseholdMembers, Lens, resolve_scope
from app.db.session import get_session
from app.models.account import Account
from app.models.enums import AccessLevel, TransactionKind
from app.models.plan import PlanPosition
from app.models.transaction import Transaction
from app.models.user import User
from app.schemas.transaction import (
    TransactionCreate,
    TransactionRead,
    TransactionUpdate,
)
from app.services import plan_month

router = APIRouter()

ZERO = Decimal("0.00")

#: Cannot be cleared on a change.
NOT_NULLABLE = ("account_id", "occurred_on", "amount")


async def _require_free_month(
    session: AsyncSession,
    account_id: uuid.UUID,
    occurred_on: date,
    *,
    ignore: uuid.UUID | None = None,
) -> None:
    """One carry-over per account and month — the database says so too (unique
    index), this gives the frontend a code instead of a server error."""
    query = select(Transaction.id).where(
        Transaction.kind == TransactionKind.CARRY_OVER,
        Transaction.account_id == account_id,
        Transaction.occurred_on == occurred_on,
    )
    if ignore is not None:
        query = query.where(Transaction.id != ignore)
    if await session.scalar(query) is not None:
        raise HTTPException(status.HTTP_409_CONFLICT, detail={"code": "carry_over_exists"})


async def _account_owner(
    session: AsyncSession,
    account_id: uuid.UUID,
    user: User,
    needs: AccessLevel = AccessLevel.EDIT,
    area: Area = Area.BOOK,
) -> uuid.UUID:
    """Who owns the account — and whether `user` may book on it.

    Booking is a question of the owner's book, not of their accounts: the accounts
    grant is about the accounts themselves. Seeing the balances without the book
    is only a hint in the grants page, never a refusal here (decision 62). A
    carry-over is the exception: it sets where the account starts, so it asks the
    accounts grant (decision 69).
    """
    account = await load_owned(
        session, Account, account_id, user, area, needs, not_found="account_not_found"
    )
    return account.owner_id


async def _position_owner(
    session: AsyncSession,
    position_id: uuid.UUID,
    user: User,
    needs: AccessLevel = AccessLevel.EDIT,
) -> uuid.UUID:
    """A position belongs to the owner of its plan, shared ones included.

    Booking onto it is a question of the book, not the plan: bookings are under
    the same rule as ticking a position off, which creates a booking in the owner's
    book. Without this check, bookings could be attached to other people positions
    and change their actual amounts.
    """
    _, plan = await load_position(session, position_id, user, Area.BOOK, needs)
    return plan.user_id


async def _recalc_position(session: AsyncSession, position_id: uuid.UUID | None) -> None:
    """Keep `amount_actual` up to date as the sum of the assigned bookings.

    Written along rather than computed on every read: `_summarize`, the plan
    overview and the frontend all read the column already. A subquery in every one
    of those places would cost more and reach further than one line here.
    """
    if position_id is None:
        return

    position = await session.get(PlanPosition, position_id)
    if position is None:
        return

    total = await session.scalar(
        select(func.sum(Transaction.amount)).where(Transaction.position_id == position_id)
    )
    # No bookings left: back to NULL, not to 0. "nothing recorded" and "zero spent"
    # are different statements.
    position.amount_actual = total if total is not None else None


async def _load(
    session: AsyncSession,
    transaction_id: uuid.UUID,
    user: User,
    *,
    needs: AccessLevel = AccessLevel.EDIT,
) -> Transaction:
    """A booking the user may act on: their own always, somebody else's from the
    level the owner granted in `Area.BOOK` — or in `Area.ACCOUNTS` for a
    carry-over, which belongs to the account rather than the book (decision 69).

    `needs` separates changing from deleting: a wrong booking can be corrected,
    a deleted one leaves a gap in a balance that nothing explains.
    """
    transaction = await session.get(Transaction, transaction_id)
    if transaction is None:
        raise HTTPException(
            status.HTTP_404_NOT_FOUND, detail={"code": "transaction_not_found"}
        )
    area = Area.ACCOUNTS if transaction.kind is TransactionKind.CARRY_OVER else Area.BOOK
    await require_level(session, transaction.owner_id, user, area, needs)
    return transaction


async def _check_carry_over_change(
    session: AsyncSession, transaction: Transaction, changes: dict, user: User
) -> None:
    """A carry-over may change its amount, its month or its account — nothing else."""
    if any(changes.get(field) is not None for field in (
        "counter_account_id", "category", "budget", "position_id"
    )):
        raise HTTPException(
            status.HTTP_422_UNPROCESSABLE_CONTENT, detail={"code": "carry_over_is_bare"}
        )
    day = changes.get("occurred_on", transaction.occurred_on)
    if day.day != 1:
        raise HTTPException(
            status.HTTP_422_UNPROCESSABLE_CONTENT,
            detail={"code": "carry_over_needs_first_of_month"},
        )
    if changes.get("account_id") is not None:
        # Ownership first: otherwise "taken" (409) against "not yours" (403) would
        # tell a stranger which months another person's account has a carry-over for.
        new_owner = await _account_owner(
            session, changes["account_id"], user, area=Area.ACCOUNTS
        )
        require(new_owner == transaction.owner_id, "not_account_owner")
    await _require_free_month(
        session,
        changes.get("account_id", transaction.account_id),
        day,
        ignore=transaction.id,
    )


@router.get("", response_model=list[TransactionRead])
async def list_transactions(
    year: int | None = Query(default=None, ge=2000, le=2100),
    month: int | None = Query(default=None, ge=1, le=12),
    owner: uuid.UUID | None = Query(default=None),
    household: uuid.UUID | None = Query(default=None),
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> list[TransactionRead]:
    """Buchungen, neueste zuerst. Ohne Zeitraum alle.

    Welcher Monat, entscheidet der **Plan-Monat** der Buchung (`plan_year`,
    `plan_month`) — nicht das Datum. Mit Posten ist das der Monat des Posten-Plans,
    ohne Posten der gewählte (Vormonat, Monat des Datums, Folgemonat; siehe
    `services/plan_month.py`).

    Wohngeld für August wird am 31. Juli überwiesen, ALG1 ebenso. Sie gehören
    in den August und tauchen dort auf, mit ihrem echten Juli-Datum. Genau das
    machen die meisten Haushaltsbücher falsch: sie legen eine Buchung nach
    ihrem Datum ab, und damit ist Wohngeld für immer ein Juli-Vorgang.

    Eine Buchung steht in **einem** Monat, nicht in zweien. Sonst zählte sie
    doppelt, sobald man Summen über das Buch bildet. Der Monat muss keinen Plan
    haben: die Buchung erscheint dort, sobald er angelegt ist — und bis dahin im
    Buch.
    """
    # Whose book: your own, one person, or the household. Bookings are private —
    # they only become visible once the owner granted at least level `view`. The
    # owner decides, not the reader.
    # The household book shows every member's bookings except those on a private
    # position, whatever the book grant says: the same rule as the household
    # plan, so book and plan add up to the same thing (decision 48, #242).
    scope = await resolve_scope(
        session,
        user,
        Area.BOOK,
        owner=owner,
        household=household,
        members=HouseholdMembers.ALL,
    )
    owner_ids = scope.owner_ids

    shared = scope.lens is Lens.HOUSEHOLD
    query = select(Transaction, User.first_name).join(
        User, User.id == Transaction.owner_id
    ).where(Transaction.owner_id.in_(owner_ids))

    if shared:
        private = select(PlanPosition.id).where(PlanPosition.is_private.is_(True))
        query = query.where(
            Transaction.position_id.is_(None) | Transaction.position_id.not_in(private)
        )
        # The household book shows spending, not account mechanics: another member's
        # carry-over and pure transfers (no position) stay with them. One's own
        # remain visible to oneself.
        query = query.where(
            (Transaction.owner_id == user.id)
            | (
                (Transaction.kind != TransactionKind.CARRY_OVER)
                & (
                    Transaction.counter_account_id.is_(None)
                    | Transaction.position_id.is_not(None)
                )
            )
        )

    if year is not None:
        query = query.where(Transaction.plan_year == year)
    if month is not None:
        query = query.where(Transaction.plan_month == month)

    result = await session.execute(
        query.order_by(
            Transaction.occurred_on.desc(),
            Transaction.created_at.desc(),
            # Settles the rest, and it has to: an import writes every booking of
            # a batch in one transaction, so `created_at` is identical across
            # them. Without this the order is undefined and an edited booking
            # comes back somewhere else in the list.
            Transaction.id,
        )
    )
    return [
        TransactionRead.model_validate(transaction).model_copy(
            # Only in the household view: there the bookings of several people sit
            # under each other and the name is what tells them apart.
            update={"owner_name": name if shared else None}
        )
        for transaction, name in result.all()
    ]


@router.post("", response_model=TransactionRead, status_code=status.HTTP_201_CREATED)
async def create_transaction(
    payload: TransactionCreate,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> Transaction:
    # A booking belongs to the account owner, not to whoever types it in. Ticking
    # off somebody else position puts the booking in **their** book; booking
    # directly has to behave the same, otherwise their payment would show up in the
    # delegate book.
    # A carry-over sets where somebody's account starts: that is their accounts,
    # at edit, not a booking in their book (decision 69).
    carry_over = payload.kind is TransactionKind.CARRY_OVER
    booking_owner = await _account_owner(
        session,
        payload.account_id,
        user,
        AccessLevel.EDIT if carry_over else AccessLevel.CREATE,
        area=Area.ACCOUNTS if carry_over else Area.BOOK,
    )
    if payload.counter_account_id is not None:
        target_owner = await _account_owner(
            session, payload.counter_account_id, user, AccessLevel.CREATE
        )
        require(target_owner == booking_owner, "transfer_needs_one_owner")
    if payload.position_id is not None:
        position_owner = await _position_owner(
            session, payload.position_id, user, AccessLevel.CREATE
        )
        require(position_owner == booking_owner, "position_needs_same_owner")

    if carry_over:
        await _require_free_month(session, payload.account_id, payload.occurred_on)

    chosen = (
        (payload.plan_year, payload.plan_month) if payload.plan_year is not None else None
    )
    year, month = await plan_month.resolve(
        session,
        kind=payload.kind,
        occurred_on=payload.occurred_on,
        position_id=payload.position_id,
        is_transfer=payload.counter_account_id is not None,
        chosen=chosen,
    )
    transaction = Transaction(
        owner_id=booking_owner,
        plan_year=year,
        plan_month=month,
        **payload.model_dump(exclude={"plan_year", "plan_month"}),
    )
    session.add(transaction)
    try:
        await session.flush()
    except IntegrityError:
        # Two carry-overs for one account and month created at the same time: the
        # unique index decides, the check above only spares the usual case.
        await session.rollback()
        if payload.kind is not TransactionKind.CARRY_OVER:
            raise
        raise HTTPException(
            status.HTTP_409_CONFLICT, detail={"code": "carry_over_exists"}
        ) from None

    await _recalc_position(session, transaction.position_id)
    await session.commit()
    await session.refresh(transaction)
    return transaction


@router.patch("/{transaction_id}", response_model=TransactionRead)
async def update_transaction(
    transaction_id: uuid.UUID,
    payload: TransactionUpdate,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> Transaction:
    transaction = await _load(session, transaction_id, user)
    changes = payload.model_dump(exclude_unset=True)

    # An explicit null on these would reach the database as a server error.
    for field in NOT_NULLABLE:
        if field in changes and changes[field] is None:
            raise HTTPException(
                status.HTTP_422_UNPROCESSABLE_CONTENT, detail={"code": "null_not_allowed"}
            )
    # A booking made by ticking a position off belongs to that position: taking it
    # away would leave the position ticked with nothing behind it.
    if (
        transaction.auto_booked
        and "position_id" in changes
        and changes["position_id"] != transaction.position_id
    ):
        raise HTTPException(
            status.HTTP_409_CONFLICT, detail={"code": "auto_booking_keeps_position"}
        )
    if transaction.kind is TransactionKind.CARRY_OVER:
        await _check_carry_over_change(session, transaction, changes, user)
    elif changes.get("amount") is not None and changes["amount"] <= 0:
        raise HTTPException(
            status.HTTP_422_UNPROCESSABLE_CONTENT, detail={"code": "amount_must_be_positive"}
        )
    # The same shape rules as on create, against what the booking will be after the
    # change — a single field can break the pair it belongs to.
    merged = {
        field: changes[field] if field in changes else getattr(transaction, field)
        for field in ("account_id", "counter_account_id", "category", "budget")
    }
    if transaction.kind is TransactionKind.CARRY_OVER:
        pass
    elif merged["counter_account_id"] is None:
        if merged["category"] is None or merged["budget"] is None:
            raise HTTPException(
                status.HTTP_422_UNPROCESSABLE_CONTENT, detail={"code": "purpose_required"}
            )
    elif merged["counter_account_id"] == merged["account_id"]:
        raise HTTPException(
            status.HTTP_422_UNPROCESSABLE_CONTENT,
            detail={"code": "transfer_needs_two_accounts"},
        )

    for field in ("account_id", "counter_account_id"):
        if changes.get(field) is not None:
            booking_owner = await _account_owner(session, changes[field], user)
            require(booking_owner == transaction.owner_id, "not_account_owner")
    if changes.get("position_id") is not None:
        position_owner = await _position_owner(session, changes["position_id"], user)
        require(position_owner == transaction.owner_id, "position_needs_same_owner")

    # If the booking moves to a different position, **both** have to be recomputed
    # — the old one loses it, the new one gains it.
    previous_position = transaction.position_id

    # The plan month follows what the booking becomes: its position, or the choice
    # made now, or — for a change of date alone — the choice it already had.
    chosen = None
    if changes.get("plan_year") is not None:
        chosen = (changes["plan_year"], changes["plan_month"])
    new_day = changes.get("occurred_on", transaction.occurred_on)
    new_position = changes["position_id"] if "position_id" in changes else transaction.position_id
    new_counter = (
        changes["counter_account_id"]
        if "counter_account_id" in changes
        else transaction.counter_account_id
    )
    year, month = await plan_month.resolve(
        session,
        kind=transaction.kind,
        occurred_on=new_day,
        position_id=new_position,
        is_transfer=new_counter is not None,
        chosen=chosen,
        fallback_offset=plan_month.offset_from(
            transaction.occurred_on, (transaction.plan_year, transaction.plan_month)
        ),
    )
    changes = {k: v for k, v in changes.items() if k not in ("plan_year", "plan_month")}

    for field, value in changes.items():
        setattr(transaction, field, value)
    transaction.plan_year, transaction.plan_month = year, month

    await session.flush()
    await _recalc_position(session, previous_position)
    if transaction.position_id != previous_position:
        await _recalc_position(session, transaction.position_id)

    await session.commit()
    await session.refresh(transaction)
    return transaction


@router.delete("/{transaction_id}", status_code=status.HTTP_204_NO_CONTENT)
async def delete_transaction(
    transaction_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> None:
    transaction = await _load(session, transaction_id, user, needs=AccessLevel.DELETE)
    position_id = transaction.position_id

    await session.delete(transaction)
    await session.flush()
    await _recalc_position(session, position_id)
    await session.commit()
