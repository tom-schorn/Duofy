"""Personal default quotas and the sum-100 rule (#84)."""

from decimal import Decimal

import pytest
from httpx import AsyncClient
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.enums import AccessLevel
from app.models.plan import Plan
from app.models.user import User
from tests.test_area_permissions import add_member, make_household, make_user
from tests.test_delegation import grant, sign_in
from tests.test_refresh_tokens import PASSWORD, register_and_login

QUOTAS = {"targetNeeds": "65", "targetWants": "20", "targetSavings": "15"}


async def bearer(client: AsyncClient) -> dict[str, str]:
    """A real signed-in user: fastapi-users' own routes ignore `sign_in`."""
    await register_and_login(client, "quota@example.org")
    response = await client.post(
        "/api/v1/auth/login", data={"username": "quota@example.org", "password": PASSWORD}
    )
    return {"Authorization": f"Bearer {response.json()['access_token']}"}


async def plan_of(session: AsyncSession, user) -> Plan:
    return (await session.execute(select(Plan).where(Plan.user_id == user.id))).scalar_one()


async def test_a_new_month_starts_from_the_personal_default(
    client: AsyncClient, session: AsyncSession
):
    user = await make_user(session, "Owner")
    user.target_needs, user.target_wants = Decimal("65"), Decimal("20")
    user.target_savings, user.buffer_percent = Decimal("15"), Decimal("5")
    await session.commit()
    sign_in(user)

    assert (await client.post("/api/v1/plans", json={"year": 2026, "month": 9})).status_code == 201

    plan = await plan_of(session, user)
    assert (plan.target_needs, plan.target_wants, plan.target_savings) == (65, 20, 15)
    assert plan.buffer_percent == 5


async def test_changing_the_default_leaves_existing_months_alone(
    client: AsyncClient, session: AsyncSession
):
    headers = await bearer(client)
    user = await session.scalar(select(User))
    await client.post("/api/v1/plans", json={"year": 2026, "month": 9}, headers=headers)

    response = await client.patch("/api/v1/users/me", json=QUOTAS, headers=headers)
    assert response.status_code == 200
    assert Decimal(response.json()["targetNeeds"]) == 65

    assert (await plan_of(session, user)).target_needs == 50
    await client.post("/api/v1/plans", json={"year": 2026, "month": 10}, headers=headers)
    october = (
        await session.execute(select(Plan).where(Plan.user_id == user.id, Plan.month == 10))
    ).scalar_one()
    assert october.target_needs == 65


async def test_a_month_created_for_someone_else_takes_the_owners_default(
    client: AsyncClient, session: AsyncSession
):
    owner = await make_user(session, "Owner")
    helper = await make_user(session, "Helper")
    owner.target_needs, owner.target_wants = Decimal("65"), Decimal("20")
    owner.target_savings = Decimal("15")
    household = await make_household(session, "Shared")
    await add_member(session, household, owner)
    await add_member(session, household, helper)
    await session.commit()
    await grant(session, household, owner, AccessLevel.EDIT)
    sign_in(helper)

    response = await client.post(f"/api/v1/plans?owner={owner.id}", json={"year": 2026, "month": 9})
    assert response.status_code == 201
    assert (await plan_of(session, owner)).target_needs == 65


@pytest.mark.parametrize(
    "body, code",
    [
        (
            {"targetNeeds": "60", "targetWants": "30", "targetSavings": "20"},
            "quotas_must_sum_to_100",
        ),
        ({"targetNeeds": "60"}, "quotas_incomplete"),
    ],
)
async def test_quotas_not_adding_up_to_100_are_rejected(
    client: AsyncClient, session: AsyncSession, body, code
):
    user = await make_user(session, "Owner")
    household = await make_household(session, "Shared")
    await add_member(session, household, user)
    await session.commit()
    sign_in(user)
    await client.post("/api/v1/plans", json={"year": 2026, "month": 9})
    plan = await plan_of(session, user)

    headers = await bearer(client)
    for url in (
        "/api/v1/users/me",
        f"/api/v1/plans/{plan.id}",
        f"/api/v1/households/{household.id}",
    ):
        response = await client.patch(url, json=body, headers=headers if "users" in url else {})
        assert response.status_code == 422, url
        assert code in response.text, url


async def test_any_member_sets_the_household_quota_but_only_the_owner_renames(
    client: AsyncClient, session: AsyncSession
):
    from app.models.enums import Role
    from app.models.household import HouseholdMember

    owner = await make_user(session, "Owner")
    member = await make_user(session, "Member")
    household = await make_household(session, "Shared")
    await add_member(session, household, owner)
    await add_member(session, household, member)
    row = await session.scalar(
        select(HouseholdMember).where(
            HouseholdMember.household_id == household.id, HouseholdMember.user_id == owner.id
        )
    )
    row.role = Role.OWNER
    await session.commit()
    sign_in(member)

    ok = await client.patch(f"/api/v1/households/{household.id}", json=QUOTAS)
    assert ok.status_code == 200
    assert Decimal(ok.json()["targetNeeds"]) == 65

    renamed = await client.patch(f"/api/v1/households/{household.id}", json={"name": "New"})
    assert renamed.status_code == 403
    assert renamed.json()["detail"] == {"code": "not_household_owner"}


async def test_more_than_two_decimals_are_rejected_not_a_server_error(
    client: AsyncClient, session: AsyncSession
):
    """33.335 would pass a sum check and then hit the database CHECK."""
    user = await make_user(session, "Owner")
    household = await make_household(session, "Shared")
    await add_member(session, household, user)
    await session.commit()
    sign_in(user)
    await client.post("/api/v1/plans", json={"year": 2026, "month": 9})
    plan = await plan_of(session, user)
    headers = await bearer(client)
    body = {"targetNeeds": "33.335", "targetWants": "33.335", "targetSavings": "33.33"}

    for url in (
        "/api/v1/users/me",
        f"/api/v1/plans/{plan.id}",
        f"/api/v1/households/{household.id}",
    ):
        response = await client.patch(url, json=body, headers=headers if "users" in url else {})
        assert response.status_code == 422, url
        assert "decimal_max_places" in response.text, url
