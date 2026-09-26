"""Hints on the plan — the pipe and its first rule, `position_overdue`.

The backend decides which hints apply and ships them as code, severity and
values; the frontend only translates. Nothing is stored, so ticking a position off
takes its hint away on the next read.

Part of #88.
"""

from datetime import UTC, date, datetime
from decimal import Decimal

import pytest
from httpx import AsyncClient
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.enums import Budget, Category
from app.models.plan import Plan, PlanPosition
from app.models.user import User
from app.services import hints
from tests.test_area_permissions import add_member, make_household, make_user
from tests.test_delegation import sign_in


@pytest.fixture
async def owner(session: AsyncSession) -> User:
    user = await make_user(session, "Owner")
    await session.commit()
    sign_in(user)
    return user


def pin_today(monkeypatch: pytest.MonkeyPatch, day: date) -> None:
    monkeypatch.setattr(hints, "today", lambda: day)


async def make_plan(
    session: AsyncSession, owner: User, *, year: int = 2026, month: int = 9
) -> Plan:
    plan = Plan(user_id=owner.id, year=year, month=month)
    session.add(plan)
    await session.flush()
    return plan


def position(plan: Plan, due_day: int, **kwargs) -> PlanPosition:
    return PlanPosition(
        plan_id=plan.id,
        label="Rent",
        amount_planned=Decimal("890.00"),
        category=Category.HOUSING_RENT,
        budget=Budget.NEEDS,
        due_day=due_day,
        **kwargs,
    )


async def read_hints(client: AsyncClient, year: int = 2026, month: int = 9) -> list[dict]:
    response = await client.get(f"/api/v1/plans/{year}/{month}")
    assert response.status_code == 200
    return response.json()["hints"]


async def test_a_plan_without_anything_to_point_out_has_an_empty_list(
    client: AsyncClient, session: AsyncSession, owner: User, monkeypatch: pytest.MonkeyPatch
):
    pin_today(monkeypatch, date(2026, 9, 2))
    await make_plan(session, owner)
    await session.commit()

    assert await read_hints(client) == []


async def test_an_open_commitment_is_overdue_the_day_after_it_fell_due(
    client: AsyncClient, session: AsyncSession, owner: User, monkeypatch: pytest.MonkeyPatch
):
    pin_today(monkeypatch, date(2026, 9, 2))
    plan = await make_plan(session, owner)
    rent = position(plan, 1)
    session.add(rent)
    await session.commit()

    assert await read_hints(client) == [
        {
            "code": "position_overdue",
            "severity": "warning",
            "positionId": str(rent.id),
            "params": {"due_date": "2026-09-01", "days_overdue": 1},
        }
    ]


async def test_on_the_due_day_itself_there_is_no_hint_yet(
    client: AsyncClient, session: AsyncSession, owner: User, monkeypatch: pytest.MonkeyPatch
):
    pin_today(monkeypatch, date(2026, 9, 1))
    plan = await make_plan(session, owner)
    session.add(position(plan, 1))
    await session.commit()

    assert await read_hints(client) == []


async def test_ticking_a_position_off_removes_its_hint(
    client: AsyncClient, session: AsyncSession, owner: User, monkeypatch: pytest.MonkeyPatch
):
    pin_today(monkeypatch, date(2026, 9, 10))
    plan = await make_plan(session, owner)
    session.add(position(plan, 1, paid_at=datetime(2026, 9, 5, tzinfo=UTC)))
    await session.commit()

    assert await read_hints(client) == []


async def test_a_limit_is_never_overdue(
    client: AsyncClient, session: AsyncSession, owner: User, monkeypatch: pytest.MonkeyPatch
):
    pin_today(monkeypatch, date(2026, 9, 28))
    plan = await make_plan(session, owner)
    session.add(position(plan, 1, is_limit=True))
    await session.commit()

    assert await read_hints(client) == []


async def test_a_position_due_on_the_31st_is_compared_against_the_30th_in_a_short_month(
    client: AsyncClient, session: AsyncSession, owner: User, monkeypatch: pytest.MonkeyPatch
):
    plan = await make_plan(session, owner)
    session.add(position(plan, 31))
    await session.commit()

    pin_today(monkeypatch, date(2026, 9, 30))
    assert await read_hints(client) == []

    pin_today(monkeypatch, date(2026, 10, 1))
    [hint] = await read_hints(client)
    assert hint["params"] == {"due_date": "2026-09-30", "days_overdue": 1}


async def test_a_future_month_never_produces_a_hint(
    client: AsyncClient, session: AsyncSession, owner: User, monkeypatch: pytest.MonkeyPatch
):
    pin_today(monkeypatch, date(2026, 9, 15))
    plan = await make_plan(session, owner, month=10)
    session.add(position(plan, 1))
    await session.commit()

    assert await read_hints(client, month=10) == []


async def test_a_past_month_keeps_its_hint_until_the_position_is_ticked_off(
    client: AsyncClient, session: AsyncSession, owner: User, monkeypatch: pytest.MonkeyPatch
):
    pin_today(monkeypatch, date(2026, 9, 15))
    plan = await make_plan(session, owner, month=7)
    session.add(position(plan, 3))
    await session.commit()

    [hint] = await read_hints(client, month=7)
    assert hint["params"] == {"due_date": "2026-07-03", "days_overdue": 74}


async def test_the_household_plan_carries_the_hints_of_the_positions_it_shows(
    client: AsyncClient, session: AsyncSession, owner: User, monkeypatch: pytest.MonkeyPatch
):
    pin_today(monkeypatch, date(2026, 9, 5))
    household = await make_household(session, "Shared")
    await add_member(session, household, owner)
    plan = await make_plan(session, owner)
    shared = position(plan, 1, household_id=household.id)
    private = position(plan, 2)
    session.add_all([shared, private])
    await session.commit()

    response = await client.get(f"/api/v1/plans/household/{household.id}/2026/9")
    assert response.status_code == 200
    assert [h["positionId"] for h in response.json()["hints"]] == [str(shared.id)]


async def test_income_and_pass_through_positions_are_overdue_too(
    client: AsyncClient, session: AsyncSession, owner: User, monkeypatch: pytest.MonkeyPatch
):
    """The issue excludes limits only: money that has not arrived, or has not been
    passed on, is as open as a bill."""
    pin_today(monkeypatch, date(2026, 9, 5))
    plan = await make_plan(session, owner)
    salary = position(plan, 1, budget=Budget.INCOME, category=Category.INCOME_EARNED)
    forwarded = position(plan, 2, pass_through=True)
    session.add_all([salary, forwarded])
    await session.commit()

    assert {h["positionId"] for h in await read_hints(client)} == {
        str(salary.id),
        str(forwarded.id),
    }
