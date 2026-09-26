"""The migrations of #106 to #110, run forwards and backwards on a filled table.

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

Several tests below seed **old shapes on purpose** (`rhythm`, `due_day`, `active`,
`remaining_debt` on commitments): a migration test has to write the columns the
migration is about to remove. That is the only place those names may still appear
in the tests (closing criterion of #83).
"""

import os
import subprocess
import sys
from collections.abc import AsyncGenerator
from datetime import date
from pathlib import Path

import pytest
from sqlalchemy import text
from sqlalchemy.exc import DBAPIError
from sqlalchemy.ext.asyncio import AsyncConnection, create_async_engine

from app.core.config import settings

BACKEND = Path(__file__).parents[1]

#: The revision `block` became `budget` on, the one after it that replaced the
#: commitment type `budget` with the flag `is_limit`, and the one before both.
RENAME = "a1d9f4c6b207"
LIMIT_FLAG = "b4e2a7d15f38"
BEFORE_RENAME = "f3b71d5a92c4"

#: #107: `rhythm` (four words) became `interval_months` (a number of months).
INTERVAL = "c7f3d92e5a18"

#: #108: `due_day` left `commitments`, the day lives in `first_due_date` only.
DUE_DAY = "d8a4e1b6c093"

#: #109: `active` (a switch) became `ends_on` (the last month it falls due).
ENDS_ON = "e5b2c7a94d16"

#: #110: `remaining_debt` left `commitments`.
REMAINING_DEBT = "f7c1a3d58e29"

#: A database of its own — the suite's own one must keep the schema `create_all`
#: gave it, and these tests move a schema up and down.
SCRATCH_DB = f"{settings.postgres_db}_migrations"

#: The tables that carry the 50/30/20 dimension.
TOUCHED = {"commitments", "plan_positions", "transactions", "imported_entries"}


#: The chain up to the revision before #107, migrated once and copied per test —
#: the chain is what takes the time, and Postgres copies a database in a moment.
TEMPLATE_DB = f"{settings.postgres_db}_migrations_template"
TEMPLATE_DUE_DAY_DB = f"{settings.postgres_db}_migrations_template_due_day"
TEMPLATE_ENDS_ON_DB = f"{settings.postgres_db}_migrations_template_ends_on"
TEMPLATE_REMAINING_DEBT_DB = f"{settings.postgres_db}_migrations_template_remaining_debt"


def run_alembic(arguments: tuple[str, ...], database: str) -> subprocess.CompletedProcess:
    """Run alembic against a database, as a person would."""
    environment = os.environ | {
        "POSTGRES_HOST": settings.postgres_host,
        "POSTGRES_PORT": str(settings.postgres_port),
        "POSTGRES_DB": database,
        "POSTGRES_USER": settings.postgres_user,
        "POSTGRES_PASSWORD": settings.postgres_password,
        "JWT_SECRET": settings.jwt_secret,
    }
    return subprocess.run(
        [sys.executable, "-m", "alembic", *arguments],
        cwd=BACKEND,
        env=environment,
        capture_output=True,
        text=True,
    )


def alembic(*arguments: str, database: str = SCRATCH_DB) -> None:
    finished = run_alembic(arguments, database)
    assert finished.returncode == 0, (
        f"alembic {' '.join(arguments)} failed:\n{finished.stdout}\n{finished.stderr}"
    )


def alembic_fails(*arguments: str) -> str:
    """Run alembic on the scratch database, expect it to stop, return what it said."""
    finished = run_alembic(arguments, SCRATCH_DB)
    assert finished.returncode != 0, f"alembic {' '.join(arguments)} was expected to fail"
    return finished.stdout + finished.stderr


def maintenance_url() -> str:
    """A connection to `postgres`, from which another database can be created."""
    return settings.database_url.rsplit("/", 1)[0] + "/postgres"


async def drop_database(name: str) -> None:
    engine = create_async_engine(maintenance_url(), isolation_level="AUTOCOMMIT")
    async with engine.connect() as connection:
        # WITH (FORCE) so a connection left behind by a failed run does not block
        # the drop and take every following run down with it.
        await connection.execute(text(f'DROP DATABASE IF EXISTS "{name}" WITH (FORCE)'))
    await engine.dispose()


