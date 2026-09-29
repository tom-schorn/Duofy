"""The plan month of a booking (#239): the month it counts in, chosen or derived.

* with a position, the plan of the position decides — ticking off, an import and
  re-assigning move the booking with it
* without a position, previous / own / next month relative to the date, the date's
  month by default
* a carry-over and a pure transfer count in the month of their date, fixed
* sums over a month (book listing, flow) read the plan month, not the date
"""

from datetime import date
from decimal import Decimal

import pytest
from httpx import AsyncClient
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.enums import Budget, Category
from app.models.plan import Plan
from app.models.transaction import Transaction
from app.models.user import User
from tests.test_area_permissions import make_user
from tests.test_booking_month import make_position as make_position_in
from tests.test_delegation import sign_in
from tests.test_mark_paid import make_account, make_position

URL = "/api/v1/transactions"


@pytest.fixture
async def owner(session: AsyncSession) -> User:
    user = await make_user(session, "Owner")
    await session.commit()
    sign_in(user)
    return user


def payload(account, **extra) -> dict:
    return {
        "accountId": str(account.id),
        "occurredOn": "2026-09-25",
        "amount": "40.00",
        "category": Category.LEISURE_SUBSCRIPTIONS.value,
        "budget": Budget.WANTS.value,
        **extra,
    }


async def account_of(session: AsyncSession, owner: User, name: str = "Giro", **kwargs):
    account = await make_account(session, owner, name, **kwargs)
    await session.commit()
    return account


async def test_a_booking_without_a_position_counts_in_the_month_of_its_date_by_default(
    client: AsyncClient, session: AsyncSession, owner: User
):
    account = await account_of(session, owner, is_default=True)

    response = await client.post(URL, json=payload(account))

    assert response.status_code == 201
    assert (response.json()["planYear"], response.json()["planMonth"]) == (2026, 9)


@pytest.mark.parametrize(
    ("chosen", "expected"),
    [((2026, 8), (2026, 8)), ((2026, 9), (2026, 9)), ((2026, 10), (2026, 10))],
)
async def test_a_booking_without_a_position_can_count_in_the_previous_or_next_month(
    client: AsyncClient, session: AsyncSession, owner: User, chosen, expected
):
    account = await account_of(session, owner, is_default=True)

    response = await client.post(
        URL, json=payload(account, planYear=chosen[0], planMonth=chosen[1])
    )

    assert response.status_code == 201
    assert (response.json()["planYear"], response.json()["planMonth"]) == expected


async def test_the_choice_crosses_the_turn_of_the_year(
    client: AsyncClient, session: AsyncSession, owner: User
):
    """A salary paid on 29 December for January."""
    account = await account_of(session, owner, is_default=True)

    response = await client.post(
        URL, json=payload(account, occurredOn="2026-12-29", planYear=2027, planMonth=1)
    )

    assert response.status_code == 201
    assert (response.json()["planYear"], response.json()["planMonth"]) == (2027, 1)


async def test_a_month_two_away_from_the_date_is_refused(
    client: AsyncClient, session: AsyncSession, owner: User
):
    account = await account_of(session, owner, is_default=True)

    response = await client.post(URL, json=payload(account, planYear=2026, planMonth=11))

    assert response.status_code == 422
    assert response.json()["detail"]["code"] == "plan_month_out_of_range"


async def test_year_and_month_come_together_or_not_at_all(
    client: AsyncClient, session: AsyncSession, owner: User
):
    account = await account_of(session, owner, is_default=True)

    response = await client.post(URL, json=payload(account, planMonth=10))

    assert response.status_code == 422


async def test_a_booking_counts_in_the_plan_of_its_position_whatever_its_date(
    client: AsyncClient, session: AsyncSession, owner: User
):
    """Salary paid on 25 September for the October plan."""
    account = await account_of(session, owner, is_default=True)
    position = await make_position_in(session, owner, year=2026, month=10)
    await session.commit()

    response = await client.post(URL, json=payload(account, positionId=str(position.id)))

    assert response.status_code == 201
    assert (response.json()["planYear"], response.json()["planMonth"]) == (2026, 10)


