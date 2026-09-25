"""The two migrations of #106, run forwards and backwards on a filled table.

## Why this test exists at all

`tests/conftest.py` builds the schema with `create_all`, straight from the
models. That is fast and it keeps the other tests independent, but it never runs
a single migration — so a migration that does not arrive at the same schema, or
one that loses data on the way, would go unnoticed until it ran on real data.

The second migration is the one that earns a test: it does not only rename, it
**rewrites rows**. Every commitment of type `budget` becomes a `contract` with
`is_limit = true`, and the whole point of #106 is that this happens to the rows
that already exist.

## How it works

Each test gets a database of its own, next to the one the suite uses, migrated
to the revision **before** the migration under test. Rows go in, the migration
runs, the rows are checked; then the downgrade runs and they are checked again.

Alembic is called as a subprocess rather than through its Python API: `env.py`
ends in `asyncio.run()`, which cannot be nested inside the event loop the async
tests already run in. A subprocess also gets the migrations exactly as a person
would run them on the command line, which is what is being verified.

Part of #106.
"""

import os
import subprocess
import sys
from collections.abc import AsyncGenerator
from pathlib import Path

import pytest
from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncConnection, create_async_engine

from app.core.config import settings

BACKEND = Path(__file__).parents[1]

#: The revision `block` became `budget` on, the one after it that replaced the
#: commitment type `budget` with the flag `is_limit`, and the one before both.
RENAME = "a1d9f4c6b207"
LIMIT_FLAG = "b4e2a7d15f38"
BEFORE_RENAME = "f3b71d5a92c4"

#: A database of its own — the suite's own one must keep the schema `create_all`
#: gave it, and these tests move a schema up and down.
SCRATCH_DB = f"{settings.postgres_db}_migrations"

#: The tables that carry the 50/30/20 dimension.
TOUCHED = {"commitments", "plan_positions", "transactions", "imported_entries"}


def alembic(*arguments: str) -> None:
    """Run alembic against the scratch database, as a person would."""
    environment = os.environ | {
        "POSTGRES_HOST": settings.postgres_host,
        "POSTGRES_PORT": str(settings.postgres_port),
        "POSTGRES_DB": SCRATCH_DB,
        "POSTGRES_USER": settings.postgres_user,
        "POSTGRES_PASSWORD": settings.postgres_password,
        "JWT_SECRET": settings.jwt_secret,
    }
    finished = subprocess.run(
        [sys.executable, "-m", "alembic", *arguments],
        cwd=BACKEND,
        env=environment,
        capture_output=True,
        text=True,
    )
    assert finished.returncode == 0, (
        f"alembic {' '.join(arguments)} failed:\n{finished.stdout}\n{finished.stderr}"
    )


def maintenance_url() -> str:
    """A connection to `postgres`, from which another database can be created."""
    return settings.database_url.rsplit("/", 1)[0] + "/postgres"


async def drop_scratch() -> None:
    engine = create_async_engine(maintenance_url(), isolation_level="AUTOCOMMIT")
    async with engine.connect() as connection:
        # WITH (FORCE) so a connection left behind by a failed run does not block
        # the drop and take every following run down with it.
        await connection.execute(text(f'DROP DATABASE IF EXISTS "{SCRATCH_DB}" WITH (FORCE)'))
    await engine.dispose()


@pytest.fixture
async def at_revision(request) -> AsyncGenerator[AsyncConnection]:
    """A fresh scratch database, migrated up to the revision the test names.

    The revision arrives through indirect parametrisation, so each test says for
    itself where it wants to start.
    """
    await drop_scratch()
    engine = create_async_engine(maintenance_url(), isolation_level="AUTOCOMMIT")
    async with engine.connect() as connection:
        await connection.execute(text(f'CREATE DATABASE "{SCRATCH_DB}"'))
    await engine.dispose()

    alembic("upgrade", request.param)

    scratch = create_async_engine(
        settings.database_url.rsplit("/", 1)[0] + f"/{SCRATCH_DB}",
        isolation_level="AUTOCOMMIT",
    )
    async with scratch.connect() as connection:
        yield connection
    await scratch.dispose()

    await drop_scratch()


