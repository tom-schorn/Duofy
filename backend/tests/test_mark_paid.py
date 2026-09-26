"""Ticking a position off (`POST /positions/{id}/paid`) and undoing it, the
normal path, on the current model, before #83.

`mark_paid` books into the household book: the position's own account if it has
one, otherwise the owner's default account; a free date and amount override the
planned ones. `unmark_paid` removes only the booking it created itself.

Ticking without a booking (#95): no account anywhere and source equal to target
are rejected; a position that already carries bookings is ticked without a new one.

Part of #13.
"""

from datetime import date
from decimal import Decimal

import pytest
from httpx import AsyncClient
from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.account import Account
from app.models.enums import AccountType, Budget, Category
from app.models.plan import Plan, PlanPosition
from app.models.transaction import Transaction
from app.models.user import User
from tests.test_area_permissions import make_user
from tests.test_delegation import sign_in


async def make_account(
    session: AsyncSession, owner: User, name: str, *, is_default: bool = False
) -> Account:
    account = Account(
        owner_id=owner.id,
        name=name,
        type=AccountType.CHECKING,
        opening_balance=Decimal("0.00"),
        opening_date=date(2026, 1, 1),
        is_default=is_default,
        counts_as_available=True,
    )
    session.add(account)
    await session.flush()
    return account