async def test_a_month_chosen_next_to_a_position_is_refused(
    client: AsyncClient, session: AsyncSession, owner: User
):
    account = await account_of(session, owner, is_default=True)
    position = await make_position_in(session, owner, year=2026, month=10)
    await session.commit()

    response = await client.post(
        URL, json=payload(account, positionId=str(position.id), planYear=2026, planMonth=9)
    )

    assert response.status_code == 422
    assert response.json()["detail"]["code"] == "plan_month_follows_position"


async def test_a_carry_over_counts_in_the_month_of_its_date(
    client: AsyncClient, session: AsyncSession, owner: User
):
    account = await account_of(session, owner, is_default=True)

    response = await client.post(
        URL,
        json={
            "accountId": str(account.id),
            "kind": "carry_over",
            "occurredOn": "2026-10-01",
            "amount": "120.00",
        },
    )

    assert response.status_code == 201
    assert (response.json()["planYear"], response.json()["planMonth"]) == (2026, 10)


async def test_a_pure_transfer_counts_in_the_month_of_its_date_and_cannot_be_moved(
    client: AsyncClient, session: AsyncSession, owner: User
):
    giro = await account_of(session, owner, is_default=True)
    savings = await account_of(session, owner, "Sparen")
    transfer = {
        "accountId": str(giro.id),
        "counterAccountId": str(savings.id),
        "occurredOn": "2026-09-30",
        "amount": "50.00",
    }

    created = await client.post(URL, json=transfer)
    assert created.status_code == 201
    assert (created.json()["planYear"], created.json()["planMonth"]) == (2026, 9)

    moved = await client.post(URL, json={**transfer, "planYear": 2026, "planMonth": 10})
    assert moved.status_code == 422
    assert moved.json()["detail"]["code"] == "plan_month_fixed"


async def test_ticking_a_position_off_counts_in_its_plan_even_with_an_earlier_date(
    client: AsyncClient, session: AsyncSession, owner: User
):
    account = await account_of(session, owner, is_default=True)
    position = await make_position(session, owner, account_id=account.id)  # plan 2026-09
    await session.commit()

    response = await client.post(
        f"/api/v1/positions/{position.id}/paid", json={"occurredOn": "2026-08-30"}
    )

    assert response.status_code == 200
    booking = (await session.execute(select(Transaction))).scalar_one()
    assert (booking.plan_year, booking.plan_month) == (2026, 9)


async def test_booking_an_import_assigned_to_a_position_counts_in_its_plan(
    client: AsyncClient, session: AsyncSession, owner: User
):
    from app.models.imported_entry import ImportedEntry

    account = await account_of(session, owner, is_default=True)
    position = await make_position_in(session, owner, year=2026, month=10)
    entry = ImportedEntry(
        owner_id=owner.id,
        imported_by_id=owner.id,
        account_id=account.id,
        occurred_on=date(2026, 9, 25),
        value_on=date(2026, 9, 25),
        amount=Decimal("450.00"),
        incoming=True,
        purpose="Lohn",
        category=Category.INCOME_BENEFITS,
        budget=Budget.INCOME,
        position_id=position.id,
        external_ref="ref-1",
    )
    session.add(entry)
    await session.commit()

    assert (await client.post(f"/api/v1/imports/{entry.id}/book")).status_code == 200

    booking = (await session.execute(select(Transaction))).scalar_one()
    assert (booking.plan_year, booking.plan_month) == (2026, 10)


async def test_assigning_a_booking_to_a_position_moves_it_into_that_plan(
    client: AsyncClient, session: AsyncSession, owner: User
):
    account = await account_of(session, owner, is_default=True)
    position = await make_position_in(session, owner, year=2026, month=10)
    await session.commit()
    created = await client.post(URL, json=payload(account))
    assert created.json()["planMonth"] == 9

    response = await client.patch(
        f"{URL}/{created.json()['id']}", json={"positionId": str(position.id)}
    )

    assert response.status_code == 200
    assert (response.json()["planYear"], response.json()["planMonth"]) == (2026, 10)