async def create_database(name: str, *, template: str | None = None) -> None:
    engine = create_async_engine(maintenance_url(), isolation_level="AUTOCOMMIT")
    async with engine.connect() as connection:
        suffix = f' TEMPLATE "{template}"' if template else ""
        await connection.execute(text(f'CREATE DATABASE "{name}"{suffix}'))
    await engine.dispose()


async def drop_scratch() -> None:
    await drop_database(SCRATCH_DB)


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


@pytest.fixture(scope="module")
async def template_before_interval() -> AsyncGenerator[None]:
    """The chain up to the revision before #107, migrated once for this module."""
    await drop_database(TEMPLATE_DB)
    await create_database(TEMPLATE_DB)
    alembic("upgrade", LIMIT_FLAG, database=TEMPLATE_DB)
    yield
    await drop_database(TEMPLATE_DB)


@pytest.fixture
async def before_interval(template_before_interval: None) -> AsyncGenerator[AsyncConnection]:
    """A copy of that database, private to one test."""
    await drop_database(SCRATCH_DB)
    await create_database(SCRATCH_DB, template=TEMPLATE_DB)
    scratch = create_async_engine(
        settings.database_url.rsplit("/", 1)[0] + f"/{SCRATCH_DB}", isolation_level="AUTOCOMMIT"
    )
    async with scratch.connect() as connection:
        yield connection
    await scratch.dispose()
    await drop_database(SCRATCH_DB)


#: Synthetic commitments, one for each of the four words: (rhythm, name, first due date).
ONE_OF_EACH = (
    ("monthly", "Gym", None),
    ("quarterly", "Insurance", "2026-01-15"),
    ("biannual", "Tax", "2026-02-10"),
    ("annual", "Licence", "2026-03-05"),
)


async def fill_commitments(connection: AsyncConnection) -> None:
    """Commitments in the `rhythm` shape, the way the database holds them before #107."""
    await connection.execute(
        text(
            "INSERT INTO users (id, email, hashed_password, is_active, is_superuser,"
            " is_verified, first_name, last_name) VALUES"
            " ('11111111-1111-1111-1111-111111111111', 'seed@example.invalid', 'x',"
            " true, false, true, 'Seed', 'User')"
        )
    )
    for number, (rhythm, name, first_due_date) in enumerate(ONE_OF_EACH, start=1):
        day = int(first_due_date[-2:]) if first_due_date else 1
        await connection.execute(
            text(
                "INSERT INTO commitments (id, owner_id, type, name, amount, category, budget,"
                " rhythm, first_due_date, due_day, active, pass_through, is_limit) VALUES"
                " (:id, '11111111-1111-1111-1111-111111111111', 'contract', :name, 10.00,"
                " 'leisure.subscriptions', 'wants', :rhythm, :first_due_date, :day, true,"
                " false, false)"
            ),
            {
                "id": f"33333333-3333-3333-3333-{number:012d}",
                "name": name,
                "rhythm": rhythm,
                "first_due_date": date.fromisoformat(first_due_date) if first_due_date else None,
                "day": day,
            },
        )


async def test_every_rhythm_word_becomes_its_number_of_months(before_interval: AsyncConnection):
    await fill_commitments(before_interval)

    alembic("upgrade", INTERVAL)

    rows = await before_interval.execute(
        text("SELECT name, interval_months FROM commitments ORDER BY name")
    )
    assert rows.all() == [("Gym", 1), ("Insurance", 3), ("Licence", 12), ("Tax", 6)]
    assert await columns(before_interval, "rhythm") == set()

    # NOT NULL, and no default left behind: a raw INSERT must say what it means.
    shape = await before_interval.execute(
        text(
            "SELECT is_nullable, column_default FROM information_schema.columns"
            " WHERE table_name = 'commitments' AND column_name = 'interval_months'"
        )
    )
    assert shape.all() == [("NO", None)]


