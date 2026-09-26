"""Contracts, limits, savings goals and debts.

One table for all of them — `type` only says whether the thing has an end, and
`is_limit` whether the amount is a single payment or a limit that fills up over
the month. A commitment can sit in any budget: rent in needs, streaming in
wants, a savings plan in savings.

Commitments are **private by default**, even inside a shared household. A member
sees another member's contract only if that member granted `Area.COMMITMENTS`
insight; without it they see the position it produces, and only if the owner
attached that position to the household.
"""

import uuid
from datetime import date

from fastapi import APIRouter, Depends, HTTPException, Query, status
from sqlalchemy import or_, select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.auth import current_active_user
from app.core.permissions import Area, can_assign_to_household, granted_level, require
from app.db.session import get_session
from app.models.commitment import Commitment
from app.models.enums import AccessLevel, CommitmentType, resolve_budget
from app.models.plan import PlanPosition
from app.models.user import User
from app.schemas.commitment import CommitmentCreate, CommitmentRead, CommitmentUpdate

#: Fields of `CommitmentUpdate` that map to a NOT NULL column. Every one of them is
#: optional in the schema, which makes an explicit `null` legal for Pydantic and a
#: 500 for the database — so the endpoint turns it into a 422 first.
NOT_NULLABLE = (
    "name",
    "amount",
    "category",
    "budget",
    "interval_months",
    "first_due_date",
    "is_limit",
    "pass_through",
)

STATUS_FILTERS = ("active", "ended", "all")


def _check_ends_on(first_due_date: date, ends_on: date | None) -> None:
    """An end before the start month can never be due — see the schema's twin check."""
    if ends_on is not None and (ends_on.year, ends_on.month) < (
        first_due_date.year,
        first_due_date.month,
    ):
        raise HTTPException(
            status.HTTP_422_UNPROCESSABLE_CONTENT, detail={"code": "ends_on_before_start"}
        )


def _check_limit(type_: CommitmentType, is_limit: bool) -> None:
    """A limit is a property of a contract and of nothing else.

    A debt or a savings goal has a fixed amount and income has no limit at all.
    Left unchecked, `create_plan` would copy the flag onto the month's position and
    take its tick box away for good. Rejected rather than quietly reset: the caller
    asked for something that cannot be, and should hear it.
    """
    if is_limit and type_ is not CommitmentType.CONTRACT:
        raise HTTPException(
            status.HTTP_422_UNPROCESSABLE_CONTENT, detail={"code": "limit_only_for_contract"}
        )


router = APIRouter()


async def _load(
    session: AsyncSession,
    commitment_id: uuid.UUID,
    user: User,
    *,
    needs: AccessLevel = AccessLevel.EDIT,
) -> Commitment:
    """Load a commitment the user is allowed to act on.

    Their own always, and somebody else’s from the level the owner granted.
    `granted_level()` answers `edit` for oneself, so there is no separate case
    for the normal path. `needs` separates changing from deleting.
    """
    commitment = await session.get(Commitment, commitment_id)
    if commitment is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "commitment_not_found"})
    # Your own is always yours to delete — whether it is still allowed is the
    # separate question of `_require_unused`. Others' need the granted level.
    if commitment.owner_id == user.id:
        return commitment
    level = await granted_level(session, commitment.owner_id, user.id, Area.COMMITMENTS)
    require(
        level.rank >= needs.rank,
        "no_delete_granted" if needs is AccessLevel.DELETE else "no_edit_granted",
    )
    return commitment


async def _mark_deletable(session: AsyncSession, commitments: list[Commitment]) -> None:
    """Set `deletable` on each: true while no month position refers to it.

    Not a column: it is derived, so it cannot drift from the positions. One query
    for the whole list.
    """
    ids = [commitment.id for commitment in commitments]
    used: set[uuid.UUID] = set()
    if ids:
        rows = await session.execute(
            select(PlanPosition.commitment_id)
            .where(PlanPosition.commitment_id.in_(ids))
            .distinct()
        )
        used = set(rows.scalars())
    for commitment in commitments:
        commitment.deletable = commitment.id not in used