async def test_taking_a_booking_off_its_position_keeps_it_in_the_month_it_counted_in(
    client: AsyncClient, session: AsyncSession, owner: User
):
    account = await account_of(session, owner, is_default=True)
    position = await make_position_in(session, owner, year=2026, month=10)
    await session.commit()
    created = await client.post(URL, json=payload(account, positionId=str(position.id)))

    response = await client.patch(f"{URL}/{created.json()['id']}", json={"positionId": None})

    assert response.status_code == 200
    assert response.json()["planMonth"] == 10


async def test_taking_a_booking_off_a_position_far_from_its_date_falls_back_to_the_date(
    client: AsyncClient, session: AsyncSession, owner: User
):
    """The position's plan may be more than a month away from the date — outside
    what can be chosen without a position, so the date's month is used."""
    account = await account_of(session, owner, is_default=True)
    position = await make_position_in(session, owner, year=2026, month=12)
    await session.commit()
    created = await client.post(URL, json=payload(account, positionId=str(position.id)))
    assert created.json()["planMonth"] == 12

    response = await client.patch(f"{URL}/{created.json()['id']}", json={"positionId": None})

    assert response.status_code == 200
    assert response.json()["planMonth"] == 9


async def test_a_booking_without_a_position_can_be_moved_to_another_month(
    client: AsyncClient, session: AsyncSession, owner: User
):
    account = await account_of(session, owner, is_default=True)
    created = await client.post(URL, json=payload(account))

    response = await client.patch(
        f"{URL}/{created.json()['id']}", json={"planYear": 2026, "planMonth": 8}
    )

    assert response.status_code == 200
    assert response.json()["planMonth"] == 8


async def test_changing_the_date_takes_the_default_plan_month_along_but_keeps_a_choice(
    client: AsyncClient, session: AsyncSession, owner: User
):
    account = await account_of(session, owner, is_default=True)
    by_default = (await client.post(URL, json=payload(account))).json()
    chosen = (
        await client.post(URL, json=payload(account, planYear=2026, planMonth=10))
    ).json()

    moved_default = await client.patch(
        f"{URL}/{by_default['id']}", json={"occurredOn": "2026-10-02"}
    )
    moved_chosen = await client.patch(f"{URL}/{chosen['id']}", json={"occurredOn": "2026-10-02"})

    assert moved_default.json()["planMonth"] == 10  # own month before, own month now
    assert moved_chosen.json()["planMonth"] == 11  # "next month" before, "next month" now


async def test_the_book_lists_by_plan_month_not_by_date(
    client: AsyncClient, session: AsyncSession, owner: User
):
    account = await account_of(session, owner, is_default=True)
    await client.post(URL, json=payload(account, planYear=2026, planMonth=10))
    await client.post(URL, json=payload(account, occurredOn="2026-09-10"))

    september = (await client.get(f"{URL}?year=2026&month=9")).json()
    october = (await client.get(f"{URL}?year=2026&month=10")).json()

    assert [t["occurredOn"] for t in september] == ["2026-09-10"]
    assert [t["occurredOn"] for t in october] == ["2026-09-25"]


async def test_the_book_lists_a_booking_for_a_month_that_has_no_plan_yet(
    client: AsyncClient, session: AsyncSession, owner: User
):
    account = await account_of(session, owner, is_default=True)
    await client.post(URL, json=payload(account, planYear=2026, planMonth=10))

    october = await client.get(f"{URL}?year=2026&month=10")

    assert len(october.json()) == 1


async def test_the_flow_counts_an_unplanned_booking_in_its_plan_month(
    client: AsyncClient, session: AsyncSession, owner: User
):
    account = await account_of(session, owner, is_default=True)
    session.add_all([Plan(user_id=owner.id, year=2026, month=m) for m in (9, 10)])
    await session.commit()
    await client.post(URL, json=payload(account, planYear=2026, planMonth=10))

    september = await client.get("/api/v1/plans/2026/9/flow")
    october = await client.get("/api/v1/plans/2026/10/flow")

    assert september.status_code == 200, september.text
    assert september.json()["entries"] == []
    assert [e["amount"] for e in october.json()["entries"]] == ["-40.00"]
