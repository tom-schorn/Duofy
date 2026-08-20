"""Lists must not reshuffle when one of their rows is edited.

Postgres may return equally-ranked rows in any order, and an updated row
typically comes back last. Everything written in one transaction shares a
`created_at` to the microsecond — `now()` is the transaction's start time — so a
generated month and an imported batch both produce rows that no sort key can
tell apart unless one is unique.

The symptom is the same everywhere: assign a category, and the entries jump.
"""

from datetime import date
from decimal import Decimal

from httpx import AsyncClient
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.auth import current_active_user
from app.main import app
from app.models.account import Account
from app.models.enums import AccountType, Block, Category
from app.models.plan import Plan, PlanPosition
from app.models.transaction import Transaction
from app.models.user import User
from tests.test_area_permissions import make_user


def sign_in(user: User) -> None:
    app.dependency_overrides[current_active_user] = lambda: user


async def test_positions_keep_their_order_when_one_changes(
    client: AsyncClient, session: AsyncSession
):
    """Six positions due on the same day — only the id can separate them."""
    user = await make_user(session, "Owner")
    plan = Plan(user_id=user.id, year=2026, month=8)
    session.add(plan)
    await session.flush()

    for number in range(6):
        session.add(
            PlanPosition(
                plan_id=plan.id,
                label=f"Posten {number}",
                amount_planned=Decimal("10.00") + number,
                category=Category.HOUSEHOLD_GROCERIES,
                block=Block.NEEDS,
                due_day=15,
            )
        )
    await session.commit()
    sign_in(user)

    async def order() -> list[str]:
        # Ohne das kämen die Objekte aus der Identity Map der Session und stünden
        # dort in Einfügereihenfolge — der Test würde die Datenbank gar nicht
        # fragen und den Fehler nie sehen.
        session.expunge_all()
        rows = (await client.get("/api/v1/plans/2026/8")).json()["positions"]
        return [row["id"] for row in rows]

    before = await order()
    assert len(before) == 6

    await client.patch(f"/api/v1/positions/{before[3]}", json={"label": "Geändert"})

    assert await order() == before

    app.dependency_overrides.clear()


async def test_bookings_keep_their_order_when_one_changes(
    client: AsyncClient, session: AsyncSession
):
    """Six bookings on the same day, written in one transaction — as an import does."""
    user = await make_user(session, "Owner")
    account = Account(
        owner_id=user.id,
        name="Giro",
        type=AccountType.CHECKING,
        opening_balance=Decimal("0.00"),
        opening_date=date(2026, 1, 1),
    )
    session.add(account)
    await session.flush()

    for number in range(6):
        session.add(
            Transaction(
                owner_id=user.id,
                account_id=account.id,
                occurred_on=date(2026, 8, 14),
                amount=Decimal("10.00") + number,
                category=Category.HOUSEHOLD_GROCERIES,
                block=Block.NEEDS,
            )
        )
    await session.commit()
    sign_in(user)

    async def order() -> list[str]:
        session.expunge_all()
        rows = (await client.get("/api/v1/transactions?year=2026&month=8")).json()
        return [row["id"] for row in rows]

    before = await order()
    assert len(before) == 6

    await client.patch(f"/api/v1/transactions/{before[3]}", json={"note": "Geändert"})

    assert await order() == before

    app.dependency_overrides.clear()
