"""Posten eines Monatsplans.

Eigener Router, damit sich `/positions/{id}` und `/plans/{id}` nicht in die
Quere kommen — sonst versucht FastAPI, „positions" als Jahreszahl zu lesen.

Im gemeinsamen Haushalt dürfen beide Mitglieder Posten ändern, auch die des
anderen. Jede Änderung wird protokolliert.
"""

import uuid
from datetime import UTC, date, datetime

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.auth import current_active_user
from app.core.permissions import (
    can_assign_to_household,
    granted_level,
    owns_plan,
    require,
)
from app.db.session import get_session
from app.models.account import Account
from app.models.enums import AccessLevel
from app.models.plan import Plan, PlanPosition, PlanPositionChange
from app.models.transaction import Transaction
from app.models.user import User
from app.schemas.plan import PositionPaid, PositionRead, PositionUpdate

router = APIRouter()


async def _load(
    session: AsyncSession,
    position_id: uuid.UUID,
    user: User,
    *,
    allow_delegate: bool = True,
) -> tuple[PlanPosition, Plan]:
    position = await session.get(PlanPosition, position_id)
    if position is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "position_not_found"})

    plan = await session.get(Plan, position.plan_id)
    if plan is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "plan_not_found"})

    # Your own position is always allowed.
    if owns_plan(user, plan):
        return position, plan

    # Somebody else position only at level `edit`, and only the owner can grant
    # that. Without it everyone carries their own part and books on their own
    # positions.
    #
    # It stays traceable through `plan_position_changes`, which records who changed
    # which field when. That is why this needs a log rather than a lock: locking
    # would get in the way far more often than it helps.
    #
    # `allow_delegate=False` on delete: changing is reversible and recorded,
    # deleting is neither.
    require(allow_delegate, "not_allowed")
    level = await granted_level(session, plan.user_id, user.id)
    require(level is AccessLevel.EDIT, "no_edit_granted")
    return position, plan


async def _check_accounts(
    session: AsyncSession, plan: Plan, changes: dict
) -> None:
    """Accounts on a position have to belong to the **plan owner**.

    Without this check a position could be pointed at somebody else account — as a
    delegate even at your own, and the other person booking would then run through
    your balance.

    Source and target must not be the same account: the booking would have no
    effect and the database rejects it through a CHECK anyway.
    """
    for field in ("account_id", "counter_account_id"):
        account_id = changes.get(field)
        if account_id is None:
            continue
        account = await session.get(Account, account_id)
        require(
            account is not None and account.owner_id == plan.user_id,
            "not_account_owner",
        )

    source = changes.get("account_id", ...)
    target = changes.get("counter_account_id", ...)
    if source is ...:
        source = None
    if target is ...:
        target = None
    # Only check when both appear in this change or already sit on the position —
    # otherwise a partial update would be blocked for no reason.
    if source is not None and target is not None:
        require(source != target, "transfer_needs_two_accounts")


