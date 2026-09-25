"""Creating a month (`POST /plans`), on the current model, before #83.

A month is built once, from whichever commitments are due that month — never
regenerated, never copied from the month before. These tests pin down that rule
so #83, which changes the fields a commitment carries, does not also change what
`create_plan` decides to copy.

Part of #13.
"""

from datetime import date
from decimal import Decimal

import pytest
from httpx import AsyncClient
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.commitment import Commitment
from app.models.enums import Budget, Category, CommitmentType, Rhythm
from app.models.plan import Plan, PlanPosition
from app.models.user import User
from tests.test_area_permissions import make_user
from tests.test_delegation import sign_in


async def make_commitment(
    session: AsyncSession,
    owner: User,
    name: str,
    *,
    rhythm: Rhythm = Rhythm.MONTHLY,
    first_due_date: date = date(2026, 1, 1),
    due_day: int = 1,
    active: bool = True,
    amount: str = "50.00",
) -> Commitment:
    commitment = Commitment(
        owner_id=owner.id,
        type=CommitmentType.CONTRACT,
        name=name,
        amount=Decimal(amount),
        category=Category.LEISURE_SUBSCRIPTIONS,
        budget=Budget.WANTS,
        rhythm=rhythm,
        first_due_date=first_due_date,
        due_day=due_day,
        active=active,
    )
    session.add(commitment)
    await session.flush()
    return commitment


@pytest.fixture
async def owner(session: AsyncSession) -> User:
    user = await make_user(session, "Owner")
    await session.commit()
    sign_in(user)
    return user


async def test_a_due_commitment_becomes_a_position(
    client: AsyncClient, session: AsyncSession, owner: User
):
    await make_commitment(session, owner, "Rent")
    await session.commit()

    response = await client.post("/api/v1/plans", json={"year": 2026, "month": 9})
    assert response.status_code == 201

    plan = (
        await session.execute(select(Plan).where(Plan.user_id == owner.id))
    ).scalar_one()
    positions = (
        (
            await session.execute(
                select(PlanPosition).where(PlanPosition.plan_id == plan.id)
            )
        )
        .scalars()
        .all()
    )
    assert [position.label for position in positions] == ["Rent"]


async def test_a_commitment_not_due_this_month_is_left_out(
    client: AsyncClient, session: AsyncSession, owner: User
):
    """Quarterly from January is due in Jan/Apr/Jul/Oct — not in September."""
    await make_commitment(session, owner, "Insurance", rhythm=Rhythm.QUARTERLY)
    await session.commit()

    response = await client.post("/api/v1/plans", json={"year": 2026, "month": 9})
    assert response.status_code == 201

    plan = (
        await session.execute(select(Plan).where(Plan.user_id == owner.id))
    ).scalar_one()
    positions = (
        (
            await session.execute(
                select(PlanPosition).where(PlanPosition.plan_id == plan.id)
            )
        )
        .scalars()
        .all()
    )
    assert positions == []


async def test_an_inactive_commitment_is_left_out(
    client: AsyncClient, session: AsyncSession, owner: User
):
    await make_commitment(session, owner, "Cancelled gym", active=False)
    await session.commit()

    response = await client.post("/api/v1/plans", json={"year": 2026, "month": 9})
    assert response.status_code == 201

    plan = (
        await session.execute(select(Plan).where(Plan.user_id == owner.id))
    ).scalar_one()
    positions = (
        (
            await session.execute(
                select(PlanPosition).where(PlanPosition.plan_id == plan.id)
            )
        )
        .scalars()
        .all()
    )
    assert positions == []


async def test_a_commitment_before_its_start_date_is_left_out(
    client: AsyncClient, session: AsyncSession, owner: User
):
    await make_commitment(session, owner, "New subscription", first_due_date=date(2026, 10, 1))
    await session.commit()

    response = await client.post("/api/v1/plans", json={"year": 2026, "month": 9})
    assert response.status_code == 201

    plan = (
        await session.execute(select(Plan).where(Plan.user_id == owner.id))
    ).scalar_one()
    positions = (
        (
            await session.execute(
                select(PlanPosition).where(PlanPosition.plan_id == plan.id)
            )
        )
        .scalars()
        .all()
    )
    assert positions == []


async def test_the_copy_is_independent_of_the_commitment(
    client: AsyncClient, session: AsyncSession, owner: User
):
    """Changing the contract afterwards must not reach back into an old month.

    The snapshot pattern (see CLAUDE.md): a commitment change never rewrites a
    month that already exists.
    """
    commitment = await make_commitment(session, owner, "Rent", amount="600.00")
    await session.commit()

    response = await client.post("/api/v1/plans", json={"year": 2026, "month": 9})
    assert response.status_code == 201

    commitment.amount = Decimal("650.00")
    commitment.name = "Rent (raised)"
    await session.commit()

    plan = (
        await session.execute(select(Plan).where(Plan.user_id == owner.id))
    ).scalar_one()
    position = (
        await session.execute(select(PlanPosition).where(PlanPosition.plan_id == plan.id))
    ).scalar_one()
    assert position.label == "Rent"
    assert position.amount_planned == Decimal("600.00")


async def test_the_due_day_is_clamped_to_the_month_when_copied(
    client: AsyncClient, session: AsyncSession, owner: User
):
    """A due day of 31 becomes the position's actual last day of September: 30."""
    await make_commitment(session, owner, "Rent", due_day=31)
    await session.commit()

    response = await client.post("/api/v1/plans", json={"year": 2026, "month": 9})
    assert response.status_code == 201

    plan = (
        await session.execute(select(Plan).where(Plan.user_id == owner.id))
    ).scalar_one()
    position = (
        await session.execute(select(PlanPosition).where(PlanPosition.plan_id == plan.id))
    ).scalar_one()
    assert position.due_day == 30


async def test_creating_the_same_month_twice_is_rejected(
    client: AsyncClient, session: AsyncSession, owner: User
):
    first = await client.post("/api/v1/plans", json={"year": 2026, "month": 9})
    assert first.status_code == 201

    second = await client.post("/api/v1/plans", json={"year": 2026, "month": 9})
    assert second.status_code == 409
    assert second.json()["detail"]["code"] == "plan_already_exists"

    still_there = await session.scalar(
        select(Plan).where(Plan.user_id == owner.id, Plan.year == 2026, Plan.month == 9)
    )
    assert still_there is not None
