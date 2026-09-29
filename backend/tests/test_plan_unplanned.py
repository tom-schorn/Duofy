"""Bookings without a position, summed per budget in the plan (#240).

The plan used to be blind to them: a 40 EUR shop visit outside every position moved
the balance but no budget. The month now reports what was booked without a position
in it, by the **booking's own budget** (not the category's suggestion) and only in
its plan month.

Income is reported apart: it raises what is left, never the budgets. Transfers
between own accounts are not spending and stay out. A carry-over states a balance
and stays out too.
"""

from datetime import date
from decimal import Decimal

import pytest
from httpx import AsyncClient
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.permissions import Area
from app.models.account import Account
from app.models.enums import (
    AccessLevel,
    AccountType,
    Budget,
    Category,
    TransactionKind,
)
from app.models.plan import Plan, PlanPosition
from app.models.transaction import Transaction
from app.models.user import User
from app.services.grants import set_levels
from tests.test_area_permissions import add_member, make_household, make_user
from tests.test_delegation import sign_in

ZERO = Decimal("0.00")


@pytest.fixture
async def owner(session: AsyncSession) -> User:
    user = await make_user(session, "Owner")
    await session.commit()
    sign_in(user)
    return user


async def make_account(
    session: AsyncSession, owner: User, name: str = "Giro", *, is_default: bool = True
) -> Account:
    account = Account(
        owner_id=owner.id,
        name=name,
        type=AccountType.CHECKING,
        opening_balance=ZERO,
        opening_date=date(2026, 1, 1),
        is_default=is_default,
        counts_as_available=True,
    )
    session.add(account)
    await session.flush()
    return account


async def make_plan(session: AsyncSession, owner: User, month: int = 9) -> Plan:
    plan = Plan(user_id=owner.id, year=2026, month=month)
    session.add(plan)
    await session.flush()
    return plan


def book(
    owner: User,
    account: Account,
    amount: str,
    budget: Budget | None,
    *,
    month: int = 9,
    **kwargs,
) -> Transaction:
    return Transaction(
        owner_id=owner.id,
        account_id=account.id,
        occurred_on=date(2026, month, 12),
        plan_year=2026,
        plan_month=month,
        amount=Decimal(amount),
        category=kwargs.pop("category", Category.HOUSEHOLD_GROCERIES if budget else None),
        budget=budget,
        **kwargs,
    )


async def unplanned(client: AsyncClient, url: str = "/api/v1/plans/2026/9") -> dict:
    response = await client.get(url)
    assert response.status_code == 200, response.text
    body = response.json()
    assert "unplanned" in body, f"unplanned missing from the plan: {sorted(body)}"
    return {key: Decimal(value) for key, value in body["unplanned"].items()}


async def test_a_booking_without_a_position_counts_in_the_budget_it_carries(
    client: AsyncClient, session: AsyncSession, owner: User
):
    account = await make_account(session, owner)
    await make_plan(session, owner)
    session.add_all(
        [
            book(owner, account, "40.00", Budget.NEEDS),
            book(owner, account, "12.50", Budget.NEEDS),
            book(owner, account, "25.00", Budget.WANTS),
            book(owner, account, "100.00", Budget.SAVINGS),
        ]
    )
    await session.commit()

    assert await unplanned(client) == {
        "income": ZERO,
        "needs": Decimal("52.50"),
        "wants": Decimal("25.00"),
        "savings": Decimal("100.00"),
    }


async def test_the_budget_of_the_booking_wins_over_the_category_suggestion(
    client: AsyncClient, session: AsyncSession, owner: User
):
    """Groceries suggest needs; a booking that says wants is wants."""
    account = await make_account(session, owner)
    await make_plan(session, owner)
    session.add(
        book(owner, account, "30.00", Budget.WANTS, category=Category.HOUSEHOLD_GROCERIES)
    )
    await session.commit()

    figures = await unplanned(client)
    assert figures["wants"] == Decimal("30.00")
    assert figures["needs"] == ZERO


async def test_unplanned_income_is_kept_apart_from_the_budgets(
    client: AsyncClient, session: AsyncSession, owner: User
):
    account = await make_account(session, owner)
    await make_plan(session, owner)
    session.add(book(owner, account, "80.00", Budget.INCOME, category=Category.INCOME_OTHER))
    await session.commit()

    figures = await unplanned(client)
    assert figures["income"] == Decimal("80.00")
    assert figures["needs"] == figures["wants"] == figures["savings"] == ZERO


async def test_a_booking_on_a_position_is_planned_and_stays_out(
    client: AsyncClient, session: AsyncSession, owner: User
):
    account = await make_account(session, owner)
    plan = await make_plan(session, owner)
    position = PlanPosition(
        plan_id=plan.id,
        label="Groceries",
        amount_planned=Decimal("600.00"),
        category=Category.HOUSEHOLD_GROCERIES,
        budget=Budget.NEEDS,
        due_day=1,
        is_limit=True,
    )
    session.add(position)
    await session.flush()
    session.add(book(owner, account, "40.00", Budget.NEEDS, position_id=position.id))
    await session.commit()

    assert (await unplanned(client))["needs"] == ZERO


