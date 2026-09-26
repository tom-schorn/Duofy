"""Deleting a commitment or an account until it is first used (#139).

A typo should not stay forever. A commitment is deletable while no month position
refers to it, an account while no booking touches it (on either side of a
transfer). After that only ending (commitment) or archiving (account) is left,
so the past keeps adding up.
"""

from datetime import date
from decimal import Decimal

from httpx import AsyncClient
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.enums import AccessLevel, Budget, Category
from app.models.plan import PlanPosition
from app.models.transaction import Transaction
from tests.test_delegation import (
    grant_area,
    make_commitment,
    make_plan,
    pair,  # noqa: F401
    sign_in,
)
from tests.test_transfers import make_account


async def use_commitment(session: AsyncSession, owner, commitment) -> None:
    plan = await make_plan(session, owner)
    session.add(
        PlanPosition(
            plan_id=plan.id,
            commitment_id=commitment.id,
            label="Used",
            amount_planned=Decimal("50.00"),
            category=Category.LEISURE_SUBSCRIPTIONS,
            budget=Budget.WANTS,
            due_day=1,
        )
    )
    await session.commit()


async def book(session: AsyncSession, owner, account, counter=None) -> None:
    session.add(
        Transaction(
            owner_id=owner.id,
            account_id=account.id,
            counter_account_id=counter.id if counter else None,
            occurred_on=date(2026, 9, 1),
            amount=Decimal("10.00"),
            note="x",
            category=None if counter else Category.LEISURE_SUBSCRIPTIONS,
            budget=None if counter else Budget.WANTS,
        )
    )
    await session.commit()


async def test_an_owner_deletes_their_unused_commitment(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
):
    owner, _, _ = pair
    commitment = await make_commitment(session, owner, "Typo")
    await session.commit()
    sign_in(owner)

    listed = await client.get("/api/v1/commitments")
    assert [row["deletable"] for row in listed.json() if row["name"] == "Typo"] == [True]

    response = await client.delete(f"/api/v1/commitments/{commitment.id}")
    assert response.status_code == 204


async def test_a_commitment_in_a_month_is_refused_with_its_code(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
):
    owner, _, _ = pair
    commitment = await make_commitment(session, owner, "Used")
    await session.commit()
    await use_commitment(session, owner, commitment)
    sign_in(owner)

    listed = await client.get("/api/v1/commitments")
    assert [row["deletable"] for row in listed.json() if row["name"] == "Used"] == [False]

    response = await client.delete(f"/api/v1/commitments/{commitment.id}")
    assert response.status_code == 403
    assert response.json()["detail"]["code"] == "commitment_in_use"


async def test_someone_elses_unused_commitment_needs_the_delete_right(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
):
    owner, helper, household = pair
    commitment = await make_commitment(session, owner, "Not yours")
    await grant_area(session, household, owner, "commitments", AccessLevel.EDIT)
    sign_in(helper)

    response = await client.delete(f"/api/v1/commitments/{commitment.id}")
    assert response.status_code == 403
    assert response.json()["detail"]["code"] == "no_delete_granted"


async def test_an_owner_deletes_their_account_without_bookings(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
):
    owner, _, _ = pair
    account = await make_account(session, owner, "Typo")
    await session.commit()
    sign_in(owner)

    listed = await client.get("/api/v1/accounts")
    assert [row["deletable"] for row in listed.json()] == [True]

    response = await client.delete(f"/api/v1/accounts/{account.id}")
    assert response.status_code == 204


async def test_an_account_with_a_booking_is_refused_with_its_code(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
):
    owner, _, _ = pair
    account = await make_account(session, owner, "Used")
    await book(session, owner, account)
    sign_in(owner)

    listed = await client.get("/api/v1/accounts")
    assert [row["deletable"] for row in listed.json()] == [False]

    response = await client.delete(f"/api/v1/accounts/{account.id}")
    assert response.status_code == 403
    assert response.json()["detail"]["code"] == "account_has_transactions"


async def test_the_counter_side_of_a_transfer_counts_as_a_booking(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
):
    owner, _, _ = pair
    source = await make_account(session, owner, "Source")
    target = await make_account(session, owner, "Target")
    await book(session, owner, source, counter=target)
    sign_in(owner)

    response = await client.delete(f"/api/v1/accounts/{target.id}")
    assert response.status_code == 403
    assert response.json()["detail"]["code"] == "account_has_transactions"


async def test_someone_elses_unused_account_needs_the_delete_right(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
):
    owner, helper, household = pair
    account = await make_account(session, owner, "Not yours")
    await grant_area(session, household, owner, "accounts", AccessLevel.EDIT)
    sign_in(helper)

    response = await client.delete(f"/api/v1/accounts/{account.id}")
    assert response.status_code == 403
    assert response.json()["detail"]["code"] == "no_delete_granted"
