"""Who may do what.

No role framework — a handful of rules the endpoints call into.

    Commitment   owner, plus whoever they granted `commitments` access to
    Plan         owner, plus whoever they granted `plan` access to
    Position     read:  plan owner plus members of the household it is in
                 write: the same, and every change is recorded
    Household    read:  members
                 write: the owner role only

The predicates return `bool`. `require()` turns that into an HTTP error carrying
a **code** which the frontend translates.
"""

import uuid
from enum import StrEnum

from fastapi import HTTPException, status
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.enums import AccessLevel, Role
from app.models.household import HouseholdMember
from app.models.plan import Plan
from app.models.user import User


class Area(StrEnum):
    """Which kind of data a grant is about.

    Not stored anywhere — the value names the column on `HouseholdMember` that
    carries the level. Keeping it out of the database means adding an area is a
    migration for the new column and nothing else.
    """

    PLAN = "plan"
    COMMITMENTS = "commitments"
    #: The book belongs here too, see `HouseholdMember.grants_accounts`.
    ACCOUNTS = "accounts"


#: Which column answers for which area.
_GRANT_COLUMN = {
    Area.PLAN: HouseholdMember.grants_plan,
    Area.COMMITMENTS: HouseholdMember.grants_commitments,
    Area.ACCOUNTS: HouseholdMember.grants_accounts,
}


def require(allowed: bool, code: str) -> None:
    """Raise a 403 with an error code unless allowed.

    The code is machine readable; the wording comes from the frontend.
    """
    if not allowed:
        raise HTTPException(status.HTTP_403_FORBIDDEN, detail={"code": code})


async def is_member(session: AsyncSession, user_id: uuid.UUID, household_id: uuid.UUID) -> bool:
    result = await session.execute(
        select(HouseholdMember.id).where(
            HouseholdMember.user_id == user_id,
            HouseholdMember.household_id == household_id,
        )
    )
    return result.scalar_one_or_none() is not None


async def granted_level(
    session: AsyncSession, owner_id: uuid.UUID, viewer_id: uuid.UUID, area: Area
) -> AccessLevel:
    """What `viewer` may do with `owner` data in one area, across all households.

    The level hangs on **the owner** membership: they grant it, not the person who
    wants to use it. If both share several households, the highest level wins —
    otherwise the right would depend on which household one happens to be looking
    through, and the same person would see different things by different routes.

    The areas are independent of each other. Granting insight into the month says
    nothing about the contracts behind it, and a caller has to name which one it
    is asking about — there is no default, because a wrong guess here hands out
    data.

    There is no restriction towards yourself.
    """
    if owner_id == viewer_id:
        return AccessLevel.EDIT

    gemeinsam = select(HouseholdMember.household_id).where(
        HouseholdMember.user_id == viewer_id
    )
    result = await session.execute(
        select(_GRANT_COLUMN[area]).where(
            HouseholdMember.user_id == owner_id,
            HouseholdMember.household_id.in_(gemeinsam),
        )
    )
    stufen = [AccessLevel(x) for x in result.scalars()]
    return max(stufen, key=lambda s: s.rank) if stufen else AccessLevel.PLAN


async def viewable_members(
    session: AsyncSession, household_id: uuid.UUID, viewer_id: uuid.UUID, area: Area
) -> list[uuid.UUID]:
    """Whose figures may be added up for a household view of one area.

    Always oneself, plus every member who granted at least `view` in that area.
    Anyone below that is missing from the list — the totals are then incomplete and
    the frontend says so. A number silently missing a person would be worse than no
    number at all.

    The area matters: a household book adds up accounts, so somebody who shares
    their month but not their bookings does not belong in that total.

    Assumes the asker is a member; the caller checks that.
    """
    result = await session.execute(
        select(HouseholdMember.user_id, _GRANT_COLUMN[area]).where(
            HouseholdMember.household_id == household_id
        )
    )
    return [
        user_id
        for user_id, level in result.all()
        if user_id == viewer_id or AccessLevel(level).rank >= AccessLevel.VIEW.rank
    ]


async def is_household_owner(
    session: AsyncSession, user_id: uuid.UUID, household_id: uuid.UUID
) -> bool:
    result = await session.execute(
        select(HouseholdMember.id).where(
            HouseholdMember.user_id == user_id,
            HouseholdMember.household_id == household_id,
            HouseholdMember.role == Role.OWNER,
        )
    )
    return result.scalar_one_or_none() is not None


def owns_plan(user: User, plan: Plan) -> bool:
    return plan.user_id == user.id


async def can_assign_to_household(
    session: AsyncSession, plan_owner_id: uuid.UUID, household_id: uuid.UUID | None
) -> bool:
    """A position may only go into a household its owner belongs to.

    Without this check, positions could be pushed into other people\'s households.
    """
    if household_id is None:
        return True
    return await is_member(session, plan_owner_id, household_id)