async def test_the_database_holds_the_range_and_the_start_date_rule(
    before_interval: AsyncConnection,
):
    """The two CHECKs after the upgrade: 1 to 120, and a start unless it is monthly."""
    await fill_commitments(before_interval)
    alembic("upgrade", INTERVAL)

    definitions = await before_interval.execute(
        text(
            "SELECT conname, pg_get_constraintdef(oid) FROM pg_constraint"
            " WHERE conrelid = 'commitments'::regclass AND contype = 'c'"
            " AND conname IN ('ck_commitment_first_due_date_required',"
            " 'ck_commitment_interval_months_range')"
        )
    )
    by_name = dict(definitions.all())
    assert set(by_name) == {
        "ck_commitment_first_due_date_required",
        "ck_commitment_interval_months_range",
    }
    assert "interval_months = 1" in by_name["ck_commitment_first_due_date_required"]

    update = "UPDATE commitments SET interval_months = :months WHERE name = 'Gym'"
    for months in (0, 121):
        with pytest.raises(DBAPIError):
            await before_interval.execute(text(update), {"months": months})
    await before_interval.execute(text(update), {"months": 1})
    # An interval above 1 needs a start date; Gym has none.
    with pytest.raises(DBAPIError):
        await before_interval.execute(text(update), {"months": 5})


async def test_the_downgrade_gives_every_rhythm_word_back(before_interval: AsyncConnection):
    await fill_commitments(before_interval)
    alembic("upgrade", INTERVAL)

    alembic("downgrade", "-1")

    rows = await before_interval.execute(
        text("SELECT name, rhythm FROM commitments ORDER BY name")
    )
    assert rows.all() == [
        ("Gym", "monthly"),
        ("Insurance", "quarterly"),
        ("Licence", "annual"),
        ("Tax", "biannual"),
    ]
    assert await columns(before_interval, "interval_months") == set()
    definition = await before_interval.scalar(
        text(
            "SELECT pg_get_constraintdef(oid) FROM pg_constraint"
            " WHERE conname = 'ck_commitment_first_due_date_required'"
        )
    )
    assert "rhythm" in definition


async def test_the_downgrade_stops_at_an_interval_with_no_word_and_changes_nothing(
    before_interval: AsyncConnection,
):
    """Every 5 months has no word, and rounding it would move its due months."""
    await fill_commitments(before_interval)
    alembic("upgrade", INTERVAL)
    await before_interval.execute(
        text(
            "UPDATE commitments SET interval_months = 5, first_due_date = '2026-11-01',"
            " due_day = 1, name = 'Every five months' WHERE name = 'Insurance'"
        )
    )
    stuck_id = await before_interval.scalar(
        text("SELECT id FROM commitments WHERE name = 'Every five months'")
    )

    said = alembic_fails("downgrade", "-1")

    assert "Cannot downgrade" in said
    assert str(stuck_id) in said, "the message has to name the commitment"
    assert "Every five months" in said
    assert "every 5 months" in said

    # Nothing moved: still the new shape, still at the new revision, value intact.
    assert await columns(before_interval, "interval_months") == {"commitments"}
    assert await columns(before_interval, "rhythm") == set()
    months = await before_interval.scalar(
        text("SELECT interval_months FROM commitments WHERE name = 'Every five months'")
    )
    assert months == 5
    assert INTERVAL in run_alembic(("current",), SCRATCH_DB).stdout


@pytest.fixture(scope="module")
async def template_before_due_day() -> AsyncGenerator[None]:
    """The chain up to the revision before #108, migrated once for this module."""
    await drop_database(TEMPLATE_DUE_DAY_DB)
    await create_database(TEMPLATE_DUE_DAY_DB)
    alembic("upgrade", INTERVAL, database=TEMPLATE_DUE_DAY_DB)
    yield
    await drop_database(TEMPLATE_DUE_DAY_DB)


@pytest.fixture
async def before_due_day(template_before_due_day: None) -> AsyncGenerator[AsyncConnection]:
    """A copy of that database, private to one test."""
    await drop_database(SCRATCH_DB)
    await create_database(SCRATCH_DB, template=TEMPLATE_DUE_DAY_DB)
    scratch = create_async_engine(
        settings.database_url.rsplit("/", 1)[0] + f"/{SCRATCH_DB}", isolation_level="AUTOCOMMIT"
    )
    async with scratch.connect() as connection:
        yield connection
    await scratch.dispose()
    await drop_database(SCRATCH_DB)


OWNER = "11111111-1111-1111-1111-111111111111"


