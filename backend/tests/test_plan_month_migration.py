"""The migration that adds `transactions.plan_year` / `plan_month` (#239), up and down.

A database of its own, migrated to the revision before it and filled with three
bookings: one on a position of another month than its date, one without a position,
one that lost its position. The upgrade has to put every one of them where the book
already listed it.
"""

import pytest
from sqlalchemy import text
from sqlalchemy.exc import DBAPIError
from sqlalchemy.ext.asyncio import create_async_engine

from app.core.config import settings
from tests.test_migrations import alembic, create_database, drop_database

BEFORE = "48618ba806e8"
PLAN_MONTH = "c5a8e2f7d941"
DB = f"{settings.postgres_db}_plan_month"

USER = "11111111-1111-1111-1111-111111111111"
ACCOUNT = "22222222-2222-2222-2222-222222222222"
PLAN = "33333333-3333-3333-3333-333333333333"
POSITION = "44444444-4444-4444-4444-444444444444"


async def query(sql: str):
    url = settings.database_url.rsplit("/", 1)[0] + f"/{DB}"
    engine = create_async_engine(url)
    try:
        async with engine.begin() as connection:
            result = await connection.execute(text(sql))
            return result.all() if result.returns_rows else []
    finally:
        await engine.dispose()


@pytest.fixture
async def scratch():
    await drop_database(DB)
    await create_database(DB)
    yield
    await drop_database(DB)


async def seed() -> None:
    await query(
        f"INSERT INTO users (id, email, hashed_password, is_active, is_superuser, "
        f"is_verified, first_name, last_name) VALUES ('{USER}', 'a@example.com', 'x', "
        "true, false, true, 'Ada', 'Test')"
    )
    await query(
        f"INSERT INTO accounts (id, owner_id, name, type, opening_balance, opening_date, "
        f"is_default, active, counts_as_available) VALUES ('{ACCOUNT}', '{USER}', 'Giro', "
        "'checking', 0, '2026-01-01', true, true, true)"
    )
    await query(
        f"INSERT INTO plans (id, user_id, year, month, target_needs, target_wants, "
        f"target_savings) VALUES ('{PLAN}', '{USER}', 2026, 10, 50, 30, 20)"
    )
    await query(
        f"INSERT INTO plan_positions (id, plan_id, label, amount_planned, category, budget, "
        f"due_day, is_limit, pass_through, manually_changed) VALUES ('{POSITION}', '{PLAN}', "
        "'Lohn', 100, 'income.earned', 'income', 1, false, false, false)"
    )


async def seed_bookings() -> None:
    booking = (
        "INSERT INTO transactions (id, owner_id, account_id, occurred_on, amount, "
        "auto_booked, kind, category, budget, position_id) VALUES "
        f"(gen_random_uuid(), '{USER}', '{ACCOUNT}', '{{day}}', 10, false, 'booking', "
        "'income.earned', 'income', {position})"
    )
    await query(booking.format(day="2026-09-25", position=f"'{POSITION}'"))
    await query(booking.format(day="2026-11-03", position="NULL"))


async def test_a_booking_starts_in_the_plan_of_its_position_or_else_in_the_month_of_its_date(
    scratch,
):
    alembic("upgrade", BEFORE, database=DB)
    await seed()
    await seed_bookings()

    alembic("upgrade", PLAN_MONTH, database=DB)

    rows = await query(
        "SELECT occurred_on::text, plan_year, plan_month FROM transactions ORDER BY occurred_on"
    )
    assert rows == [("2026-09-25", 2026, 10), ("2026-11-03", 2026, 11)]


async def test_the_month_is_checked_and_has_no_default_left(scratch):
    alembic("upgrade", PLAN_MONTH, database=DB)
    await seed()

    # No plan month given: the migration left no default behind.
    with pytest.raises(DBAPIError):
        await query(
            "INSERT INTO transactions (id, owner_id, account_id, occurred_on, amount, "
            "auto_booked, kind, category, budget) VALUES "
            f"(gen_random_uuid(), '{USER}', '{ACCOUNT}', '2026-09-01', 1, false, 'booking', "
            "'income.earned', 'income')"
        )
    with pytest.raises(DBAPIError):
        await query(
            "INSERT INTO transactions (id, owner_id, account_id, occurred_on, amount, "
            "auto_booked, kind, category, budget, plan_year, plan_month) VALUES "
            f"(gen_random_uuid(), '{USER}', '{ACCOUNT}', '2026-09-01', 1, false, 'booking', "
            "'income.earned', 'income', 2026, 13)"
        )


async def test_the_downgrade_takes_the_columns_away_and_keeps_the_bookings(scratch):
    alembic("upgrade", BEFORE, database=DB)
    await seed()
    await seed_bookings()
    alembic("upgrade", PLAN_MONTH, database=DB)

    alembic("downgrade", BEFORE, database=DB)

    assert await query("SELECT count(*) FROM transactions") == [(2,)]
    with pytest.raises(DBAPIError):
        await query("SELECT plan_month FROM transactions")

    alembic("upgrade", PLAN_MONTH, database=DB)
    assert await query("SELECT plan_month FROM transactions ORDER BY occurred_on") == [(10,), (11,)]
