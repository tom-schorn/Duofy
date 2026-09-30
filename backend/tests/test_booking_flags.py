"""Three flags on every booking the book lists, computed by the server (#254).

* `unplanned`: on no position, no transfer, no carry-over — the same rule the plan
  uses for its "Ungeplant" per budget (#240)
* `planMonthFixed`: nobody can choose the plan month — it follows a position, or a
  transfer or carry-over counts in the month of its date (#239)
* `countsElsewhere`: the plan month is not the month of the date

The frontend reads them and computes nothing itself; so they have to arrive the
same in your own book, a member's book and the household book.
"""

from datetime import date
from decimal import Decimal

import pytest
from httpx import AsyncClient
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.account import Account
from app.models.enums import AccessLevel, AccountType, TransactionKind
from app.models.household import Household
from app.models.user import User
from tests.test_area_permissions import add_member, make_household, make_user
from tests.test_delegation import sign_in
from tests.test_household_book import booking, position_of

SCOPES = ["own", "member", "household"]


@pytest.fixture
async def couple(session: AsyncSession) -> tuple[User, User, Household]:
    """Two members; the partner lets the others see their book."""
    me = await make_user(session, "Me")
    partner = await make_user(session, "Partner")
    household = await make_household(session, "Shared")
    await add_member(session, household, me)
    await add_member(session, household, partner, accounts=AccessLevel.VIEW)
    await session.commit()
    sign_in(me)
    return me, partner, household


async def account_of(session: AsyncSession, user: User, name: str) -> Account:
    account = Account(
        owner_id=user.id,
        name=name,
        type=AccountType.CHECKING,
        opening_balance=Decimal("0.00"),
        opening_date=date(2026, 1, 1),
        counts_as_available=True,
    )
    session.add(account)
    await session.flush()
    return account


def owner_and_query(scope: str, couple) -> tuple[User, str]:
    """Whose bookings to write and how to read them back.

    The household book leaves another member's transfers and carry-overs out, so
    there the bookings are one's own — still read through the household.
    """
    me, partner, household = couple
    if scope == "own":
        return me, ""
    if scope == "member":
        return partner, f"&owner={partner.id}"
    return me, f"&household={household.id}"


async def flags(client: AsyncClient, query: str, month: int = 9) -> dict[str, dict]:
    response = await client.get(f"/api/v1/transactions?year=2026&month={month}{query}")
    assert response.status_code == 200, response.text
    return {
        row["note"]: {
            key: row[key] for key in ("unplanned", "planMonthFixed", "countsElsewhere")
        }
        for row in response.json()
    }


@pytest.mark.parametrize("scope", SCOPES)
async def test_the_book_says_which_booking_is_unplanned_and_whose_month_is_fixed(
    client: AsyncClient, session: AsyncSession, couple, scope: str
):
    owner, query = owner_and_query(scope, couple)
    giro = await account_of(session, owner, "Giro")
    savings = await account_of(session, owner, "Savings")
    position = await position_of(session, owner, private=False)
    session.add_all(
        [
            booking(owner, giro, "free"),
            booking(owner, giro, "on position", position_id=position.id),
            booking(
                owner,
                giro,
                "transfer",
                counter_account_id=savings.id,
                category=None,
                budget=None,
            ),
            booking(
                owner,
                giro,
                "carry over",
                kind=TransactionKind.CARRY_OVER,
                occurred_on=date(2026, 9, 1),
                category=None,
                budget=None,
            ),
        ]
    )
    await session.commit()

    book = await flags(client, query)

    assert book == {
        "free": {"unplanned": True, "planMonthFixed": False, "countsElsewhere": False},
        "on position": {
            "unplanned": False,
            "planMonthFixed": True,
            "countsElsewhere": False,
        },
        "transfer": {"unplanned": False, "planMonthFixed": True, "countsElsewhere": False},
        "carry over": {
            "unplanned": False,
            "planMonthFixed": True,
            "countsElsewhere": False,
        },
    }


@pytest.mark.parametrize("scope", SCOPES)
async def test_the_book_says_when_a_booking_counts_in_another_month_than_its_date(
    client: AsyncClient, session: AsyncSession, couple, scope: str
):
    owner, query = owner_and_query(scope, couple)
    giro = await account_of(session, owner, "Giro")
    session.add_all(
        [
            booking(owner, giro, "salary for october", occurred_on=date(2026, 9, 30),
                    plan_month=10),
            booking(owner, giro, "october itself", occurred_on=date(2026, 10, 2),
                    plan_month=10),
        ]
    )
    await session.commit()

    book = await flags(client, query, month=10)

    assert book["salary for october"]["countsElsewhere"] is True
    assert book["october itself"]["countsElsewhere"] is False


async def test_a_new_booking_answers_with_its_flags(
    client: AsyncClient, session: AsyncSession, couple
):
    me, _, _ = couple
    giro = await account_of(session, me, "Giro")
    await session.commit()

    response = await client.post(
        "/api/v1/transactions",
        json={
            "accountId": str(giro.id),
            "occurredOn": "2026-09-28",
            "amount": "12.00",
            "category": "household.groceries",
            "budget": "needs",
            "planYear": 2026,
            "planMonth": 10,
        },
    )

    assert response.status_code == 201, response.text
    body = response.json()
    assert (body["unplanned"], body["planMonthFixed"], body["countsElsewhere"]) == (
        True,
        False,
        True,
    )
