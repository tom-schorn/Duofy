"""A savings goal stops asking for money once it is reached (#87)."""

from datetime import date
from decimal import Decimal

import pytest
from httpx import AsyncClient
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.commitment import Commitment
from app.models.enums import Budget, Category, CommitmentType
from app.models.plan import Plan
from app.models.user import User
from tests.test_area_permissions import make_user
from tests.test_booking_month import make_account
from tests.test_delegation import sign_in


@pytest.fixture
async def owner(session: AsyncSession) -> User:
    user = await make_user(session, "Owner")
    await make_account(session, user)
    await session.commit()
    sign_in(user)
    return user


async def make_goal(
    session: AsyncSession, owner: User, *, amount: str = "50.00", target: str | None = "120.00"
) -> Commitment:
    goal = Commitment(
        owner_id=owner.id,
        type=CommitmentType.SAVINGS_GOAL,
        name="Holiday",
        amount=Decimal(amount),
        target_amount=Decimal(target) if target else None,
        category=Category.FINANCE_SAVINGS,
        budget=Budget.SAVINGS,
        interval_months=1,
        first_due_date=date(2026, 1, 1),
    )
    session.add(goal)
    await session.commit()
    return goal


async def plan_month(client: AsyncClient, month: int) -> list[dict]:
    response = await client.post("/api/v1/plans", json={"year": 2026, "month": month})
    assert response.status_code == 201, response.text
    return response.json()["positions"]


async def tick(client: AsyncClient, position: dict) -> None:
    response = await client.post(f"/api/v1/positions/{position['id']}/paid")
    assert response.status_code == 200, response.text


async def test_a_reached_goal_plans_no_position(
    client: AsyncClient, session: AsyncSession, owner: User
):
    await make_goal(session, owner)
    for month in (1, 2):
        (position,) = await plan_month(client, month)
        await tick(client, position)
    (last,) = await plan_month(client, 3)
    await tick(client, last)

    assert await plan_month(client, 4) == []


async def test_less_than_one_rate_missing_plans_only_the_rest(
    client: AsyncClient, session: AsyncSession, owner: User
):
    await make_goal(session, owner)
    for month in (1, 2):
        (position,) = await plan_month(client, month)
        await tick(client, position)

    (rest,) = await plan_month(client, 3)

    assert Decimal(rest["amountPlanned"]) == Decimal("20.00")


async def test_a_rate_that_is_not_ticked_does_not_count_as_saved(
    client: AsyncClient, session: AsyncSession, owner: User
):
    await make_goal(session, owner)
    await plan_month(client, 1)

    (second,) = await plan_month(client, 2)

    assert Decimal(second["amountPlanned"]) == Decimal("50.00")


async def test_a_goal_without_a_target_keeps_planning_its_rate(
    client: AsyncClient, session: AsyncSession, owner: User
):
    await make_goal(session, owner, target=None)
    for month in range(1, 5):
        (position,) = await plan_month(client, month)
        await tick(client, position)

    (position,) = await plan_month(client, 5)

    assert Decimal(position["amountPlanned"]) == Decimal("50.00")


async def test_unticking_a_rate_plans_it_again(
    client: AsyncClient, session: AsyncSession, owner: User
):
    await make_goal(session, owner)
    positions = []
    for month in (1, 2, 3):
        (position,) = await plan_month(client, month)
        await tick(client, position)
        positions.append(position)
    assert await plan_month(client, 4) == []
    plan = (
        await session.execute(select(Plan).where(Plan.user_id == owner.id, Plan.month == 4))
    ).scalar_one()
    await session.delete(plan)
    await session.commit()

    response = await client.delete(f"/api/v1/positions/{positions[2]['id']}/paid")
    assert response.status_code == 200, response.text

    (again,) = await plan_month(client, 4)
    assert Decimal(again["amountPlanned"]) == Decimal("20.00")
