"""Test setup: one throwaway database, one client per test.

## Why a real Postgres and not SQLite

The models use things SQLite does not have — a partial unique index on `accounts`,
`timestamptz`, the CHECK constraints on `commitments`. A test suite on SQLite would
be green while the constraints that actually protect the data went unchecked. The
point of these tests is the rules, so they run where the rules live.

Locally that means a container:

    docker run -d --rm --name duofy-test \\
      -e POSTGRES_PASSWORD=test -e POSTGRES_USER=duofy -e POSTGRES_DB=duofy_test \\
      -p 55433:5432 postgres:18-alpine

    POSTGRES_HOST=localhost POSTGRES_PORT=55433 POSTGRES_DB=duofy_test \\
    POSTGRES_USER=duofy POSTGRES_PASSWORD=test JWT_SECRET=test uv run pytest

In CI a service container does the same job, see `.github/workflows/ci.yml`.

## Why the schema is created from the models, not from the migrations

`create_all` is fast and independent. Whether the migrations arrive at the same
schema is a separate question, and one the migration itself answers when it is run
forwards and backwards before a pull request.
"""

from collections.abc import AsyncGenerator

import pytest
from httpx import ASGITransport, AsyncClient
from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker, create_async_engine

# `models` is imported for its side effect: it registers every table on
# `Base.metadata`, and without it `create_all` would produce an empty database.
#
# Written as `from app import models`, not `import app.models` — the latter rebinds
# the name `app` from the FastAPI instance to the package, and every
# `app.dependency_overrides` below would fail.
from app import models  # noqa: F401
from app.core.config import settings
from app.db.base import Base
from app.db.session import get_session
from app.main import app


def pytest_configure() -> None:
    """Refuse to run against anything that is not obviously a test database.

    `Settings` reads `.env.dev` when no environment variables are set, and that file
    points at a real database with real data in it. The fixture below starts with
    `drop_all`. Without this guard, running `pytest` with no environment set would
    delete the development database — and the command that does it looks completely
    harmless.
    """
    if "test" not in settings.postgres_db:
        raise pytest.UsageError(
            f"POSTGRES_DB is {settings.postgres_db!r}, which does not look like a test "
            "database — refusing to run, because the fixtures drop every table. "
            "Set POSTGRES_DB to something containing 'test'."
        )


@pytest.fixture(scope="session")
def anyio_backend() -> str:
    return "asyncio"


@pytest.fixture(scope="session")
async def engine() -> AsyncGenerator:
    """One engine for the whole run, with the schema built once.

    `echo=False` regardless of `settings.debug`: a failing test should show its
    assertion, not four hundred lines of SQL above it.
    """
    engine = create_async_engine(settings.database_url, echo=False)

    async with engine.begin() as connection:
        await connection.run_sync(Base.metadata.drop_all)
        await connection.run_sync(Base.metadata.create_all)

    yield engine
    await engine.dispose()


@pytest.fixture
async def session(engine) -> AsyncGenerator[AsyncSession]:
    """A session per test, and an empty database to go with it.

    Every table is emptied before the test rather than after: a failed test then
    leaves its rows behind and they can be looked at.

    `TRUNCATE ... CASCADE` rather than dropping and recreating — it is far quicker,
    and `RESTART IDENTITY` keeps sequences from carrying over between tests.
    """
    tabellen = ", ".join(f'"{table.name}"' for table in reversed(Base.metadata.sorted_tables))
    async with engine.begin() as connection:
        await connection.exec_driver_sql(f"TRUNCATE {tabellen} RESTART IDENTITY CASCADE")

    factory = async_sessionmaker(engine, class_=AsyncSession, expire_on_commit=False)
    async with factory() as session:
        yield session


@pytest.fixture
async def client(session) -> AsyncGenerator[AsyncClient]:
    """The app, wired to the test session.

    The override matters: without it the endpoints would open their own session
    against the same database, and a test could not see what the endpoint wrote
    before it committed.

    `base_url` uses http, so `Secure` cookies would be dropped — hence
    `settings.cookie_secure` is switched off for the duration of the run. That is
    the one place where the test environment differs from production on purpose.
    """
    app.dependency_overrides[get_session] = lambda: session

    transport = ASGITransport(app=app)
    async with AsyncClient(transport=transport, base_url="http://test") as client:
        yield client

    app.dependency_overrides.clear()


@pytest.fixture(autouse=True, scope="session")
def _cookies_without_https() -> None:
    """Let the test client keep the refresh cookie.

    httpx follows the rules: a cookie marked `Secure` never comes back over http.
    Every refresh test would fail on that alone, for a reason that has nothing to do
    with what is being tested.
    """
    settings.cookie_secure = False