async def test_only_the_plan_month_of_the_booking_counts(
    client: AsyncClient, session: AsyncSession, owner: User
):
    """A salary dated 30 September that counts in October is October's."""
    account = await make_account(session, owner)
    await make_plan(session, owner)
    await make_plan(session, owner, month=10)
    early = book(owner, account, "500.00", Budget.INCOME, category=Category.INCOME_OTHER)
    early.occurred_on = date(2026, 9, 30)
    early.plan_month = 10
    session.add_all([early, book(owner, account, "9.00", Budget.NEEDS, month=8)])
    await session.commit()

    assert (await unplanned(client))["income"] == ZERO
    assert (await unplanned(client, "/api/v1/plans/2026/10"))["income"] == Decimal("500.00")


async def test_transfers_and_carry_overs_are_not_unplanned_spending(
    client: AsyncClient, session: AsyncSession, owner: User
):
    account = await make_account(session, owner)
    savings = await make_account(session, owner, "Tagesgeld", is_default=False)
    await make_plan(session, owner)
    session.add_all(
        [
            book(owner, account, "200.00", None, counter_account_id=savings.id),
            book(owner, account, "75.00", Budget.SAVINGS, counter_account_id=savings.id),
            Transaction(
                owner_id=owner.id,
                account_id=account.id,
                kind=TransactionKind.CARRY_OVER,
                occurred_on=date(2026, 9, 1),
                plan_year=2026,
                plan_month=9,
                amount=Decimal("300.00"),
            ),
        ]
    )
    await session.commit()

    assert set((await unplanned(client)).values()) == {ZERO}


async def test_a_month_without_bookings_reports_zeros(
    client: AsyncClient, session: AsyncSession, owner: User
):
    await make_plan(session, owner)
    await session.commit()

    assert set((await unplanned(client)).values()) == {ZERO}


async def test_another_persons_plan_shows_their_unplanned_only_with_the_book_grant(
    client: AsyncClient, session: AsyncSession, owner: User
):
    partner = await make_user(session, "Partner")
    household = await make_household(session, "Shared")
    await add_member(session, household, owner)
    await add_member(
        session, household, partner, plan=AccessLevel.VIEW, accounts=AccessLevel.NONE
    )
    account = await make_account(session, partner)
    await make_plan(session, partner)
    session.add(book(partner, account, "33.00", Budget.WANTS))
    await session.commit()
    url = f"/api/v1/plans/2026/9?owner={partner.id}"

    assert (await unplanned(client, url))["wants"] == ZERO

    await set_levels(session, partner.id, owner.id, {Area.BOOK: AccessLevel.VIEW})
    await session.commit()
    assert (await unplanned(client, url))["wants"] == Decimal("33.00")


async def test_the_household_plan_adds_up_the_unplanned_of_every_member(
    client: AsyncClient, session: AsyncSession, owner: User
):
    """Decision 48: the household sees everything of everybody, except what is on a
    private position — an unplanned booking has none, so it always counts."""
    partner = await make_user(session, "Partner")
    household = await make_household(session, "Shared")
    await add_member(session, household, owner)
    await add_member(session, household, partner, accounts=AccessLevel.NONE)
    owner_account = await make_account(session, owner)
    partner_account = await make_account(session, partner)
    await make_plan(session, owner)
    await make_plan(session, partner)
    session.add_all(
        [
            book(owner, owner_account, "20.00", Budget.NEEDS),
            book(partner, partner_account, "15.00", Budget.NEEDS),
            book(partner, partner_account, "60.00", Budget.INCOME, category=Category.INCOME_OTHER),
        ]
    )
    await session.commit()

    figures = await unplanned(client, f"/api/v1/plans/household/{household.id}/2026/9")

    assert figures["needs"] == Decimal("35.00")
    assert figures["income"] == Decimal("60.00")


async def test_a_half_household_plan_shows_no_unplanned_at_all(
    client: AsyncClient, session: AsyncSession, owner: User
):
    """A member has not planned the month yet: the household plan is not shown, so
    its sums are not either — not even the bookings of the members who did."""
    partner = await make_user(session, "Partner")
    household = await make_household(session, "Shared")
    await add_member(session, household, owner, accounts=AccessLevel.VIEW)
    await add_member(session, household, partner, accounts=AccessLevel.VIEW)
    account = await make_account(session, owner)
    await make_plan(session, owner)
    session.add(book(owner, account, "20.00", Budget.NEEDS))
    await session.commit()

    response = await client.get(f"/api/v1/plans/household/{household.id}/2026/9")

    assert response.status_code == 200
    assert response.json()["missingMembers"] == ["Partner"]
    assert set(Decimal(value) for value in response.json()["unplanned"].values()) == {ZERO}
