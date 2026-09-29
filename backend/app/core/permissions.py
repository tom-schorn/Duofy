"""Who may do what.

No role framework — a handful of rules the endpoints call into.

    Everything   owner, plus whoever they granted the area to (`Grant`), at
                 the step the action needs: view, create, edit or delete
    Position     read:  plan owner plus members of the household it is in
                 write: the same, and every change is recorded
    Household    read:  members
                 write: the owner role only

The predicates return `bool`. `require()` turns that into an HTTP error carrying
a **code** which the frontend translates. `require_level()` and `load_owned()` are
the one path every endpoint takes for somebody else's data.

Whose data a list or a household view covers is the other half, in `scope.py`.
"""

import uuid

from fastapi import HTTPException, status
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.enums import AccessLevel, Area, Role
from app.models.grant import Grant
from app.models.household import HouseholdMember
from app.models.plan import Plan, PlanPosition
from app.models.user import User


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
    """What `viewer` may do with `owner` data in one area.

    The owner grants it to this one person (decision 57), and it counts only while
    both share a household: a grant is trust inside a household, and a row left
    over from before must not open anything. No row is `none`.

    The areas are independent of each other. Granting insight into the month says
    nothing about the contracts behind it, and a caller has to name which one it
    is asking about — there is no default, because a wrong guess here hands out
    data.

    There is no restriction towards yourself.
    """
    if owner_id == viewer_id:
        return AccessLevel.DELETE

    shared = select(HouseholdMember.household_id).where(HouseholdMember.user_id == viewer_id)
    level = await session.scalar(
        select(Grant.level).where(
            Grant.granter_id == owner_id,
            Grant.grantee_id == viewer_id,
            Grant.area == area,
            select(HouseholdMember.id)
            .where(
                HouseholdMember.user_id == owner_id,
                HouseholdMember.household_id.in_(shared),
            )
            .exists(),
        )
    )
    return AccessLevel(level) if level is not None else AccessLevel.NONE


#: Which code a refusal carries, by the level that was missing. Every level has
#: an entry — even `none`, though the check itself never fires below it: Python
#: evaluates the lookup before `require` looks at the condition, and a missing
#: entry would turn any `needs=none` call into a 500 the moment one is added.
_REFUSAL = {
    AccessLevel.NONE: "no_insight_granted",
    AccessLevel.VIEW: "no_insight_granted",
    AccessLevel.CREATE: "no_create_granted",
    AccessLevel.EDIT: "no_edit_granted",
    AccessLevel.DELETE: "no_delete_granted",
}


async def require_level(
    session: AsyncSession,
    owner_id: uuid.UUID,
    user: User,
    area: Area,
    needs: AccessLevel,
) -> None:
    """Refuse unless `user` may act on data of `owner_id` in `area` at level `needs`.

    Your own always passes. Everybody else needs the level the owner granted them,
    and the refusal says which step was missing: seeing, adding, changing or
    deleting.
    """
    if owner_id == user.id:
        return
    level = await granted_level(session, owner_id, user.id, area)
    require(level.rank >= needs.rank, _REFUSAL[needs])


async def load_owned[T](
    session: AsyncSession,
    model: type[T],
    record_id: uuid.UUID,
    user: User,
    area: Area,
    needs: AccessLevel = AccessLevel.EDIT,
    *,
    not_found: str,
    owner_attr: str = "owner_id",
) -> T:
    """Load one record by id that `user` may act on.

    A 404 with `not_found` if it does not exist — before any right is checked, so
    the answer is the same as it always was. Then `require_level` on whoever owns
    it: `owner_id` on most tables, `user_id` on a plan.
    """
    record = await session.get(model, record_id)
    if record is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": not_found})
    await require_level(session, getattr(record, owner_attr), user, area, needs)
    return record


async def load_position(
    session: AsyncSession,
    position_id: uuid.UUID,
    user: User,
    area: Area,
    needs: AccessLevel = AccessLevel.EDIT,
) -> tuple[PlanPosition, Plan]:
    """A position and its plan, checked against the plan owner.

    A position has no owner of its own: it belongs to whoever owns the plan it
    sits in. The area is the caller's — changing a position is a question of the
    plan, ticking it off or booking onto it a question of the book.
    """
    position = await session.get(PlanPosition, position_id)
    if position is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "position_not_found"})
    plan = await load_owned(
        session,
        Plan,
        position.plan_id,
        user,
        area,
        needs,
        not_found="plan_not_found",
        owner_attr="user_id",
    )
    return position, plan


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
