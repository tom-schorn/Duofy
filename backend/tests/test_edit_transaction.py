"""Changing a booking (`PATCH /transactions/{id}`, #144).

A booking made by ticking a position off stays tied to that position: changing its
amount or date must keep the position's actual amount and the account balance
right, and it cannot be detached from the position it was made for. Shapes the
database would refuse (a transfer to the same account, a booking without a
purpose) are answered with a code, not with a server error.
"""

from datetime import date
from decimal import Decimal

from httpx import AsyncClient
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.enums import AccessLevel, Budget, Category
from app.models.transaction import Transaction
from tests.test_delegation import grant_area, pair, sign_in  # noqa: F401
from tests.test_mark_paid import make_account, make_position


async def tick(client: AsyncClient, session: AsyncSession, owner, **kwargs):
    """A position ticked off: returns it and the booking the tick made."""
    account = await make_account(session, owner, "Giro", is_default=True)
    position = await make_position(session, owner, account_id=account.id, **kwargs)
    await session.commit()
    sign_in(owner)
    assert (await client.post(f"/api/v1/positions/{position.id}/paid")).status_code == 200
    booking = (
        await session.execute(select(Transaction).where(Transaction.position_id == position.id))
    ).scalar_one()
    return position, booking, account


async def balance(client: AsyncClient, account) -> Decimal:
    rows = (await client.get("/api/v1/accounts")).json()
    return Decimal(next(row["balance"] for row in rows if row["id"] == str(account.id)))


async def test_changing_the_amount_of_a_ticked_booking_keeps_plan_and_balance_right(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
):
    owner, _, _ = pair
    position, booking, account = await tick(client, session, owner)
    before = await balance(client, account)

    response = await client.patch(f"/api/v1/transactions/{booking.id}", json={"amount": "80.00"})
    assert response.status_code == 200

    await session.refresh(position)
    assert position.amount_actual == Decimal("80.00")
    assert position.paid_at is not None
    assert await balance(client, account) == before - Decimal("30.00")


async def test_changing_the_date_of_a_ticked_booking_keeps_its_position(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
):
    owner, _, _ = pair
    position, booking, _ = await tick(client, session, owner)

    response = await client.patch(
        f"/api/v1/transactions/{booking.id}", json={"occurredOn": "2026-09-03"}
    )
    assert response.status_code == 200
    assert response.json()["positionId"] == str(position.id)
    assert response.json()["occurredOn"] == "2026-09-03"


async def test_a_ticked_booking_cannot_be_moved_off_its_position(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
):
    owner, _, _ = pair
    position, booking, _ = await tick(client, session, owner)

    response = await client.patch(f"/api/v1/transactions/{booking.id}", json={"positionId": None})
    assert response.status_code == 409
    assert response.json()["detail"]["code"] == "auto_booking_keeps_position"


async def test_a_member_with_the_edit_right_changes_a_booking(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
):
    owner, helper, household = pair
    _, booking, _ = await tick(client, session, owner)
    await grant_area(session, household, owner, "accounts", AccessLevel.EDIT)
    sign_in(helper)

    response = await client.patch(f"/api/v1/transactions/{booking.id}", json={"note": "Fixed"})
    assert response.status_code == 200
    assert response.json()["note"] == "Fixed"


async def test_a_member_without_the_edit_right_cannot_change_a_booking(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
):
    owner, helper, household = pair
    _, booking, _ = await tick(client, session, owner)
    await grant_area(session, household, owner, "accounts", AccessLevel.VIEW)
    sign_in(helper)

    response = await client.patch(f"/api/v1/transactions/{booking.id}", json={"note": "Nope"})
    assert response.status_code == 403
    assert response.json()["detail"]["code"] == "no_edit_granted"


async def test_taking_the_transfer_away_without_a_purpose_is_refused_with_a_code(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
):
    owner, _, _ = pair
    giro = await make_account(session, owner, "Giro")
    savings = await make_account(session, owner, "Savings")
    transfer = Transaction(
        owner_id=owner.id,
        account_id=giro.id,
        counter_account_id=savings.id,
        occurred_on=date(2026, 9, 1),
        amount=Decimal("10.00"),
    )
    session.add(transfer)
    await session.commit()
    sign_in(owner)

    response = await client.patch(
        f"/api/v1/transactions/{transfer.id}", json={"counterAccountId": None}
    )
    assert response.status_code == 422
    assert response.json()["detail"]["code"] == "purpose_required"


async def test_pointing_a_transfer_at_its_own_account_is_refused_with_a_code(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
):
    owner, _, _ = pair
    giro = await make_account(session, owner, "Giro")
    savings = await make_account(session, owner, "Savings")
    transfer = Transaction(
        owner_id=owner.id,
        account_id=giro.id,
        counter_account_id=savings.id,
        occurred_on=date(2026, 9, 1),
        amount=Decimal("10.00"),
    )
    session.add(transfer)
    await session.commit()
    sign_in(owner)

    response = await client.patch(
        f"/api/v1/transactions/{transfer.id}", json={"counterAccountId": str(giro.id)}
    )
    assert response.status_code == 422
    assert response.json()["detail"]["code"] == "transfer_needs_two_accounts"


async def test_an_explicit_null_on_a_required_field_is_a_422_not_a_500(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
):
    owner, _, _ = pair
    _, booking, _ = await tick(client, session, owner)

    for field in ("amount", "occurredOn", "accountId"):
        response = await client.patch(f"/api/v1/transactions/{booking.id}", json={field: None})
        assert response.status_code == 422, field
        assert response.json()["detail"]["code"] == "null_not_allowed"


async def test_a_plain_booking_changes_its_purpose(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
):
    owner, _, _ = pair
    account = await make_account(session, owner, "Giro")
    plain = Transaction(
        owner_id=owner.id,
        account_id=account.id,
        occurred_on=date(2026, 9, 1),
        amount=Decimal("10.00"),
        category=Category.LEISURE_SUBSCRIPTIONS,
        budget=Budget.WANTS,
    )
    session.add(plain)
    await session.commit()
    sign_in(owner)

    response = await client.patch(
        f"/api/v1/transactions/{plain.id}",
        json={"category": "household.groceries", "budget": "needs"},
    )
    assert response.status_code == 200
    assert response.json()["budget"] == "needs"
