"""The carry-over (#94): the balance an account goes into a month with.

It is a row in `transactions` of its own kind. It states a balance and moves none,
so every sum has to leave it out — the tests here pin exactly that, next to the
shape rules the database and the API hold it to.
"""

from datetime import date
from decimal import Decimal

import pytest
from httpx import AsyncClient
from sqlalchemy.exc import IntegrityError
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.enums import Budget, Category, TransactionKind
from app.models.transaction import Transaction
from tests.test_area_permissions import make_user
from tests.test_imports import sign_in
from tests.test_mark_paid import make_account


async def book(client: AsyncClient, account, day: str, amount: str, budget: str = "needs"):
    response = await client.post(
        "/api/v1/transactions",
        json={
            "accountId": str(account.id),
            "occurredOn": day,
            "amount": amount,
            "category": "household.groceries",
            "budget": budget,
        },
    )
    assert response.status_code == 201, response.text
    return response.json()


async def carry(client: AsyncClient, account, day: str, amount: str):
    return await client.post(
        "/api/v1/transactions",
        json={
            "kind": "carry_over",
            "accountId": str(account.id),
            "occurredOn": day,
            "amount": amount,
        },
    )


async def balance(client: AsyncClient, account) -> Decimal:
    rows = (await client.get("/api/v1/accounts")).json()
    return Decimal(next(row["balance"] for row in rows if row["id"] == str(account.id)))


@pytest.fixture
async def setup(client: AsyncClient, session: AsyncSession):
    owner = await make_user(session, "Owner")
    account = await make_account(session, owner, "Giro", is_default=True)
    await session.commit()
    sign_in(owner)
    return owner, account


async def test_a_carry_over_may_be_negative_but_a_booking_may_not(client, setup):
    _, account = setup

    response = await carry(client, account, "2026-10-01", "-120.50")
    assert response.status_code == 201
    assert response.json()["kind"] == "carry_over"
    assert Decimal(response.json()["amount"]) == Decimal("-120.50")

    booking = await client.post(
        "/api/v1/transactions",
        json={
            "accountId": str(account.id),
            "occurredOn": "2026-10-02",
            "amount": "-5.00",
            "category": "household.groceries",
            "budget": "needs",
        },
    )
    assert booking.status_code == 422


async def test_a_second_carry_over_for_the_same_account_and_month_is_refused(client, setup):
    _, account = setup
    assert (await carry(client, account, "2026-10-01", "100.00")).status_code == 201

    again = await carry(client, account, "2026-10-01", "200.00")

    assert again.status_code == 409
    assert again.json()["detail"]["code"] == "carry_over_exists"
    # Another month is another carry-over.
    assert (await carry(client, account, "2026-11-01", "200.00")).status_code == 201


async def test_a_carry_over_not_on_the_first_is_refused(client, setup):
    _, account = setup

    response = await carry(client, account, "2026-10-15", "100.00")

    assert response.status_code == 422
    assert "carry_over_needs_first_of_month" in response.text


async def test_a_carry_over_takes_no_purpose_no_position_and_no_counter_account(client, setup):
    _, account = setup

    response = await client.post(
        "/api/v1/transactions",
        json={
            "kind": "carry_over",
            "accountId": str(account.id),
            "occurredOn": "2026-10-01",
            "amount": "100.00",
            "category": "household.groceries",
            "budget": "needs",
        },
    )

    assert response.status_code == 422
    assert "carry_over_is_bare" in response.text


async def test_the_database_refuses_two_carry_overs_on_one_day(session: AsyncSession, setup):
    owner, account = setup
    for _ in range(2):
        session.add(
            Transaction(
                owner_id=owner.id,
                account_id=account.id,
                kind=TransactionKind.CARRY_OVER,
                occurred_on=date(2026, 10, 1),
                amount=Decimal("10.00"),
            )
        )
    with pytest.raises(IntegrityError):
        await session.flush()
    await session.rollback()


async def test_the_database_refuses_a_carry_over_with_a_purpose(session: AsyncSession, setup):
    owner, account = setup
    session.add(
        Transaction(
            owner_id=owner.id,
            account_id=account.id,
            kind=TransactionKind.CARRY_OVER,
            occurred_on=date(2026, 10, 1),
            amount=Decimal("10.00"),
            category=Category("household.groceries"),
            budget=Budget.NEEDS,
        )
    )
    with pytest.raises(IntegrityError):
        await session.flush()
    await session.rollback()


async def test_the_balance_is_the_same_with_and_without_a_carry_over(client, setup):
    _, account = setup
    await book(client, account, "2026-09-10", "40.00")
    before = await balance(client, account)

    assert (await carry(client, account, "2026-10-01", "5000.00")).status_code == 201

    assert await balance(client, account) == before == Decimal("-40.00")


async def test_the_history_leaves_a_carry_over_out(client, setup):
    _, account = setup
    await book(client, account, "2026-10-05", "40.00")
    plain = (await client.get("/api/v1/accounts/history?year=2026&month=10")).json()

    await carry(client, account, "2026-10-01", "5000.00")
    with_carry = (await client.get("/api/v1/accounts/history?year=2026&month=10")).json()

    assert with_carry == plain


async def test_a_carry_over_is_listed_but_belongs_to_no_position(client, setup):
    _, account = setup
    await carry(client, account, "2026-10-01", "5000.00")

    listed = (await client.get("/api/v1/transactions?year=2026&month=10")).json()

    assert [row["kind"] for row in listed] == ["carry_over"]
    assert listed[0]["positionId"] is None


async def test_a_carry_over_can_be_changed_and_deleted(client, setup):
    _, account = setup
    created = (await carry(client, account, "2026-10-01", "100.00")).json()

    changed = await client.patch(
        f"/api/v1/transactions/{created['id']}", json={"amount": "-30.00"}
    )
    assert changed.status_code == 200
    assert Decimal(changed.json()["amount"]) == Decimal("-30.00")

    moved = await client.patch(
        f"/api/v1/transactions/{created['id']}", json={"occurredOn": "2026-10-09"}
    )
    assert moved.status_code == 422

    assert (await client.delete(f"/api/v1/transactions/{created['id']}")).status_code == 204


async def test_the_suggestion_is_the_book_balance_at_the_end_of_the_month_before(client, setup):
    _, account = setup
    await client.patch(f"/api/v1/accounts/{account.id}", json={"openingBalance": "1000.00"})
    await book(client, account, "2026-09-10", "40.00")
    await book(client, account, "2026-09-30", "10.00")
    # From the 1st on: does not count.
    await book(client, account, "2026-10-01", "999.00")
    # A carry-over states a balance; it must not be added to the suggestion either.
    await carry(client, account, "2026-09-01", "7777.00")

    response = await client.get(
        f"/api/v1/accounts/{account.id}/carry-over-suggestion?year=2026&month=10"
    )

    assert response.status_code == 200
    assert Decimal(response.json()["amount"]) == Decimal("950.00")
