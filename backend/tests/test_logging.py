"""Logging (#117): the level from `LOG_LEVEL`, unexpected errors with route and
code, and no private data in any log line."""

import logging

import pytest
from fastapi import APIRouter
from httpx import ASGITransport, AsyncClient

from app.core.logging import configure_logging
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


async def test_sensitive_fields_do_not_reach_the_log(failing_client: AsyncClient, capsys):
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