async def fill(connection: AsyncConnection, *, dimension: str, flag: str) -> None:
    """One row in every table #106 touches, including a `budget` commitment.

    `dimension` is the name of the 50/30/20 column and `flag` the name of the
    limit flag on `plan_positions` at the revision the test starts from — the
    migrations rename exactly those two, so neither can be hard-coded.
    """
    await connection.execute(
        text(
            "INSERT INTO users (id, email, hashed_password, is_active, is_superuser,"
            " is_verified, first_name, last_name) VALUES"
            " ('11111111-1111-1111-1111-111111111111', 'seed@example.invalid', 'x',"
            " true, false, true, 'Seed', 'User')"
        )
    )
    await connection.execute(
        text(
            "INSERT INTO accounts (id, owner_id, name, type, opening_balance,"
            " opening_date, active, is_default) VALUES"
            " ('22222222-2222-2222-2222-222222222222',"
            " '11111111-1111-1111-1111-111111111111', 'Giro', 'checking', 100.00,"
            " '2026-01-01', true, true)"
        )
    )
    await connection.execute(
        text(
            "INSERT INTO commitments (id, owner_id, type, name, amount, category,"
            f" {dimension}, rhythm, due_day, active, pass_through) VALUES"
            " ('33333333-3333-3333-3333-333333333333',"
            " '11111111-1111-1111-1111-111111111111', 'contract', 'Rent', 890.00,"
            " 'housing.rent', 'needs', 'monthly', 1, true, false),"
            " ('33333333-3333-3333-3333-333333333334',"
            " '11111111-1111-1111-1111-111111111111', 'budget', 'Groceries', 600.00,"
            " 'household.groceries', 'needs', 'monthly', 1, true, false)"
        )
    )
    await connection.execute(
        text(
            "INSERT INTO plans (id, user_id, year, month, target_needs, target_wants,"
            " target_savings, buffer_percent) VALUES"
            " ('44444444-4444-4444-4444-444444444444',"
            " '11111111-1111-1111-1111-111111111111', 2026, 9, 50, 30, 20, 10)"
        )
    )
    await connection.execute(
        text(
            "INSERT INTO plan_positions (id, plan_id, commitment_id, label,"
            f" amount_planned, category, {dimension}, due_day, manually_changed,"
            f" {flag}, pass_through) VALUES"
            " ('55555555-5555-5555-5555-555555555555',"
            " '44444444-4444-4444-4444-444444444444',"
            " '33333333-3333-3333-3333-333333333333', 'Rent', 890.00, 'housing.rent',"
            " 'needs', 1, false, false, false),"
            " ('55555555-5555-5555-5555-555555555556',"
            " '44444444-4444-4444-4444-444444444444',"
            " '33333333-3333-3333-3333-333333333334', 'Groceries', 600.00,"
            " 'household.groceries', 'needs', 1, false, true, false)"
        )
    )
    await connection.execute(
        text(
            "INSERT INTO transactions (id, owner_id, account_id, occurred_on, amount,"
            f" category, {dimension}, auto_booked) VALUES"
            " ('66666666-6666-6666-6666-666666666666',"
            " '11111111-1111-1111-1111-111111111111',"
            " '22222222-2222-2222-2222-222222222222', '2026-09-03', 42.50,"
            " 'household.groceries', 'needs', false)"
        )
    )
    await connection.execute(
        text(
            "INSERT INTO imported_entries (id, owner_id, imported_by_id, account_id,"
            " external_ref, occurred_on, value_on, amount, incoming, category,"
            f" {dimension}) VALUES ('77777777-7777-7777-7777-777777777777',"
            " '11111111-1111-1111-1111-111111111111',"
            " '11111111-1111-1111-1111-111111111111',"
            " '22222222-2222-2222-2222-222222222222', 'REF-1', '2026-09-04',"
            " '2026-09-04', 12.00, false, 'household.groceries', 'needs')"
        )
    )


