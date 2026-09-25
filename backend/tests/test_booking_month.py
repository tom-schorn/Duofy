"""Which month a booking belongs to (`GET /transactions?year=&month=`).

With a position, the **plan** decides the month; without one, `occurred_on` does.
Benefits paid on 31 July for August belong in August, with their real July date —
excluding rather than adding on top is what keeps a booking in exactly one month.

Part of #13.
"""

from datetime import date
from decimal import Decimal

import pytest
from httpx import AsyncClient
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.account import Account
from app.models.enums import AccountType, Block, Category
from app.models.plan import Plan, PlanPosition
from app.models.transaction import Transaction
from app.models.user import User
from tests.test_area_permissions import make_user
from tests.test_delegation import sign_in


async def make_account(session: AsyncSession, owner: User) -> Account:
    account = Account(
        owner_id=owner.id,
        name="Giro",
        type=AccountType.CHECKING,
        opening_balance=Decimal("0.00"),
        opening_date=date(2026, 1, 1),
        is_default=True,
        counts_as_available=True,
    )
    session.add(account)
    await session.flush()
    return account


async def make_position(
    session: AsyncSession, owner: User, *, year: int, month: int
) -> PlanPosition:
    plan = Plan(user_id=owner.id, year=year, month=month)
    session.add(plan)
    await session.flush()
    position = PlanPosition(
        plan_id=plan.id,
        label="Wohngeld",
        amount_planned=Decimal("450.00"),
        category=Category.INCOME_BENEFITS,
        block=Block.INCOME,
        due_day=1,
    )
    session.add(position)
    await session.flush()
    return position


@pytest.fixture
async def owner(session: AsyncSession) -> User:
    user = await make_user(session, "Owner")
    await session.commit()
    sign_in(user)
    return user


async def test_a_booking_with_a_position_follows_the_plan_month(
    client: AsyncClient, session: AsyncSession, owner: User
):
    """Benefits for August, paid on 31 July, belong to the August plan."""
    account = await make_account(session, owner)
    august_position = await make_position(session, owner, year=2026, month=8)
    session.add(
        Transaction(
            owner_id=owner.id,
            account_id=account.id,
            occurred_on=date(2026, 7, 31),
            amount=Decimal("450.00"),
            category=Category.INCOME_BENEFITS,
            block=Block.INCOME,
            position_id=august_position.id,
        )
    )
    await session.commit()

    in_august = await client.get("/api/v1/transactions?year=2026&month=8")
    assert in_august.status_code == 200
    assert len(in_august.json()) == 1

    in_july = await client.get("/api/v1/transactions?year=2026&month=7")
    assert in_july.status_code == 200
    assert in_july.json() == []


async def test_a_booking_without_a_position_follows_its_own_date(
    client: AsyncClient, session: AsyncSession, owner: User
):
    account = await make_account(session, owner)
    session.add(
        Transaction(
            owner_id=owner.id,
            account_id=account.id,
            occurred_on=date(2026, 7, 15),
            amount=Decimal("23.40"),
            category=Category.LEISURE_SUBSCRIPTIONS,
            block=Block.WANTS,
            position_id=None,
        )
    )
    await session.commit()

    in_july = await client.get("/api/v1/transactions?year=2026&month=7")
    assert in_july.status_code == 200
    assert len(in_july.json()) == 1

    in_august = await client.get("/api/v1/transactions?year=2026&month=8")
    assert in_august.status_code == 200
    assert in_august.json() == []


async def test_a_booking_never_counts_in_both_months(
    client: AsyncClient, session: AsyncSession, owner: User
):
    """The rule excludes rather than adds on top — otherwise a sum over the whole
    book would count this booking twice.
    """
    account = await make_account(session, owner)
    august_position = await make_position(session, owner, year=2026, month=8)
    session.add(
        Transaction(
            owner_id=owner.id,
            account_id=account.id,
            occurred_on=date(2026, 7, 31),
            amount=Decimal("450.00"),
            category=Category.INCOME_BENEFITS,
            block=Block.INCOME,
            position_id=august_position.id,
        )
    )
    await session.commit()

    everything = await client.get("/api/v1/transactions")
    assert everything.status_code == 200
    assert len(everything.json()) == 1
