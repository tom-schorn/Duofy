"""The household book shows what the household plan shows (#241, decision 48).

Every member's bookings, except those on a private position — regardless of the
accounts grant. The household plan already works like that for positions; a book
that followed another rule would show numbers that do not add up to the plan.
"""

from datetime import date
from decimal import Decimal

import pytest
from httpx import AsyncClient
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.account import Account
from app.models.enums import AccessLevel, AccountType, Budget, Category
from app.models.household import Household
from app.models.plan import Plan, PlanPosition
from app.models.transaction import Transaction
from app.models.user import User
from tests.test_area_permissions import add_member, make_household, make_user
from tests.test_delegation import sign_in


@pytest.fixture
async def couple(session: AsyncSession) -> tuple[User, User, Household]:
    """Two members; the partner keeps their accounts to themselves (grant `plan`)."""
    me = await make_user(session, "Me")
    partner = await make_user(session, "Partner")
    household = await make_household(session, "Shared")
    await add_member(session, household, me)
    await add_member(session, household, partner, accounts=AccessLevel.PLAN)
    await session.commit()
    sign_in(me)
    return me, partner, household


async def account_of(session: AsyncSession, user: User) -> Account:
    account = Account(
        owner_id=user.id,
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


async def position_of(session: AsyncSession, user: User, *, private: bool) -> PlanPosition:
    plan = Plan(user_id=user.id, year=2026, month=9)
    session.add(plan)
    await session.flush()
    position = PlanPosition(
        plan_id=plan.id,
        label="Private thing" if private else "Rent",
        amount_planned=Decimal("100.00"),
        category=Category.HOUSING_RENT,
        budget=Budget.NEEDS,
        due_day=1,
        is_private=private,
    )
    session.add(position)
    await session.flush()
    return position


def booking(user: User, account: Account, note: str, **kwargs) -> Transaction:
    return Transaction(
        owner_id=user.id,
        account_id=account.id,
        occurred_on=date(2026, 9, 12),
        plan_year=2026,
        plan_month=9,
        amount=Decimal("10.00"),
        category=Category.HOUSEHOLD_GROCERIES,
        budget=Budget.NEEDS,
        note=note,
        **kwargs,
    )


async def notes(client: AsyncClient, household: Household) -> set[str]:
    response = await client.get(
        f"/api/v1/transactions?household={household.id}&year=2026&month=9"
    )
    assert response.status_code == 200, response.text
    return {row["note"] for row in response.json()}


async def test_the_household_book_shows_every_member_whatever_the_accounts_grant(
    client: AsyncClient, session: AsyncSession, couple
):
    me, partner, household = couple
    mine = await account_of(session, me)
    theirs = await account_of(session, partner)
    session.add_all([booking(me, mine, "mine"), booking(partner, theirs, "theirs")])
    await session.commit()

    assert await notes(client, household) == {"mine", "theirs"}


async def test_a_booking_on_a_private_position_stays_out_of_the_household_book(
    client: AsyncClient, session: AsyncSession, couple
):
    me, partner, household = couple
    theirs = await account_of(session, partner)
    private = await position_of(session, partner, private=True)
    shared = await position_of(session, me, private=False)
    session.add_all(
        [
            booking(partner, theirs, "private one", position_id=private.id),
            booking(partner, theirs, "planned shared", position_id=shared.id),
            booking(partner, theirs, "unplanned"),
        ]
    )
    await session.commit()

    assert await notes(client, household) == {"planned shared", "unplanned"}


async def test_a_private_position_does_not_hide_the_booking_from_its_owners_own_book(
    client: AsyncClient, session: AsyncSession, couple
):
    me, _partner, _household = couple
    mine = await account_of(session, me)
    private = await position_of(session, me, private=True)
    session.add(booking(me, mine, "private one", position_id=private.id))
    await session.commit()

    response = await client.get("/api/v1/transactions?year=2026&month=9")

    assert {row["note"] for row in response.json()} == {"private one"}


async def test_the_household_book_is_refused_to_an_outsider(
    client: AsyncClient, session: AsyncSession, couple
):
    _me, _partner, household = couple
    outsider = await make_user(session, "Outsider")
    await session.commit()
    sign_in(outsider)

    response = await client.get(f"/api/v1/transactions?household={household.id}")

    assert response.status_code == 403
