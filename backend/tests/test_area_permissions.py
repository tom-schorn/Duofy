"""What one member may see of another, area by area.

The grant used to be a single level covering everything a person had. It is five
areas now, granted per person (#254), and the rules worth pinning down are the
ones that make that split worth having:

* the areas do not leak into one another
* the level comes from the **owner**, never from whoever is asking
* you are never restricted towards yourself
"""

import uuid
from datetime import datetime

from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.permissions import Area, granted_level, require_level
from app.models.enums import AccessLevel, Role
from app.models.household import Household, HouseholdMember
from app.models.user import User
from app.services.grants import set_levels


async def make_user(session: AsyncSession, first_name: str) -> User:
    user = User(
        email=f"{first_name.lower()}-{uuid.uuid4().hex[:8]}@example.org",
        hashed_password="not-a-real-hash",
        is_active=True,
        is_superuser=False,
        is_verified=True,
        first_name=first_name,
        last_name="Person",
    )
    session.add(user)
    await session.flush()
    return user


async def make_household(session: AsyncSession, name: str) -> Household:
    household = Household(name=name)
    session.add(household)
    await session.flush()
    return household


#: What each member of a test household hands out to everybody else, per area —
#: the old "one level for all" shape most tests were written in. Kept here so a
#: member added later receives what the earlier ones grant, too.
_LEVELS: dict[tuple[uuid.UUID, uuid.UUID], dict[Area, AccessLevel]] = {}


def legacy_levels(
    *, plan: AccessLevel, commitments: AccessLevel, accounts: AccessLevel
) -> dict[Area, AccessLevel]:
    """The three old areas mapped the way the migration maps them: the book and the
    import follow the accounts."""
    return {
        Area.PLAN: plan,
        Area.COMMITMENTS: commitments,
        Area.ACCOUNTS: accounts,
        Area.BOOK: accounts,
        Area.IMPORT: accounts,
    }


async def grant_to_all(
    session: AsyncSession, household: Household, user: User, levels: dict[Area, AccessLevel]
) -> None:
    """`user` grants `levels` to every other member of `household`, now and later."""
    key = (household.id, user.id)
    _LEVELS[key] = {**_LEVELS.get(key, {}), **levels}
    others = await session.execute(
        select(HouseholdMember.user_id).where(
            HouseholdMember.household_id == household.id, HouseholdMember.user_id != user.id
        )
    )
    for other in others.scalars():
        await set_levels(session, user.id, other, levels)


async def add_member(
    session: AsyncSession,
    household: Household,
    user: User,
    *,
    plan: AccessLevel = AccessLevel.NONE,
    commitments: AccessLevel = AccessLevel.NONE,
    accounts: AccessLevel = AccessLevel.NONE,
    joined_at: datetime | None = None,
) -> HouseholdMember:
    """A member who grants the same levels to everybody in the household."""
    member = HouseholdMember(household_id=household.id, user_id=user.id, role=Role.MEMBER)
    if joined_at is not None:
        member.created_at = joined_at
    session.add(member)
    await session.flush()

    # What the earlier members hand out reaches the newcomer as well.
    for (household_id, granter_id), levels in _LEVELS.items():
        if household_id == household.id and granter_id != user.id:
            await set_levels(session, granter_id, user.id, levels)
    await grant_to_all(
        session,
        household,
        user,
        legacy_levels(plan=plan, commitments=commitments, accounts=accounts),
    )
    return member


async def test_areas_do_not_leak_into_each_other(session: AsyncSession) -> None:
    """Insight into the month says nothing about the contracts behind it."""
    owner = await make_user(session, "Owner")
    viewer = await make_user(session, "Viewer")
    household = await make_household(session, "WG")
    await add_member(session, household, owner, plan=AccessLevel.EDIT)
    await add_member(session, household, viewer)

    assert await granted_level(session, owner.id, viewer.id, Area.PLAN) is AccessLevel.EDIT
    assert await granted_level(session, owner.id, viewer.id, Area.COMMITMENTS) is AccessLevel.NONE
    assert await granted_level(session, owner.id, viewer.id, Area.ACCOUNTS) is AccessLevel.NONE


async def test_the_owner_grants_not_the_asker(session: AsyncSession) -> None:
    """What the asker granted about themselves does not raise what they may see."""
    owner = await make_user(session, "Owner")
    viewer = await make_user(session, "Viewer")
    household = await make_household(session, "WG")
    await add_member(session, household, owner, commitments=AccessLevel.VIEW)
    await add_member(session, household, viewer, commitments=AccessLevel.EDIT)

    assert await granted_level(session, owner.id, viewer.id, Area.COMMITMENTS) is AccessLevel.VIEW


async def test_strangers_get_nothing(session: AsyncSession) -> None:
    """No shared household at all is the same answer as no grant."""
    owner = await make_user(session, "Owner")
    stranger = await make_user(session, "Stranger")
    household = await make_household(session, "WG")
    await add_member(session, household, owner, commitments=AccessLevel.EDIT)

    assert (
        await granted_level(session, owner.id, stranger.id, Area.COMMITMENTS) is AccessLevel.NONE
    )


async def test_no_restriction_towards_yourself(session: AsyncSession) -> None:
    """Your own data is `delete`, the top step, in every area, membership or not."""
    owner = await make_user(session, "Owner")

    for area in Area:
        assert await granted_level(session, owner.id, owner.id, area) is AccessLevel.DELETE


async def test_require_level_does_not_crash_on_any_level(session: AsyncSession) -> None:
    """`require_level` looks the refusal code up eagerly, so every level needs one.

    A missing entry would raise `KeyError` before `require` ever looks at the
    condition and turn the refusal into a 500. `none` is the level that has no
    call site today — the very reason a missing entry stays unseen until it
    hits production.
    """
    owner = await make_user(session, "Owner")
    viewer = await make_user(session, "Viewer")
    household = await make_household(session, "WG")
    await add_member(session, household, owner)
    await add_member(session, household, viewer)

    # `none` is the lowest level: every viewer already meets it, so no refusal
    # is raised. The point is that the lookup itself does not blow up.
    await require_level(session, owner.id, viewer, Area.PLAN, AccessLevel.NONE)
