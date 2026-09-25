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

import asyncio
import importlib.util
import sys
from collections.abc import AsyncGenerator

import pytest
import pytest_asyncio
from httpx import ASGITransport, AsyncClient
from sqlalchemy import event
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

# --- #101 diagnostics — temporary, not part of the fix, do not merge ----------
#
# Prints one line per fact under a DIAG101 prefix, so the CI log can be grepped
# for it. Removed again once #101's real CI run has been read.


def _diag(line: str) -> None:
    print(f"DIAG101 {line}", flush=True)


_DIAG_AREA_PERMISSIONS_TESTS = {
    "tests/test_area_permissions.py::test_areas_do_not_leak_into_each_other",
    "tests/test_area_permissions.py::test_the_owner_grants_not_the_asker",
}


def _diag_loop_line(where: str) -> str:
    loop = asyncio.get_running_loop()
    task = asyncio.current_task()
    task_name = task.get_name() if task is not None else "none"
    return f"{where} loop_id={id(loop)} loop_type={type(loop).__qualname__} task={task_name}"


@pytest.fixture(autouse=True)
async def _diag_test_start(request: pytest.FixtureRequest) -> AsyncGenerator[None]:
    """Async, so it runs inside the real per-test loop — a sync hook would fire
    before any async fixture (including this test's own loop) exists yet.
    """
    if request.node.nodeid in _DIAG_AREA_PERMISSIONS_TESTS:
        _diag(f"nodeid={request.node.nodeid} " + _diag_loop_line("test-start"))
    yield


# --- end #101 diagnostics (header only; more below, next to what it inspects) -


def pytest_configure(config: pytest.Config) -> None:
    """Refuse to run against anything that is not obviously a test database.

    `Settings` reads `.env.local` when no environment variables are set, and that file
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

    # #101 diagnostics — one-shot facts about the run, before any test starts.
    policy = asyncio.get_event_loop_policy()
    uvloop_importable = importlib.util.find_spec("uvloop") is not None
    _diag(
        "startup "
        f"python={sys.version!r} executable={sys.executable} "
        f"pytest_asyncio={pytest_asyncio.__version__} "
        f"loop_policy={type(policy).__qualname__} uvloop_importable={uvloop_importable}"
    )
    fixture_scope = config.getini("asyncio_default_fixture_loop_scope")
    test_scope = config.getini("asyncio_default_test_loop_scope")
    _diag(
        "ini "
        f"asyncio_mode={config.getini('asyncio_mode')!r} "
        f"asyncio_default_fixture_loop_scope={fixture_scope!r} "
        f"asyncio_default_test_loop_scope={test_scope!r} "
        f"rootpath={config.rootpath} inipath={config.inipath}"
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
    _diag(_diag_loop_line("engine-fixture-start"))
    engine = create_async_engine(settings.database_url, echo=False)

    @event.listens_for(engine.sync_engine, "checkout")
    def _diag_on_checkout(dbapi_connection, connection_record, connection_proxy) -> None:
        try:
            loop_line = _diag_loop_line("pool-checkout")
        except RuntimeError:
            loop_line = "pool-checkout loop_id=none (no running loop)"
        _diag(f"conn_id={id(dbapi_connection)} {loop_line}")

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
    _diag(_diag_loop_line("session-fixture-start"))
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
    _diag(_diag_loop_line("client-fixture-start"))
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
