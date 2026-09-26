"""The operator's legal texts: imprint, privacy policy, terms.

They are configured per instance (`IMPRINT_FILE`, `PRIVACY_FILE`, `TERMS_FILE`),
because the operator is liable for them and a self-hosted family instance usually
needs none. Public on purpose: an imprint has to be reachable without signing in.

A document that is not configured, or whose file cannot be read, does not exist —
no entry in the list, 404 when asked for. A link that leads nowhere is worse than
none.
"""

import logging
from pathlib import Path

from fastapi import APIRouter, HTTPException, status
from pydantic import BaseModel

from app.core.config import settings

logger = logging.getLogger(__name__)

router = APIRouter()

#: A legal text is a page of prose. Anything bigger is a wrong file, not a text.
_MAX_BYTES = 200 * 1024

#: Document name in the URL → the setting holding its file path.
_SETTINGS = {
    "imprint": "imprint_file",
    "privacy": "privacy_file",
    "terms": "terms_file",
}


class LegalIndex(BaseModel):
    documents: list[str]


class LegalDocument(BaseModel):
    text: str


def _read(name: str) -> str | None:
    """The text of one document, or `None` if it is not configured or unreadable."""
    path = getattr(settings, _SETTINGS[name])
    if not path:
        return None
    try:
        file = Path(path)
        if file.stat().st_size > _MAX_BYTES:
            logger.error("legal document %s is configured but larger than the limit", name)
            return None
        text = file.read_text(encoding="utf-8")
    except (OSError, UnicodeDecodeError) as error:
        # Class only, not the message: it would carry the path.
        logger.error(
            "legal document %s is configured but unreadable: %s", name, type(error).__name__
        )
        return None
    return text if text.strip() else None


@router.get("", response_model=LegalIndex)
async def list_documents() -> LegalIndex:
    """Which documents this instance has — the frontend links exactly these."""
    return LegalIndex(documents=[name for name in _SETTINGS if _read(name) is not None])


@router.get("/{name}", response_model=LegalDocument)
async def get_document(name: str) -> LegalDocument:
    if name not in _SETTINGS:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "legal_not_found"})
    text = _read(name)
    if text is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "legal_not_found"})
    return LegalDocument(text=text)
