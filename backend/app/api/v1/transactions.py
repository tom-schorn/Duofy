"""The household book — what actually happened.

The plan says how the month was meant to go. The book says how it went. The two
touch at exactly one point: a booking **can** be assigned to a position, but it
does not have to be. An unplanned purchase belongs in the book all the same.

Bookings are **private** by default, even inside a shared household. The shared
plan shows a fill level like `127.50 of 600` — not which shops the money went to.
"""

import uuid
from decimal import Decimal

from fastapi import APIRouter, Depends, HTTPException, Query, status
from sqlalchemy import and_, extract, func, or_, select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.auth import current_active_user
from app.core.permissions import (
    Area,
    granted_level,
    is_member,
    require,
    viewable_members,
)
from app.db.session import get_session
from app.models.account import Account
from app.models.enums import AccessLevel
from app.models.plan import Plan, PlanPosition
from app.models.transaction import Transaction
from app.models.user import User
from app.schemas.transaction import (
    TransactionCreate,
    TransactionRead,
    TransactionUpdate,
)

router = APIRouter()

ZERO = Decimal("0.00")


async def _may_book_for(
    session: AsyncSession,
    booking_owner: uuid.UUID,
    user: User,
    *,
    needs: AccessLevel = AccessLevel.EDIT,
) -> None:
    """May `user` act on bookings of `booking_owner`?

    Your own always. Somebody else from the level the owner granted. This puts
    bookings under the same rule as positions — the two used to disagree: a
    delegate could **tick off** a position, which creates a booking on the other
    account, but could not book directly.

    `needs` separates changing from deleting: a wrong booking can be corrected,
    a deleted one leaves a gap in a balance that nothing explains.
    """
    if booking_owner == user.id:
        return
    level = await granted_level(session, booking_owner, user.id, Area.ACCOUNTS)
    require(
        level.rank >= needs.rank,
        "no_delete_granted" if needs is AccessLevel.DELETE else "no_edit_granted",
    )


async def _account_owner(
    session: AsyncSession, account_id: uuid.UUID, user: User
) -> uuid.UUID:
    """Who owns the account — and whether `user` may book on it."""
    account = await session.get(Account, account_id)
    if account is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "account_not_found"})
    await _may_book_for(session, account.owner_id, user)
    return account.owner_id


async def _position_owner(
    session: AsyncSession, position_id: uuid.UUID, user: User
) -> uuid.UUID:
    """A position belongs to the owner of its plan, shared ones included.

    Without this check, bookings could be attached to other people positions and
    change their actual amounts.
    """
    position = await session.get(PlanPosition, position_id)
    if position is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "position_not_found"})

    plan = await session.get(Plan, position.plan_id)
    if plan is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "plan_not_found"})
    await _may_book_for(session, plan.user_id, user)
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
    transaction = await session.get(Transaction, transaction_id)
    if transaction is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "transaction_not_found"})
    await _may_book_for(session, transaction.owner_id, user, needs=needs)
    return transaction


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

    Welcher Monat, entscheidet der **Posten** — nicht das Datum:

    * mit Posten  → der Monat des Plans, zu dem der Posten gehört
    * ohne Posten → der Monat, in dem das Geld floss

    Wohngeld für August wird am 31. Juli überwiesen, ALG1 ebenso. Sie gehören
    in den August und tauchen dort auf, mit ihrem echten Juli-Datum. Genau das
    machen die meisten Haushaltsbücher falsch: sie legen eine Buchung nach
    ihrem Datum ab, und damit ist Wohngeld für immer ein Juli-Vorgang.

    Die Regel schließt aus, statt zu ergänzen — eine zugeordnete Buchung steht
    in **einem** Monat, nicht in zweien. Sonst zählte sie doppelt, sobald man
    Summen über das Buch bildet.
    """
    # Whose book: your own, one person, or the household. Bookings are private —
    # they only become visible once the owner granted at least level `view`. The
    # owner decides, not the reader.
    if household is not None:
        require(await is_member(session, user.id, household), "not_household_member")
        owner_ids = await viewable_members(session, household, user.id, Area.ACCOUNTS)
    elif owner is not None and owner != user.id:
        level = await granted_level(session, owner, user.id, Area.ACCOUNTS)
        require(level.rank >= AccessLevel.VIEW.rank, "no_insight_granted")
        owner_ids = [owner]
    else:
        owner_ids = [user.id]

    shared = household is not None
    query = select(Transaction, User.first_name).join(
        User, User.id == Transaction.owner_id
    ).where(Transaction.owner_id.in_(owner_ids))

    if year is not None and month is not None:
        in_month = and_(
            extract("year", Transaction.occurred_on) == year,
            extract("month", Transaction.occurred_on) == month,
        )
        belongs_to_plan = Transaction.position_id.in_(
            select(PlanPosition.id)
            .join(Plan, Plan.id == PlanPosition.plan_id)
            .where(Plan.user_id.in_(owner_ids), Plan.year == year, Plan.month == month)
        )
        # Without a position the date decides, with one the plan does. Never both.
        query = query.where(
            or_(and_(Transaction.position_id.is_(None), in_month), belongs_to_plan)
        )
    elif year is not None:
        query = query.where(extract("year", Transaction.occurred_on) == year)
    elif month is not None:
        query = query.where(extract("month", Transaction.occurred_on) == month)

    result = await session.execute(
        query.order_by(Transaction.occurred_on.desc(), Transaction.created_at.desc())
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
    booking_owner = await _account_owner(session, payload.account_id, user)
    if payload.counter_account_id is not None:
        target_owner = await _account_owner(session, payload.counter_account_id, user)
        require(target_owner == booking_owner, "transfer_needs_one_owner")
    if payload.position_id is not None:
        position_owner = await _position_owner(session, payload.position_id, user)
        require(position_owner == booking_owner, "position_needs_same_owner")

    transaction = Transaction(owner_id=booking_owner, **payload.model_dump())
    session.add(transaction)
    await session.flush()

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

    for field, value in changes.items():
        setattr(transaction, field, value)

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
