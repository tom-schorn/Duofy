"""The figures the plan overview shows: `distributable` and `unpaid`.

Both changed with #106 and neither had a test of its own before.

`distributable` is the amount that may be allocated — income minus the buffer.
It used to be called `budget`, which was also the name of the 50/30/20
dimension; one word for two things is exactly the kind of thing that confuses
people who need clear terms.

`unpaid` is "Noch offen" in the interface, and it must ignore limits. A limit
has no tick: it runs until the month is over. Counting what is left of it would
keep every month looking unfinished right up to the 31st, which is the opposite
of what the number is for.

Part of #106.
"""

from decimal import Decimal

import pytest
from httpx import AsyncClient
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.enums import Budget, Category
from app.models.plan import Plan, PlanPosition
from app.models.user import User
from tests.test_area_permissions import make_user
from tests.test_delegation import sign_in


@pytest.fixture
async def owner(session: AsyncSession) -> User:
    user = await make_user(session, "Owner")
    await session.commit()
    sign_in(user)
    return user


async def make_plan(session: AsyncSession, owner: User, *, buffer_percent: str = "0.00") -> Plan:
    plan = Plan(
        user_id=owner.id,
        year=2026,
        month=9,
        buffer_percent=Decimal(buffer_percent),
    )
    session.add(plan)
    await session.flush()
    return plan


def position(plan: Plan, label: str, amount: str, **kwargs) -> PlanPosition:
    """A position with the usual fields, so the tests show only what they vary."""
    return PlanPosition(
        plan_id=plan.id,
        label=label,
        amount_planned=Decimal(amount),
        category=kwargs.pop("category", Category.HOUSEHOLD_GROCERIES),
        budget=kwargs.pop("budget", Budget.NEEDS),
        due_day=1,
        **kwargs,
    )


def figure(figures: dict, name: str) -> str:
    """One figure out of the summary, with a readable error when it is missing."""
    assert name in figures, f"{name} missing from the plan summary: {sorted(figures)}"
    return figures[name]


async def summary(client: AsyncClient) -> dict:
    response = await client.get("/api/v1/plans/2026/9")
    assert response.status_code == 200
    return response.json()


async def test_a_limit_does_not_count_as_still_open(
    client: AsyncClient, session: AsyncSession, owner: User
):
    """Only the rent is still to be paid — the grocery limit simply runs."""
    plan = await make_plan(session, owner)
    session.add(position(plan, "Rent", "890.00", category=Category.HOUSING_RENT))
    session.add(position(plan, "Groceries", "600.00", is_limit=True))
    await session.commit()

    assert Decimal(figure(await summary(client), "unpaid")) == Decimal("890.00")


async def test_a_part_paid_limit_still_counts_for_nothing(
    client: AsyncClient, session: AsyncSession, owner: User
):
    """Not even the remainder of it.

    A limit of 600 with 127.50 of purchases booked would have contributed 472.50
    to "Noch offen" before #106 — a number nobody is waiting to pay.
    """
    plan = await make_plan(session, owner)
    session.add(
        position(plan, "Groceries", "600.00", is_limit=True, amount_actual=Decimal("127.50"))
    )
    await session.commit()

    assert Decimal(figure(await summary(client), "unpaid")) == Decimal("0.00")


async def test_an_open_single_payment_counts_what_is_left_of_it(
    client: AsyncClient, session: AsyncSession, owner: User
):
    """The rule for single payments is untouched: planned minus what is booked."""
    plan = await make_plan(session, owner)
    session.add(
        position(
            plan,
            "Rent",
            "890.00",
            category=Category.HOUSING_RENT,
            amount_actual=Decimal("200.00"),
        )
    )
    await session.commit()

    assert Decimal(figure(await summary(client), "unpaid")) == Decimal("690.00")


async def test_distributable_is_income_minus_the_buffer(
    client: AsyncClient, session: AsyncSession, owner: User
):
    """The field the overview calls "Verteilbar" — 3000 less a tenth."""
    plan = await make_plan(session, owner, buffer_percent="10.00")
    session.add(
        position(
            plan,
            "Salary",
            "3000.00",
            category=Category.INCOME_EARNED,
            budget=Budget.INCOME,
        )
    )
    await session.commit()

    figures = await summary(client)
    assert Decimal(figure(figures, "income")) == Decimal("3000.00")
    assert Decimal(figure(figures, "distributable")) == Decimal("2700.00")
    assert "budget" not in figures, "the old name must be gone, not kept alongside"

