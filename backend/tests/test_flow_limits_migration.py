"""The migration that adds `users.flow_limits_by` (#93), up and down.

A database of its own, migrated up to the revision before it; one user goes in,
the upgrade runs, and the downgrade takes the column away again.
"""

import pytest
from sqlalchemy import text
from sqlalchemy.exc import DBAPIError
from sqlalchemy.ext.asyncio import create_async_engine

from app.core.config import settings
from tests.test_migrations import (
    REMAINING_DEBT,
    alembic,
    create_database,
    drop_database,
)

FLOW_LIMITS = "a3e6c9d2b715"
DB = f"{settings.postgres_db}_flow_limits"


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


async def test_existing_users_start_on_the_plan_and_the_downgrade_removes_the_column(scratch):
    alembic("upgrade", REMAINING_DEBT, database=DB)
    await query(
        "INSERT INTO users (id, email, hashed_password, is_active, is_superuser, is_verified, "
        "first_name, last_name) VALUES (gen_random_uuid(), 'a@example.com', 'x', true, false, "
        "true, 'Ada', 'Test')"
    )

    alembic("upgrade", FLOW_LIMITS, database=DB)
    assert await query("SELECT flow_limits_by FROM users") == [("plan",)]
    with pytest.raises(DBAPIError):
        await query("UPDATE users SET flow_limits_by = 'nonsense'")

    alembic("downgrade", REMAINING_DEBT, database=DB)
    with pytest.raises(DBAPIError):
        await query("SELECT flow_limits_by FROM users")