async def seed_owner(connection: AsyncConnection) -> None:
    await connection.execute(
        text(
            "INSERT INTO users (id, email, hashed_password, is_active, is_superuser,"
            " is_verified, first_name, last_name) VALUES"
            f" ('{OWNER}', 'seed@example.invalid', 'x', true, false, true, 'Seed', 'User')"
        )
    )


async def add_commitment(
    connection: AsyncConnection,
    number: int,
    name: str,
    due_day: int,
    first_due_date: str | None = None,
    interval_months: int = 1,
) -> str:
    """One commitment in the shape before #108: a due day, and maybe a start date."""
    commitment_id = f"33333333-3333-3333-3333-{number:012d}"
    await connection.execute(
        text(
            "INSERT INTO commitments (id, owner_id, type, name, amount, category, budget,"
            " interval_months, first_due_date, due_day, active, pass_through, is_limit) VALUES"
            " (:id, :owner, 'contract', :name, 10.00, 'leisure.subscriptions', 'wants',"
            " :interval, :first, :day, true, false, false)"
        ),
        {
            "id": commitment_id,
            "owner": OWNER,
            "name": name,
            "interval": interval_months,
            "first": date.fromisoformat(first_due_date) if first_due_date else None,
            "day": due_day,
        },
    )
    return commitment_id


async def add_position(connection: AsyncConnection, commitment_id: str, year: int, month: int):
    """A plan month with one position that came from the commitment."""
    plan_id = f"44444444-4444-4444-{year:04d}-{month:012d}"
    await connection.execute(
        text(
            "INSERT INTO plans (id, user_id, year, month, target_needs, target_wants,"
            " target_savings, buffer_percent) VALUES (:id, :owner, :year, :month, 50, 30, 20, 10)"
            " ON CONFLICT (id) DO NOTHING"
        ),
        {"id": plan_id, "owner": OWNER, "year": year, "month": month},
    )
    await connection.execute(
        text(
            "INSERT INTO plan_positions (id, plan_id, commitment_id, label, amount_planned,"
            " category, budget, due_day, manually_changed, is_limit, pass_through) VALUES"
            " (gen_random_uuid(), :plan, :commitment, 'Position', 10.00,"
            " 'leisure.subscriptions', 'wants', 1, false, false, false)"
        ),
        {"plan": plan_id, "commitment": commitment_id},
    )


async def this_month(connection: AsyncConnection) -> date:
    """The first of the current month as the database sees it, which is what the migration uses."""
    today = await connection.scalar(text("SELECT CURRENT_DATE"))
    return today.replace(day=1)


async def first_dates(connection: AsyncConnection) -> dict[str, date]:
    rows = await connection.execute(text("SELECT name, first_due_date FROM commitments"))
    return dict(rows.all())


async def test_a_monthly_commitment_without_a_date_starts_where_its_first_position_is(
    before_due_day: AsyncConnection,
):
    """The earliest plan month it has a position in, on its old due day."""
    await seed_owner(before_due_day)
    gym = await add_commitment(before_due_day, 1, "Gym", 20)
    await add_position(before_due_day, gym, 2026, 9)
    await add_position(before_due_day, gym, 2026, 7)

    alembic("upgrade", DUE_DAY)

    assert (await first_dates(before_due_day))["Gym"] == date(2026, 7, 20)


async def test_a_monthly_commitment_without_a_date_or_a_position_starts_this_month(
    before_due_day: AsyncConnection,
):
    """The current month at migration time — even if its day has already passed."""
    await seed_owner(before_due_day)
    await add_commitment(before_due_day, 1, "Gym", 15)

    alembic("upgrade", DUE_DAY)

    month = await this_month(before_due_day)
    assert (await first_dates(before_due_day))["Gym"] == month.replace(day=15)


@pytest.mark.parametrize(
    ("day", "expected"),
    [(31, date(2026, 3, 31)), (30, date(2026, 3, 30)), (29, date(2026, 3, 29))],
    ids=["the 31st", "the 30th", "the 29th"],
)
async def test_a_day_february_does_not_have_moves_the_start_to_march(
    before_due_day: AsyncConnection, day: int, expected: date
):
    """The day stays what it is; the start moves — clamping would change it for ever."""
    await seed_owner(before_due_day)
    rent = await add_commitment(before_due_day, 1, "Rent", day)
    await add_position(before_due_day, rent, 2026, 2)

    alembic("upgrade", DUE_DAY)

    assert (await first_dates(before_due_day))["Rent"] == expected