async def make_position(
    session: AsyncSession,
    owner: User,
    *,
    account_id=None,
    amount_planned: str = "50.00",
    label: str = "Subscription",
) -> PlanPosition:
    plan = Plan(user_id=owner.id, year=2026, month=9)
    session.add(plan)
    await session.flush()
    position = PlanPosition(
        plan_id=plan.id,
        label=label,
        amount_planned=Decimal(amount_planned),
        category=Category.LEISURE_SUBSCRIPTIONS,
        budget=Budget.WANTS,
        due_day=15,
        account_id=account_id,
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


async def test_mark_paid_books_on_the_positions_own_account(
    client: AsyncClient, session: AsyncSession, owner: User
):
    own_account = await make_account(session, owner, "Extra account")
    default_account = await make_account(session, owner, "Giro", is_default=True)
    position = await make_position(session, owner, account_id=own_account.id)
    await session.commit()

    response = await client.post(f"/api/v1/positions/{position.id}/paid")
    assert response.status_code == 200

    transaction = (
        await session.execute(
            select(Transaction).where(Transaction.position_id == position.id)
        )
    ).scalar_one()
    assert transaction.account_id == own_account.id
    assert transaction.account_id != default_account.id
    assert transaction.amount == Decimal("50.00")
    assert transaction.auto_booked is True

    await session.refresh(position)
    assert position.paid_at is not None
    assert position.amount_actual == Decimal("50.00")


async def test_mark_paid_falls_back_to_the_default_account(
    client: AsyncClient, session: AsyncSession, owner: User
):
    default_account = await make_account(session, owner, "Giro", is_default=True)
    position = await make_position(session, owner, account_id=None)
    await session.commit()

    response = await client.post(f"/api/v1/positions/{position.id}/paid")
    assert response.status_code == 200

    transaction = (
        await session.execute(
            select(Transaction).where(Transaction.position_id == position.id)
        )
    ).scalar_one()
    assert transaction.account_id == default_account.id


async def test_mark_paid_uses_a_free_date_and_amount(
    client: AsyncClient, session: AsyncSession, owner: User
):
    await make_account(session, owner, "Giro", is_default=True)
    position = await make_position(session, owner, amount_planned="50.00")
    await session.commit()

    response = await client.post(
        f"/api/v1/positions/{position.id}/paid",
        json={"occurredOn": "2026-09-12", "amount": "47.30"},
    )
    assert response.status_code == 200

    transaction = (
        await session.execute(
            select(Transaction).where(Transaction.position_id == position.id)
        )
    ).scalar_one()
    assert transaction.occurred_on == date(2026, 9, 12)
    assert transaction.amount == Decimal("47.30")

    await session.refresh(position)
    assert position.amount_actual == Decimal("47.30")


async def test_mark_paid_without_a_payload_uses_today_and_the_planned_amount(
    client: AsyncClient, session: AsyncSession, owner: User
):
    await make_account(session, owner, "Giro", is_default=True)
    position = await make_position(session, owner, amount_planned="50.00")
    await session.commit()

    response = await client.post(f"/api/v1/positions/{position.id}/paid")
    assert response.status_code == 200

    transaction = (
        await session.execute(
            select(Transaction).where(Transaction.position_id == position.id)
        )
    ).scalar_one()
    assert transaction.occurred_on == date.today()
    assert transaction.amount == Decimal("50.00")


async def test_unmark_paid_removes_the_auto_booked_transaction(
    client: AsyncClient, session: AsyncSession, owner: User
):
    await make_account(session, owner, "Giro", is_default=True)
    position = await make_position(session, owner)
    await session.commit()

    marked = await client.post(f"/api/v1/positions/{position.id}/paid")
    assert marked.status_code == 200

    response = await client.delete(f"/api/v1/positions/{position.id}/paid")
    assert response.status_code == 200

    remaining = (
        (
            await session.execute(
                select(Transaction).where(Transaction.position_id == position.id)
            )
        )
        .scalars()
        .all()
    )
    assert remaining == []

    await session.refresh(position)
    assert position.paid_at is None
    assert position.amount_actual is None


async def test_unmark_paid_leaves_a_later_hand_entered_booking_alone(
    client: AsyncClient, session: AsyncSession, owner: User
):
    """The tick books once; a purchase entered by hand afterwards is a second,
    unrelated transaction. Un-ticking must remove only the one it created.

    Booking the manual entry only *after* `mark_paid` matters: a position that
    already carries a booking when it is ticked is one of #95's silent cases and
    out of scope here — `mark_paid` would not book at all in that case.
    """
    giro = await make_account(session, owner, "Giro", is_default=True)
    position = await make_position(session, owner, account_id=giro.id, amount_planned="50.00")
    await session.commit()

    marked = await client.post(f"/api/v1/positions/{position.id}/paid")
    assert marked.status_code == 200

    manual = Transaction(
        owner_id=owner.id,
        account_id=giro.id,
        occurred_on=date(2026, 9, 20),
        amount=Decimal("23.40"),
        category=Category.LEISURE_SUBSCRIPTIONS,
        budget=Budget.WANTS,
        position_id=position.id,
        auto_booked=False,
    )
    session.add(manual)
    await session.commit()

    response = await client.delete(f"/api/v1/positions/{position.id}/paid")
    assert response.status_code == 200

    remaining = (
        (
            await session.execute(
                select(Transaction).where(Transaction.position_id == position.id)
            )
        )
        .scalars()
        .all()
    )
    assert [transaction.id for transaction in remaining] == [manual.id]

    await session.refresh(position)
    assert position.paid_at is None
    assert position.amount_actual == Decimal("23.40")


async def test_mark_paid_rejects_a_position_whose_source_equals_its_target(
    client: AsyncClient, session: AsyncSession, owner: User
):
    giro = await make_account(session, owner, "Giro", is_default=True)
    position = await make_position(session, owner, account_id=giro.id)
    position.counter_account_id = giro.id
    await session.commit()

    response = await client.post(f"/api/v1/positions/{position.id}/paid")

    assert response.status_code == 422
    assert response.json()["detail"] == {"code": "position_source_equals_target"}
    await session.refresh(position)
    assert position.paid_at is None
    assert position.amount_actual is None
    assert (
        await session.scalar(
            select(func.count()).select_from(Transaction).where(
                Transaction.position_id == position.id
            )
        )
    ) == 0


async def test_mark_paid_rejects_a_position_with_no_account_and_no_default(
    client: AsyncClient, session: AsyncSession, owner: User
):
    position = await make_position(session, owner)
    await session.commit()

    response = await client.post(f"/api/v1/positions/{position.id}/paid")

    assert response.status_code == 422
    assert response.json()["detail"] == {"code": "position_no_account"}
    await session.refresh(position)
    assert position.paid_at is None
    assert position.amount_actual is None
    assert (
        await session.scalar(select(func.count()).select_from(Transaction))
    ) == 0


async def test_mark_paid_rejects_a_transfer_from_the_default_account_onto_itself(
    client: AsyncClient, session: AsyncSession, owner: User
):
    """No account on the position: the default account is the source, and it is
    also the counter account — case 1 as the issue states it."""
    giro = await make_account(session, owner, "Giro", is_default=True)
    position = await make_position(session, owner)
    position.counter_account_id = giro.id
    await session.commit()

    response = await client.post(f"/api/v1/positions/{position.id}/paid")

    assert response.status_code == 422
    assert response.json()["detail"] == {"code": "position_source_equals_target"}
    await session.refresh(position)
    assert position.paid_at is None
    assert (
        await session.scalar(select(func.count()).select_from(Transaction))
    ) == 0


async def test_mark_paid_on_a_position_with_bookings_ticks_without_a_new_booking(
    client: AsyncClient, session: AsyncSession, owner: User
):
    giro = await make_account(session, owner, "Giro", is_default=True)
    position = await make_position(session, owner, account_id=giro.id)
    existing = Transaction(
        owner_id=owner.id,
        account_id=giro.id,
        occurred_on=date(2026, 9, 3),
        amount=Decimal("12.00"),
        category=Category.LEISURE_SUBSCRIPTIONS,
        budget=Budget.WANTS,
        position_id=position.id,
        auto_booked=False,
    )
    session.add(existing)
    await session.commit()

    response = await client.post(
        f"/api/v1/positions/{position.id}/paid",
        json={"occurred_on": "2026-09-10", "amount": "99.00"},
    )

    assert response.status_code == 200
    bookings = (
        (
            await session.execute(
                select(Transaction).where(Transaction.position_id == position.id)
            )
        )
        .scalars()
        .all()
    )
    assert [transaction.id for transaction in bookings] == [existing.id]
    await session.refresh(position)
    assert position.paid_at is not None