@router.get("", response_model=list[CommitmentRead])
async def list_commitments(
    owner: uuid.UUID | None = None,
    status_filter: str = Query("all", alias="status"),
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> list[Commitment]:
    """Commitments, running and ended alike — or only one of the two.

    `status=active` keeps those without an end and those whose last month is this
    one or later, `status=ended` the rest, `status=all` (the default, so existing
    callers see what they always saw) everything. Measured against today every
    time, never stored: a contract that ends in September turns from active to
    ended on 1 October without anybody touching it.

    Without `owner` your own. With `owner` those of that person, which needs at
    least `view` on `Area.COMMITMENTS` — and that level comes from them, not from
    the asker.

    The same shape as `/accounts` and `/transactions`: one route per resource, the
    person as a parameter. Nothing tells the reader whether a list is their own, and
    nothing has to.
    """
    owner_id = owner or user.id
    if owner_id != user.id:
        level = await granted_level(session, owner_id, user.id, Area.COMMITMENTS)
        require(level.rank >= AccessLevel.VIEW.rank, "no_insight_granted")

    if status_filter not in STATUS_FILTERS:
        raise HTTPException(
            status.HTTP_422_UNPROCESSABLE_CONTENT, detail={"code": "invalid_status_filter"}
        )

    query = select(Commitment).where(Commitment.owner_id == owner_id)
    running = or_(Commitment.ends_on.is_(None), Commitment.ends_on >= date.today().replace(day=1))
    if status_filter == "active":
        query = query.where(running)
    elif status_filter == "ended":
        query = query.where(~running)

    result = await session.execute(
        query.order_by(Commitment.budget, Commitment.amount.desc())
    )
    commitments = list(result.scalars())
    await _mark_deletable(session, commitments)
    return commitments


@router.post("", response_model=CommitmentRead, status_code=status.HTTP_201_CREATED)
async def create_commitment(
    payload: CommitmentCreate,
    owner: uuid.UUID | None = None,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> Commitment:
    """Create a commitment — your own, or that of a member who granted `edit`.

    The household check runs against the **owner**: a contract may only go into a
    household its owner belongs to, and that stays true no matter who types it in.
    """
    owner_id = owner or user.id
    if owner_id != user.id:
        level = await granted_level(session, owner_id, user.id, Area.COMMITMENTS)
        require(level.rank >= AccessLevel.EDIT.rank, "no_edit_granted")

    require(
        await can_assign_to_household(session, owner_id, payload.household_id),
        "not_household_member",
    )

    # A limit only means something on a contract — see `_check_limit`.
    _check_limit(payload.type, payload.is_limit)

    data = payload.model_dump()
    # For savings goals and debts the budget is settled — the user choice is
    # overridden so that repayment cannot pass as a want.
    data["budget"] = resolve_budget(payload.budget, payload.type)

    commitment = Commitment(owner_id=owner_id, **data)
    session.add(commitment)
    await session.commit()
    await session.refresh(commitment)
    await _mark_deletable(session, [commitment])
    return commitment


@router.patch("/{commitment_id}", response_model=CommitmentRead)
async def update_commitment(
    commitment_id: uuid.UUID,
    payload: CommitmentUpdate,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> Commitment:
    commitment = await _load(session, commitment_id, user)
    changes = payload.model_dump(exclude_unset=True)

    # Checked before anything is assigned, so a rejected update leaves the
    # commitment exactly as it was.
    for field in NOT_NULLABLE:
        if field in changes and changes[field] is None:
            raise HTTPException(
                status.HTTP_422_UNPROCESSABLE_CONTENT, detail={"code": "null_not_allowed"}
            )
    _check_limit(commitment.type, changes.get("is_limit", commitment.is_limit))
    # Against what is stored, so moving either date alone cannot break the pair.
    # Only when one of the two is touched: the migration can leave a row with an end
    # before its start (never due anyway), and renaming it must still work.
    if "ends_on" in changes or "first_due_date" in changes:
        _check_ends_on(
            changes.get("first_due_date", commitment.first_due_date),
            changes["ends_on"] if "ends_on" in changes else commitment.ends_on,
        )

    if "household_id" in changes:
        require(
            await can_assign_to_household(session, user.id, changes["household_id"]),
            "not_household_member",
        )

    for field, value in changes.items():
        setattr(commitment, field, value)

    # Re-derive after every change — the type can override the budget.
    commitment.budget = resolve_budget(commitment.budget, commitment.type)

    await session.commit()
    await session.refresh(commitment)
    await _mark_deletable(session, [commitment])
    return commitment


@router.delete("/{commitment_id}", status_code=status.HTTP_204_NO_CONTENT)
async def delete_commitment(
    commitment_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> None:
    """Delete a commitment nobody has used yet — a typo, not history.

    Once a month position refers to it only ending it is left (`ends_on`), so the
    months already planned keep their origin.
    """
    commitment = await _load(session, commitment_id, user, needs=AccessLevel.DELETE)
    await _mark_deletable(session, [commitment])
    require(commitment.deletable, "commitment_in_use")
    await session.delete(commitment)
    await session.commit()