async def test_a_31st_starting_in_a_30_day_month_moves_to_the_next_long_one(
    before_due_day: AsyncConnection,
):
    await seed_owner(before_due_day)
    rent = await add_commitment(before_due_day, 1, "Rent", 31)
    await add_position(before_due_day, rent, 2026, 4)

    alembic("upgrade", DUE_DAY)

    assert (await first_dates(before_due_day))["Rent"] == date(2026, 5, 31)


async def test_a_december_position_starts_in_december_and_a_31st_moves_into_january(
    before_due_day: AsyncConnection,
):
    """The year-end guard: month 12 must not become month 0 or 13."""
    await seed_owner(before_due_day)
    gym = await add_commitment(before_due_day, 1, "Gym", 20)
    await add_position(before_due_day, gym, 2026, 12)
    rent = await add_commitment(before_due_day, 2, "Rent", 31)
    await add_position(before_due_day, rent, 2026, 11)

    alembic("upgrade", DUE_DAY)

    dates = await first_dates(before_due_day)
    assert dates["Gym"] == date(2026, 12, 20)
    assert dates["Rent"] == date(2026, 12, 31), "November has no 31st, December does"


async def test_a_date_with_another_day_than_the_due_day_follows_the_due_day(
    before_due_day: AsyncConnection,
):
    """The payday must not change silently: the old `due_day` wins over the date's day."""
    await seed_owner(before_due_day)
    await add_commitment(before_due_day, 1, "Insurance", 20, "2026-01-15", interval_months=3)
    # The 31st does not exist in February: the date moves to March.
    await add_commitment(before_due_day, 2, "Tax", 31, "2026-02-10", interval_months=6)
    # November has no 31st either, so this one moves to December.
    await add_commitment(before_due_day, 3, "Licence", 31, "2026-11-05", interval_months=12)

    alembic("upgrade", DUE_DAY)

    dates = await first_dates(before_due_day)
    assert dates["Insurance"] == date(2026, 1, 20)
    assert dates["Tax"] == date(2026, 3, 31)
    assert dates["Licence"] == date(2026, 12, 31)


async def test_a_date_that_is_already_there_is_left_alone(before_due_day: AsyncConnection):
    await seed_owner(before_due_day)
    await add_commitment(before_due_day, 1, "Insurance", 15, "2026-01-15", interval_months=3)

    alembic("upgrade", DUE_DAY)

    assert (await first_dates(before_due_day))["Insurance"] == date(2026, 1, 15)


async def test_the_due_day_is_gone_and_the_start_date_is_required(
    before_due_day: AsyncConnection,
):
    await seed_owner(before_due_day)
    await add_commitment(before_due_day, 1, "Gym", 15)

    alembic("upgrade", DUE_DAY)

    assert await columns(before_due_day, "due_day") == {"plan_positions"}, (
        "positions keep their own due day"
    )
    nullable = await before_due_day.scalar(
        text(
            "SELECT is_nullable FROM information_schema.columns"
            " WHERE table_name = 'commitments' AND column_name = 'first_due_date'"
        )
    )
    assert nullable == "NO"
    left = await before_due_day.execute(
        text(
            "SELECT conname FROM pg_constraint WHERE conrelid = 'commitments'::regclass"
            " AND conname IN ('ck_commitment_due_day', 'ck_commitment_first_due_date_required')"
        )
    )
    assert left.all() == []