@router.patch("/{position_id}", response_model=PositionRead)
async def update_position(
    position_id: uuid.UUID,
    payload: PositionUpdate,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> PlanPosition:
    """Change a position — every change is written to the log."""
    position, plan = await _load(session, position_id, user)

    changes = payload.model_dump(exclude_unset=True)
    merged = {
        "account_id": changes.get("account_id", position.account_id),
        "counter_account_id": changes.get(
            "counter_account_id", position.counter_account_id
        ),
    }
    await _check_accounts(session, plan, merged)
    if "household_id" in changes:
        require(
            await can_assign_to_household(session, plan.user_id, changes["household_id"]),
            "not_household_member",
        )

    touched = False
    for field, value in changes.items():
        old = getattr(position, field)
        if old == value:
            continue
        session.add(
            PlanPositionChange(
                position_id=position.id,
                changed_by_id=user.id,
                field=field,
                old_value=None if old is None else str(old),
                new_value=None if value is None else str(value),
            )
        )
        setattr(position, field, value)
        touched = True

    # Manual corrections must not be overwritten the next time the month is
    # generated — the position remembers that itself.
    if touched and position.commitment_id is not None:
        position.manually_changed = True

    await session.commit()
    await session.refresh(position)
    return position


@router.delete("/{position_id}", status_code=status.HTTP_204_NO_CONTENT)
async def delete_position(
    position_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> None:
    position, _ = await _load(session, position_id, user, allow_delegate=False)
    await session.delete(position)
    await session.commit()


@router.post("/{position_id}/paid", response_model=PositionRead)
async def mark_paid(
    position_id: uuid.UUID,
    payload: PositionPaid | None = None,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> PlanPosition:
    """Tick a position off — and book it into the household book.

    Separate from the amount: "ticked off" and "amount recorded" are two different
    things. A payment can be done and match the planned amount exactly.

    **A booking is only created if the position has none yet.** A rent position has
    none, so ticking it off books the full amount. A grocery budget already collected
    purchases over the month — there the tick only means "month done", and the book
    stays the truth.

    Without that rule, purchases plus tick would count twice.

    `payload` allows a different date and a different amount while ticking off. That
    is needed constantly: the payment was a few days ago, or the instalment came out
    differently than planned. Without those two fields one would have to go into the
    book afterwards and correct the booking that was just created.
    """
    position, plan = await _load(session, position_id, user)
    position.paid_at = datetime.now(UTC)
    payload = payload or PositionPaid()

    already = await session.scalar(
        select(func.count())
        .select_from(Transaction)
        .where(Transaction.position_id == position.id)
    )

    if not already:
        # Where the money comes from: the position first, then the default account.
        # If there is none, nothing is booked — ticking off must not fail over it.
        #
        # Always **the owner account**, not the one of whoever ticks. A delegate
        # ticking off somebody else rent must not create a booking in their own book
        # for a payment they never made.
        account_id = position.account_id
        if account_id is None:
            account_id = await session.scalar(
                select(Account.id).where(Account.owner_id == plan.user_id, Account.is_default)
            )

        # A counter account means **transfer** rather than expense. On a savings
        # goal the money moves to another own account; booked as an expense the
        # total would be wrong, because nothing left the household.
        #
        # If target and source coincide — the position has no account and the
        # default account is exactly the target — nothing is booked. A booking from
        # an account to itself would be wrong, and the tick must not fail over it.
        same_account = (
            position.counter_account_id is not None
            and position.counter_account_id == account_id
        )

        if account_id is not None and not same_account:
            session.add(
                Transaction(
                    owner_id=plan.user_id,
                    account_id=account_id,
                    counter_account_id=position.counter_account_id,
                    occurred_on=payload.occurred_on or date.today(),
                    amount=payload.amount or position.amount_planned,
                    note=position.label,
                    category=position.category,
                    block=position.block,
                    position_id=position.id,
                    auto_booked=True,
                )
            )
            await session.flush()
            position.amount_actual = payload.amount or position.amount_planned

    await session.commit()
    await session.refresh(position)
    return position


@router.delete("/{position_id}/paid", response_model=PositionRead)
async def unmark_paid(
    position_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> PlanPosition:
    """Remove the tick — and the booking it created along with it.

    Only the **self-created** booking, recognisable by `auto_booked`. Purchases
    entered by hand on the same position stay; the tick never created them, so it
    does not take them away either.

    The frontend asks first and names the amount, so that a manually corrected
    figure does not disappear unnoticed.
    """
    position, _ = await _load(session, position_id, user)
    position.paid_at = None

    booked = await session.execute(
        select(Transaction).where(
            Transaction.position_id == position.id,
            Transaction.auto_booked,
        )
    )
    for transaction in booked.scalars():
        await session.delete(transaction)

    await session.flush()

    total = await session.scalar(
        select(func.sum(Transaction.amount)).where(Transaction.position_id == position.id)
    )
    position.amount_actual = total

    await session.commit()
    await session.refresh(position)
    return position
