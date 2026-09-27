# ruff: noqa: F811
"""The migration of #191, forwards and backwards on filled tables."""

import pytest
from sqlalchemy.ext.asyncio import AsyncConnection

from tests.test_migrations import (
    alembic,
    at_revision,  # noqa: F401  (a fixture, used by name below)
    columns,
    seed_owner,
)

#: The head before the buffer went away.
BEFORE_DROP = "a2c6e94b1d70"

pytestmark = pytest.mark.parametrize("at_revision", [BEFORE_DROP], indirect=True)

TABLES = {"users", "plans", "households"}


async def test_the_buffer_column_is_gone_from_all_three_tables(at_revision: AsyncConnection):
    await seed_owner(at_revision)
    assert await columns(at_revision, "buffer_percent") >= TABLES

    alembic("upgrade", "head")

    assert await columns(at_revision, "buffer_percent") == set()


async def test_the_downgrade_brings_the_columns_back_and_keeps_the_user(
    at_revision: AsyncConnection,
):
    await seed_owner(at_revision)
    alembic("upgrade", "head")

    alembic("downgrade", BEFORE_DROP)

    assert await columns(at_revision, "buffer_percent") >= TABLES