async def columns(connection: AsyncConnection, column: str) -> set[str]:
    """Which tables carry a column of that name right now."""
    rows = await connection.execute(
        text(
            "SELECT table_name FROM information_schema.columns"
            " WHERE table_schema = 'public' AND column_name = :column"
        ),
        {"column": column},
    )
    return {row[0] for row in rows}


@pytest.mark.parametrize("at_revision", [BEFORE_RENAME], indirect=True)
async def test_the_rename_keeps_every_value(at_revision: AsyncConnection):
    """`block` becomes `budget` on all four tables, and no value moves."""
    await fill(at_revision, dimension="block", flag="is_budget")

    alembic("upgrade", RENAME)

    assert await columns(at_revision, "budget") == TOUCHED
    assert await columns(at_revision, "block") == set()
    rows = await at_revision.execute(text("SELECT name, budget FROM commitments ORDER BY name"))
    assert rows.all() == [("Groceries", "needs"), ("Rent", "needs")]

    # The one constraint that names the column in its expression has to name the
    # new one: the model declares it too, and the two must agree.
    definition = await at_revision.scalar(
        text(
            "SELECT pg_get_constraintdef(oid) FROM pg_constraint"
            " WHERE conname = 'ck_transaction_purpose_unless_transfer'"
        )
    )
    assert "budget IS NOT NULL" in definition

    alembic("downgrade", "-1")

    assert await columns(at_revision, "block") == TOUCHED
    assert await columns(at_revision, "budget") == set()
    rows = await at_revision.execute(text("SELECT name, block FROM commitments ORDER BY name"))
    assert rows.all() == [("Groceries", "needs"), ("Rent", "needs")]


@pytest.mark.parametrize("at_revision", [RENAME], indirect=True)
async def test_a_budget_commitment_becomes_a_contract_with_a_limit(
    at_revision: AsyncConnection,
):
    """The heart of point 2 of #106, on rows that were already there.

    Groceries were a commitment of type `budget`; afterwards they are an ordinary
    contract whose amount is a limit. Rent, a contract all along, is left alone —
    the flag starts out false and the migration must not set it.
    """
    await fill(at_revision, dimension="budget", flag="is_budget")

    alembic("upgrade", LIMIT_FLAG)

    rows = await at_revision.execute(
        text("SELECT name, type, is_limit FROM commitments ORDER BY name")
    )
    assert rows.all() == [
        ("Groceries", "contract", True),
        ("Rent", "contract", False),
    ]

    # The snapshot on the position is renamed, not rebuilt: its values survive.
    positions = await at_revision.execute(
        text("SELECT label, is_limit FROM plan_positions ORDER BY label")
    )
    assert positions.all() == [("Groceries", True), ("Rent", False)]
    assert await columns(at_revision, "is_budget") == set()

    # NOT NULL, and with no server default left behind: new rows get the value
    # from the model, like every other flag.
    shape = await at_revision.execute(
        text(
            "SELECT is_nullable, column_default FROM information_schema.columns"
            " WHERE table_name = 'commitments' AND column_name = 'is_limit'"
        )
    )
    assert shape.all() == [("NO", None)]


@pytest.mark.parametrize("at_revision", [RENAME], indirect=True)
async def test_the_downgrade_gives_the_budget_type_back(at_revision: AsyncConnection):
    """It has to succeed on a filled table and keep the position's flag.

    The type mapping is **not** exact, and the migration says so: a contract that
    was a contract before and got `is_limit` by hand afterwards comes back as a
    `budget`. What must not happen is losing what `plan_positions` knows, because
    that is where the behaviour is read.
    """
    await fill(at_revision, dimension="budget", flag="is_budget")

    alembic("upgrade", LIMIT_FLAG)
    alembic("downgrade", "-1")

    rows = await at_revision.execute(text("SELECT name, type FROM commitments ORDER BY name"))
    assert rows.all() == [("Groceries", "budget"), ("Rent", "contract")]

    positions = await at_revision.execute(
        text("SELECT label, is_budget FROM plan_positions ORDER BY label")
    )
    assert positions.all() == [("Groceries", True), ("Rent", False)]
    assert await columns(at_revision, "is_limit") == set()
