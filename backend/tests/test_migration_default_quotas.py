# ruff: noqa: F811
"""The migration of #84, forwards and backwards on a filled `users` table."""

import pytest
from sqlalchemy import text
from sqlalchemy.exc import DBAPIError
from sqlalchemy.ext.asyncio import AsyncConnection

from tests.test_migrations import (
    REMAINING_DEBT,
    alembic,
    at_revision,  # noqa: F401  (a fixture, used by name below)
    columns,
    seed_owner,
)

pytestmark = pytest.mark.parametrize("at_revision", [REMAINING_DEBT], indirect=True)


async def test_existing_users_get_the_guideline_as_their_default(at_revision: AsyncConnection):
    await seed_owner(at_revision)

    alembic("upgrade", "head")

    row = await at_revision.execute(
        text("SELECT target_needs, target_wants, target_savings, buffer_percent FROM users")
    )
    assert row.one() == (50, 30, 20, 0)


async def test_the_database_refuses_quotas_that_do_not_add_up(at_revision: AsyncConnection):
    await seed_owner(at_revision)
    alembic("upgrade", "head")

    with pytest.raises(DBAPIError):
        await at_revision.execute(text("UPDATE users SET target_needs = 60"))


async def test_the_downgrade_removes_the_columns_and_keeps_the_user(at_revision: AsyncConnection):
    await seed_owner(at_revision)
    alembic("upgrade", "head")

    alembic("downgrade", REMAINING_DEBT)

    assert await columns(at_revision, "target_savings") >= {"plans", "households"}
    assert "users" not in await columns(at_revision, "target_savings")
    assert await at_revision.scalar(text("SELECT count(*) FROM users")) == 1
