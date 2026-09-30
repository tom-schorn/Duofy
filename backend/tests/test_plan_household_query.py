"""The household plan is asked for with `?household=`, like accounts and the book.

Before #254 it had routes of its own under `/plans/household/...`. They are gone
without aliases: frontend and backend ship in one image, nobody else calls them.
When `owner` and `household` come together, the household wins — the same rule
as everywhere `resolve_scope` decides (`app/core/scope.py`).

Part of #254.
"""

from decimal import Decimal

import pytest
from httpx import AsyncClient
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.plan import Plan
from app.models.user import User
from tests.test_area_permissions import add_member, make_household, make_user
from tests.test_delegation import sign_in


@pytest.fixture
async def owner(session: AsyncSession) -> User:
    user = await make_user(session, "Owner")
    await session.commit()
    sign_in(user)
    return user


async def shared_household(session: AsyncSession, owner: User):
    partner = await make_user(session, "Partner")
    household = await make_household(session, "Shared")
    await add_member(session, household, owner)
    await add_member(session, household, partner)
    session.add(Plan(user_id=owner.id, year=2026, month=9))
    session.add(Plan(user_id=partner.id, year=2026, month=9))
    await session.commit()
    return household, partner


async def test_the_household_month_wins_over_an_owner_given_alongside(
    client: AsyncClient, session: AsyncSession, owner: User
):
    household, partner = await shared_household(session, owner)

    response = await client.get(f"/api/v1/plans/2026/9?owner={partner.id}&household={household.id}")

    assert response.status_code == 200
    assert response.json()["householdId"] == str(household.id)


async def test_the_household_month_list_wins_over_an_owner_given_alongside(
    client: AsyncClient, session: AsyncSession, owner: User
):
    """The partner granted nothing, so an `owner` lens would be refused; the
    household lens answers instead."""
    household, partner = await shared_household(session, owner)

    response = await client.get(f"/api/v1/plans?owner={partner.id}&household={household.id}")

    assert response.status_code == 200
    assert [(row["year"], row["month"]) for row in response.json()] == [(2026, 9)]


async def test_the_household_flow_wins_over_an_owner_given_alongside(
    client: AsyncClient, session: AsyncSession, owner: User
):
    household, partner = await shared_household(session, owner)

    response = await client.get(
        f"/api/v1/plans/2026/9/flow?owner={partner.id}&household={household.id}"
    )

    assert response.status_code == 200
    assert Decimal(response.json()["start"]) == 0


@pytest.mark.parametrize(
    "path",
    ["/api/v1/plans/household/{id}/2026/9", "/api/v1/plans/household/{id}/2026/9/flow"],
)
async def test_the_old_household_routes_are_gone(
    client: AsyncClient, session: AsyncSession, owner: User, path: str
):
    household, _ = await shared_household(session, owner)

    response = await client.get(path.format(id=household.id))

    assert response.status_code == 404


@pytest.mark.parametrize(
    "path",
    ["/api/v1/plans", "/api/v1/plans/2026/9", "/api/v1/plans/2026/9/flow"],
)
async def test_someone_outside_the_household_is_refused_on_every_household_route(
    client: AsyncClient, session: AsyncSession, owner: User, path: str
):
    """The household month carries every shared position — a stranger must not
    reach it by knowing the household id."""
    household, _ = await shared_household(session, owner)
    stranger = await make_user(session, "Stranger")
    await session.commit()
    sign_in(stranger)

    response = await client.get(f"{path}?household={household.id}")

    assert response.status_code == 403
    assert response.json()["detail"]["code"] == "not_household_member"
