"""The operator's legal texts appear only when configured."""

from pathlib import Path

import httpx
import pytest
from httpx import ASGITransport

from app.core.config import settings
from app.main import app


@pytest.fixture
async def anon():
    async with httpx.AsyncClient(
        transport=ASGITransport(app=app), base_url="http://test"
    ) as client:
        yield client


@pytest.fixture(autouse=True)
def _nothing_configured(monkeypatch):
    for name in ("imprint_file", "privacy_file", "terms_file"):
        monkeypatch.setattr(settings, name, None)


async def test_without_configuration_there_is_no_document(anon) -> None:
    assert (await anon.get("/api/v1/legal")).json() == {"documents": []}
    assert (await anon.get("/api/v1/legal/imprint")).status_code == 404


async def test_a_configured_file_is_listed_and_served_without_signing_in(
    anon, tmp_path: Path, monkeypatch
) -> None:
    file = tmp_path / "imprint.md"
    file.write_text("Beispiel GmbH\nMusterweg 1", encoding="utf-8")
    monkeypatch.setattr(settings, "imprint_file", str(file))

    assert (await anon.get("/api/v1/legal")).json() == {"documents": ["imprint"]}
    response = await anon.get("/api/v1/legal/imprint")
    assert response.status_code == 200
    assert response.json() == {"text": "Beispiel GmbH\nMusterweg 1"}
    assert (await anon.get("/api/v1/legal/terms")).status_code == 404


async def test_a_missing_or_empty_file_counts_as_not_configured(
    anon, tmp_path: Path, monkeypatch
) -> None:
    empty = tmp_path / "empty.md"
    empty.write_text("  \n", encoding="utf-8")
    monkeypatch.setattr(settings, "imprint_file", str(empty))
    monkeypatch.setattr(settings, "privacy_file", str(tmp_path / "gone.md"))

    assert (await anon.get("/api/v1/legal")).json() == {"documents": []}


async def test_an_unknown_document_name_is_404(anon) -> None:
    assert (await anon.get("/api/v1/legal/passwd")).status_code == 404


async def test_a_file_over_the_size_limit_counts_as_not_configured(
    anon, tmp_path: Path, monkeypatch
) -> None:
    big = tmp_path / "big.txt"
    big.write_text("x" * (200 * 1024 + 1), encoding="utf-8")
    monkeypatch.setattr(settings, "imprint_file", str(big))

    assert (await anon.get("/api/v1/legal")).json() == {"documents": []}