async def test_the_downgrade_restores_the_due_day_from_the_date(
    before_due_day: AsyncConnection,
):
    """Filled table, both kinds of row; the backfilled dates stay."""
    await seed_owner(before_due_day)
    await add_commitment(before_due_day, 1, "Insurance", 15, "2026-01-15", interval_months=3)
    rent = await add_commitment(before_due_day, 2, "Rent", 31)
    await add_position(before_due_day, rent, 2026, 2)
    await add_commitment(before_due_day, 3, "Gym", 15)
    alembic("upgrade", DUE_DAY)

    alembic("downgrade", "-1")

    rows = await before_due_day.execute(
        text("SELECT name, due_day, first_due_date FROM commitments ORDER BY name")
    )
    month = await this_month(before_due_day)
    assert rows.all() == [
        ("Gym", 15, month.replace(day=15)),
        ("Insurance", 15, date(2026, 1, 15)),
        ("Rent", 31, date(2026, 3, 31)),
    ]
    restored = await before_due_day.execute(
        text(
            "SELECT conname FROM pg_constraint WHERE conrelid = 'commitments'::regclass"
            " AND conname IN ('ck_commitment_due_day', 'ck_commitment_first_due_date_required')"
        )
    )
    assert {name for (name,) in restored.all()} == {
        "ck_commitment_due_day",
        "ck_commitment_first_due_date_required",
    }
    nullable = await before_due_day.scalar(
        text(
            "SELECT is_nullable FROM information_schema.columns"
            " WHERE table_name = 'commitments' AND column_name = 'first_due_date'"
        )
    )
    assert nullable == "YES"


@pytest.fixture(scope="module")
async def template_before_ends_on() -> AsyncGenerator[None]:
    """The chain up to the revision before #109, migrated once for this module."""
    await drop_database(TEMPLATE_ENDS_ON_DB)
    await create_database(TEMPLATE_ENDS_ON_DB)
    alembic("upgrade", DUE_DAY, database=TEMPLATE_ENDS_ON_DB)
    yield
    await drop_database(TEMPLATE_ENDS_ON_DB)


@pytest.fixture
async def before_ends_on(template_before_ends_on: None) -> AsyncGenerator[AsyncConnection]:
    """A copy of that database, private to one test."""
    await drop_database(SCRATCH_DB)
    await create_database(SCRATCH_DB, template=TEMPLATE_ENDS_ON_DB)
    scratch = create_async_engine(
        settings.database_url.rsplit("/", 1)[0] + f"/{SCRATCH_DB}", isolation_level="AUTOCOMMIT"
    )
    async with scratch.connect() as connection:
        yield connection
    await scratch.dispose()
    await drop_database(SCRATCH_DB)


async def add_switched_commitment(
    connection: AsyncConnection, number: int, name: str, *, active: bool
) -> None:
    """A commitment in the shape before #109 — the old `active` switch is seeded on purpose."""
    await connection.execute(
        text(
            "INSERT INTO commitments (id, owner_id, type, name, amount, category, budget,"
            " interval_months, first_due_date, active, pass_through, is_limit) VALUES"
            " (:id, :owner, 'contract', :name, 10.00, 'leisure.subscriptions', 'wants',"
            " 1, '2026-01-15', :active, false, false)"
        ),
        {
            "id": f"33333333-3333-3333-3333-{number:012d}",
            "owner": OWNER,
            "name": name,
            "active": active,
        },
    )


async def test_a_switched_off_commitment_ends_on_the_day_of_the_migration(
    before_ends_on: AsyncConnection,
):
    await seed_owner(before_ends_on)
    await add_switched_commitment(before_ends_on, 1, "Running", active=True)
    await add_switched_commitment(before_ends_on, 2, "Cancelled", active=False)

    alembic("upgrade", ENDS_ON)

    rows = await before_ends_on.execute(
        text("SELECT name, ends_on = CURRENT_DATE, ends_on IS NULL FROM commitments ORDER BY name")
    )
    assert rows.all() == [("Cancelled", True, False), ("Running", None, True)]
    assert await columns(before_ends_on, "active") == {"accounts"}
    assert await columns(before_ends_on, "ends_on") == {"commitments"}


