"""Logging (#117): the level from `LOG_LEVEL`, unexpected errors with route and
code, and no private data in any log line."""

import logging
import sys

import pytest
from fastapi import APIRouter
from httpx import ASGITransport, AsyncClient
from sqlalchemy import text

from app.core import logging as duofy_logging
from app.core.logging import configure_logging, mask_path
from app.db.session import engine as app_engine
from app.main import app

SECRET = "hunter2-secret-value"


@pytest.fixture(autouse=True)
def _restore_root_logger():
    root = logging.getLogger()
    handlers, level = root.handlers[:], root.level
    # The test client logs every request itself, query string included; that is
    # not the backend's output and would only muddy what is checked here.
    logging.getLogger("httpx").setLevel(logging.WARNING)
    yield
    root.handlers, root.level = handlers, level


def test_log_level_controls_the_root_logger():
    configure_logging("debug")
    assert logging.getLogger().level == logging.DEBUG
    configure_logging("ERROR")
    assert logging.getLogger().level == logging.ERROR


def test_an_invalid_log_level_falls_back_to_info_with_a_warning(capsys):
    configure_logging("loud")

    assert logging.getLogger().level == logging.INFO
    output = capsys.readouterr().out
    assert "level=WARNING" in output
    assert "event=invalid_log_level" in output


@pytest.fixture
async def failing_client():
    router = APIRouter()

    @router.post("/boom/{item}")
    async def boom(item: str):
        raise RuntimeError(f"database says {SECRET}")

    before = app.router.routes[:]
    app.include_router(router, prefix="/__test")
    transport = ASGITransport(app=app)
    async with AsyncClient(transport=transport, base_url="http://test") as client:
        yield client
    app.router.routes[:] = before


async def test_an_unexpected_error_is_logged_with_route_and_answers_with_a_code(
    failing_client: AsyncClient, capsys
):
    configure_logging("INFO")

    response = await failing_client.post("/__test/boom/42", json={"amount": SECRET})

    assert response.status_code == 500
    assert response.json() == {"detail": {"code": "internal_error"}}
    output = capsys.readouterr().out
    assert "event=unhandled_exception" in output
    assert "method=POST" in output
    assert "route=/__test/boom/{id}" in output
    assert "status=500" in output
    assert "code=internal_error" in output
    assert "error=RuntimeError" in output
    assert "in boom" in output  # the stack is there


async def test_request_content_does_not_reach_the_log(failing_client: AsyncClient, capsys):
    configure_logging("DEBUG")

    await failing_client.post(
        "/__test/boom/42?email=x@example.org",
        json={"password": SECRET, "amount": "12.34", "iban": "DE00SECRET"},
        headers={"Authorization": f"Bearer {SECRET}"},
        cookies={"refresh": SECRET},
    )

    output = capsys.readouterr().out
    assert "unhandled_exception" in output
    for private in (SECRET, "12.34", "DE00SECRET", "x@example.org", "Bearer"):
        assert private not in output


TOKEN = "dGhpcy1pcy1hLXNlY3JldC1pbnZpdGF0aW9uLXRva2Vu"  # 43 characters, like token_urlsafe(32)


def test_an_invitation_token_in_a_path_is_masked():
    assert mask_path(f"/api/v1/invitations/{TOKEN}/accept") == "/api/v1/invitations/{id}/accept"
    assert mask_path("/api/v1/budgets/42/months") == "/api/v1/budgets/{id}/months"
    assert mask_path("/api/v1/budgets?email=x@example.org") == "/api/v1/budgets"


async def test_a_route_with_a_token_is_logged_masked(failing_client: AsyncClient, capsys):
    configure_logging("INFO")

    await failing_client.post(f"/__test/boom/{TOKEN}")

    output = capsys.readouterr().out
    assert "route=/__test/boom/{id}" in output
    assert TOKEN not in output


def test_the_uvicorn_access_line_is_masked_and_has_no_query_string(capsys):
    configure_logging("INFO")
    access = logging.getLogger("uvicorn.access")
    handler = logging.StreamHandler(sys.stdout)
    access.addHandler(handler)
    try:
        access.info(
            '%s - "%s %s HTTP/%s" %d',
            "127.0.0.1:1",
            "POST",
            f"/api/v1/invitations/{TOKEN}/accept?email=x@example.org",
            "1.1",
            200,
        )
    finally:
        access.removeHandler(handler)

    output = capsys.readouterr().out
    assert "/api/v1/invitations/{id}/accept" in output
    assert TOKEN not in output
    assert "x@example.org" not in output


@pytest.mark.parametrize("level", ["INFO", "DEBUG"])
async def test_sql_parameters_do_not_reach_the_log_at_any_level(capsys, level):
    configure_logging(level)
    marker = "marker-value-9f3a"

    # The engine the app itself uses, not the quiet one of the test fixtures.
    async with app_engine.connect() as conn:
        await conn.execute(text("SELECT CAST(:m AS text)"), {"m": marker})

    assert marker not in capsys.readouterr().out


async def test_a_failure_after_the_response_started_is_logged_and_not_re_raised(capsys):
    configure_logging("INFO")
    sent = []

    async def app(scope, receive, send):
        await send({"type": "http.response.start", "status": 200, "headers": []})
        raise RuntimeError(f"late {SECRET}")

    async def send(message):
        sent.append(message)

    wrapped = duofy_logging.UnexpectedErrorMiddleware(app)
    await wrapped({"type": "http", "method": "GET", "path": "/x"}, None, send)

    output = capsys.readouterr().out
    assert "error=RuntimeError" in output
    assert SECRET not in output
    assert sent[-1] == {"type": "http.response.body", "body": b"", "more_body": False}
