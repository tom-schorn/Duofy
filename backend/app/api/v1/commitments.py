"""Contracts, budgets, savings goals and debts.

One table for all of them — `type` only says whether the thing has an end. A
commitment can sit in any block: rent in needs, streaming in wants, a savings plan
in savings.

Commitments are **private by default**, even inside a shared household. A member
sees another member's contract only if that member granted `Area.COMMITMENTS`
insight; without it they see the position it produces, and only if the owner
attached that position to the household.
"""

import uuid

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.auth import current_active_user
from app.core.permissions import Area, can_assign_to_household, granted_level, require
from app.db.session import get_session
from app.models.commitment import Commitment
from app.models.enums import AccessLevel, resolve_block
from app.models.user import User
from app.schemas.commitment import CommitmentCreate, CommitmentRead, CommitmentUpdate

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
    level = await granted_level(session, commitment.owner_id, user.id, Area.COMMITMENTS)
    require(
        level.rank >= needs.rank,
        "no_delete_granted" if needs is AccessLevel.DELETE else "no_edit_granted",
    )
    return commitment


@router.get("", response_model=list[CommitmentRead])
async def list_commitments(
    owner: uuid.UUID | None = None,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> list[Commitment]:
    """Commitments, active and retired alike.

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

    result = await session.execute(
        select(Commitment)
        .where(Commitment.owner_id == owner_id)
        .order_by(Commitment.block, Commitment.amount.desc())
    )
    return list(result.scalars())


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

    data = payload.model_dump()
    # For savings goals and debts the block is settled — the user choice is
    # overridden so that repayment cannot pass as a want.
    data["block"] = resolve_block(payload.block, payload.type)

    commitment = Commitment(owner_id=owner_id, **data)
    session.add(commitment)
    await session.commit()
    await session.refresh(commitment)
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

    if "household_id" in changes:
        require(
            await can_assign_to_household(session, user.id, changes["household_id"]),
            "not_household_member",
        )

    for field, value in changes.items():
        setattr(commitment, field, value)

    # Re-derive after every change — the type can override the block.
    commitment.block = resolve_block(commitment.block, commitment.type)

    # Catch what the database checks anyway, but with a readable error code.
    if commitment.rhythm.value != "monthly" and commitment.first_due_date is None:
        raise HTTPException(
            status.HTTP_422_UNPROCESSABLE_ENTITY, detail={"code": "first_due_date_required"}
        )

    await session.commit()
    await session.refresh(commitment)
    return commitment


@router.delete("/{commitment_id}", status_code=status.HTTP_204_NO_CONTENT)
async def delete_commitment(
    commitment_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> None:
    """Delete a commitment.

    Positions already generated stay — `commitment_id` is ON DELETE SET NULL.
    Nothing new is generated for future months.
    """
    commitment = await _load(session, commitment_id, user, needs=AccessLevel.DELETE)
    await session.delete(commitment)
    await session.commit()