async def test_the_downgrade_brings_the_switch_back_and_loses_the_dates(
    before_ends_on: AsyncConnection,
):
    await seed_owner(before_ends_on)
    await add_switched_commitment(before_ends_on, 1, "Running", active=True)
    await add_switched_commitment(before_ends_on, 2, "Cancelled", active=False)
    alembic("upgrade", ENDS_ON)
    # One end long past, one still ahead: both become a plain switch, the date is gone.
    await before_ends_on.execute(
        text(
            "UPDATE commitments SET ends_on = CURRENT_DATE - INTERVAL '400 days'"
            " WHERE name = 'Cancelled'"
        )
    )
    await before_ends_on.execute(
        text(
            "INSERT INTO commitments (id, owner_id, type, name, amount, category, budget,"
            " interval_months, first_due_date, ends_on, pass_through, is_limit) VALUES"
            " ('33333333-3333-3333-3333-000000000003', :owner, 'contract', 'Ends later',"
            " 10.00, 'leisure.subscriptions', 'wants', 1, '2026-01-15',"
            " CURRENT_DATE + INTERVAL '200 days', false, false)"
        ),
        {"owner": OWNER},
    )

    alembic("downgrade", "-1")

    rows = await before_ends_on.execute(text("SELECT name, active FROM commitments ORDER BY name"))
    assert rows.all() == [("Cancelled", False), ("Ends later", True), ("Running", True)]
    assert await columns(before_ends_on, "ends_on") == set()


@pytest.fixture(scope="module")
async def template_before_remaining_debt() -> AsyncGenerator[None]:
    """The chain up to the revision before #110, migrated once for this module."""
    await drop_database(TEMPLATE_REMAINING_DEBT_DB)
    await create_database(TEMPLATE_REMAINING_DEBT_DB)
    alembic("upgrade", ENDS_ON, database=TEMPLATE_REMAINING_DEBT_DB)
    yield
    await drop_database(TEMPLATE_REMAINING_DEBT_DB)


@pytest.fixture
async def before_remaining_debt(
    template_before_remaining_debt: None,
) -> AsyncGenerator[AsyncConnection]:
    """A copy of that database, private to one test."""
    await drop_database(SCRATCH_DB)
    await create_database(SCRATCH_DB, template=TEMPLATE_REMAINING_DEBT_DB)
    scratch = create_async_engine(
        settings.database_url.rsplit("/", 1)[0] + f"/{SCRATCH_DB}", isolation_level="AUTOCOMMIT"
    )
    async with scratch.connect() as connection:
        yield connection
    await scratch.dispose()
    await drop_database(SCRATCH_DB)


async def add_debt_commitments(connection: AsyncConnection) -> None:
    """A debt with a remaining amount and a contract (old `remaining_debt`, seeded on purpose)."""
    await seed_owner(connection)
    await connection.execute(
        text(
            "INSERT INTO commitments (id, owner_id, type, name, amount, category, budget,"
            " interval_months, first_due_date, remaining_debt, pass_through, is_limit) VALUES"
            " ('33333333-3333-3333-3333-000000000001', :owner, 'debt', 'Loan', 200.00,"
            " 'finance.debt', 'savings', 1, '2026-01-15', 4200.00, false, false),"
            " ('33333333-3333-3333-3333-000000000002', :owner, 'contract', 'Gym', 10.00,"
            " 'leisure.subscriptions', 'wants', 1, '2026-01-15', NULL, false, false)"
        ),
        {"owner": OWNER},
    )


async def test_dropping_the_remaining_debt_keeps_the_commitments(
    before_remaining_debt: AsyncConnection,
):
    await add_debt_commitments(before_remaining_debt)

    alembic("upgrade", REMAINING_DEBT)

    assert await columns(before_remaining_debt, "remaining_debt") == set()
    rows = await before_remaining_debt.execute(
        text("SELECT name, type, amount FROM commitments ORDER BY name")
    )
    assert rows.all() == [("Gym", "contract", 10), ("Loan", "debt", 200)]
    constraint = await before_remaining_debt.scalar(
        text(
            "SELECT count(*) FROM pg_constraint"
            " WHERE conname = 'ck_commitment_remaining_debt_only_for_debt'"
        )
    )
    assert constraint == 0


async def test_the_downgrade_brings_the_column_back_empty_with_its_check(
    before_remaining_debt: AsyncConnection,
):
    await add_debt_commitments(before_remaining_debt)
    alembic("upgrade", REMAINING_DEBT)

    alembic("downgrade", "-1")

    rows = await before_remaining_debt.execute(
        text("SELECT name, remaining_debt FROM commitments ORDER BY name")
    )
    assert rows.all() == [("Gym", None), ("Loan", None)]
    with pytest.raises(DBAPIError):
        await before_remaining_debt.execute(
            text("UPDATE commitments SET remaining_debt = 5 WHERE name = 'Gym'")
        )
