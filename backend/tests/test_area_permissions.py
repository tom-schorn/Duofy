"""What one member may see of another, area by area.

The grant used to be a single level covering everything a person had. It is three
now — plan, commitments, accounts — and the rules worth pinning down are the ones
that make that split worth having:

* the areas do not leak into one another
* the level comes from the **owner**, never from whoever is asking
* sharing several households takes the highest grant, not the nearest one
* you are never restricted towards yourself
"""

import uuid

import pytest
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.permissions import Area, granted_level
from app.models.enums import AccessLevel, Role
from app.models.household import Household, HouseholdMember
from app.models.user import User


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


async def add_member(
    session: AsyncSession,
    household: Household,
    user: User,
    *,
    plan: AccessLevel = AccessLevel.PLAN,
    commitments: AccessLevel = AccessLevel.PLAN,
    accounts: AccessLevel = AccessLevel.PLAN,
) -> HouseholdMember:
    member = HouseholdMember(
        household_id=household.id,
        user_id=user.id,
        role=Role.MEMBER,
        grants_plan=plan,
        grants_commitments=commitments,
        grants_accounts=accounts,
    )
    session.add(member)
    await session.flush()
    return member


@pytest.mark.anyio
async def test_areas_do_not_leak_into_each_other(session: AsyncSession) -> None:
    """Insight into the month says nothing about the contracts behind it."""
    owner = await make_user(session, "Owner")
    viewer = await make_user(session, "Viewer")
    household = await make_household(session, "WG")
    await add_member(session, household, owner, plan=AccessLevel.EDIT)
    await add_member(session, household, viewer)

    assert await granted_level(session, owner.id, viewer.id, Area.PLAN) is AccessLevel.EDIT
    assert await granted_level(session, owner.id, viewer.id, Area.COMMITMENTS) is AccessLevel.PLAN
    assert await granted_level(session, owner.id, viewer.id, Area.ACCOUNTS) is AccessLevel.PLAN


@pytest.mark.anyio
async def test_the_owner_grants_not_the_asker(session: AsyncSession) -> None:
    """What the asker granted about themselves does not raise what they may see."""
    owner = await make_user(session, "Owner")
    viewer = await make_user(session, "Viewer")
    household = await make_household(session, "WG")
    await add_member(session, household, owner, commitments=AccessLevel.VIEW)
    await add_member(session, household, viewer, commitments=AccessLevel.EDIT)

    assert await granted_level(session, owner.id, viewer.id, Area.COMMITMENTS) is AccessLevel.VIEW


@pytest.mark.anyio
async def test_the_highest_grant_wins_across_households(session: AsyncSession) -> None:
    """Otherwise the right would depend on which household one looks through."""
    owner = await make_user(session, "Owner")
    viewer = await make_user(session, "Viewer")
    flat = await make_household(session, "Flat")
    club = await make_household(session, "Club")
    await add_member(session, flat, owner, accounts=AccessLevel.VIEW)
    await add_member(session, flat, viewer)
    await add_member(session, club, owner, accounts=AccessLevel.EDIT)
    await add_member(session, club, viewer)

    assert await granted_level(session, owner.id, viewer.id, Area.ACCOUNTS) is AccessLevel.EDIT


@pytest.mark.anyio
async def test_strangers_get_nothing(session: AsyncSession) -> None:
    """No shared household at all is the same answer as sharing only the plan."""
    owner = await make_user(session, "Owner")
    stranger = await make_user(session, "Stranger")
    household = await make_household(session, "WG")
    await add_member(session, household, owner, commitments=AccessLevel.EDIT)

    assert (
        await granted_level(session, owner.id, stranger.id, Area.COMMITMENTS) is AccessLevel.PLAN
    )


@pytest.mark.anyio
async def test_no_restriction_towards_yourself(session: AsyncSession) -> None:
    """Your own data is `edit` in every area, membership or not."""
    owner = await make_user(session, "Owner")

    for area in Area:
        assert await granted_level(session, owner.id, owner.id, area) is AccessLevel.EDIT
