"""Long-lived sessions, one row per device.

The access token is a short-lived JWT and cannot be taken back once signed. This
module holds the other half: a random value the client keeps, backed by a row that
can be deleted. That row is what makes "sign out", "sign out everywhere" and
"somebody has a copy of my token" possible at all.

Three rules run through everything here:

* **Only hashes are stored.** A database dump must not hand out sessions.
* **Every use replaces the token.** A value that travels over the network many
  times is worth stealing; one that is good for a single exchange is not.
* **A replayed token is treated as theft, not as an error.** See `rotate`.

The endpoints in `app/api/v1/auth.py` call into this; no HTTP terms appear here.
"""

import hashlib
import secrets
import uuid
from datetime import UTC, datetime, timedelta

from sqlalchemy import delete, or_, select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.config import settings
from app.models.refresh_token import RefreshToken

#: 32 bytes of randomness, URL-safe. Long enough that guessing is hopeless and
#: short enough to sit in a cookie without thought.
TOKEN_BYTES = 32


class RefreshTokenError(Exception):
    """The token cannot be used. Deliberately without a reason attached.

    Expired, unknown and replayed all look the same to the caller, and they should:
    telling somebody *why* their token failed tells an attacker which of their
    guesses came close.

    `all_sessions_ended` is the one distinction the endpoint needs — not to report
    it, but to know that a reused token has just closed every session of that user.
    """

    def __init__(self, all_sessions_ended: bool = False) -> None:
        super().__init__("refresh_token_invalid")
        self.all_sessions_ended = all_sessions_ended


def hash_token(token: str) -> str:
    """SHA-256, hex encoded.

    No salt and no password hashing function on purpose. Those exist to slow down
    guessing low-entropy secrets; this token has 256 bits of randomness, so there is
    nothing to guess. A fast hash is what we want — it runs on every request that
    refreshes.
    """
    return hashlib.sha256(token.encode()).hexdigest()


def _expiry() -> datetime:
    return datetime.now(UTC) + timedelta(seconds=settings.refresh_lifetime_seconds)


async def issue(session: AsyncSession, user_id: uuid.UUID) -> str:
    """Start a new session and return the token to hand to the client.

    The plaintext token is returned and never stored. This is the only moment it
    exists on the server.

    Expired rows of the same user are cleared out on the way. That keeps the table
    from growing with every abandoned login without needing a scheduled job — and
    signing in is the natural moment for it, because it is rare.
    """
    await session.execute(
        delete(RefreshToken).where(
            RefreshToken.user_id == user_id,
            RefreshToken.expires_at <= datetime.now(UTC),
        )
    )

    token = secrets.token_urlsafe(TOKEN_BYTES)
    session.add(
        RefreshToken(
            user_id=user_id,
            token_hash=hash_token(token),
            expires_at=_expiry(),
        )
    )
    await session.commit()
    return token


async def rotate(session: AsyncSession, token: str) -> tuple[uuid.UUID, str]:
    """Exchange a token for a new one. Returns the user and the new token.

    The row is **updated rather than replaced**, so a session keeps its identity: a
    session list can say "this phone, signed in three weeks ago" even though the
    token has changed thirty times since.

    Two things happen on every successful exchange:

    * the old hash moves to `previous_hash`, so exactly one step of history exists
    * the expiry moves forward, which is what makes the month a month of inactivity
      rather than a month since signing in

    **A token matching `previous_hash` ends every session of that user.** A real
    client never sends a token it has already exchanged — it has the new one. So a
    match here means two parties hold the same token, and there is no way to tell
    which of them is the owner. Refusing just this request would leave the thief
    with a working token; ending everything costs the owner one sign-in and costs
    the thief everything.

    Raises `RefreshTokenError` for unknown, expired and replayed tokens alike.
    """
    hashed = hash_token(token)

    row = await session.scalar(
        select(RefreshToken).where(
            or_(RefreshToken.token_hash == hashed, RefreshToken.previous_hash == hashed)
        )
    )

    if row is None:
        raise RefreshTokenError

    if row.previous_hash == hashed:
        await session.execute(delete(RefreshToken).where(RefreshToken.user_id == row.user_id))
        await session.commit()
        raise RefreshTokenError(all_sessions_ended=True)

    if row.expires_at <= datetime.now(UTC):
        await session.delete(row)
        await session.commit()
        raise RefreshTokenError

    fresh = secrets.token_urlsafe(TOKEN_BYTES)
    row.previous_hash = row.token_hash
    row.token_hash = hash_token(fresh)
    row.expires_at = _expiry()
    row.last_used_at = datetime.now(UTC)
    await session.commit()

    return row.user_id, fresh


async def revoke(session: AsyncSession, token: str) -> None:
    """End the session this token belongs to.

    Silent when the token is unknown: signing out is not a place to complain. The
    client wanted the session gone, and it is gone.

    Also matches `previous_hash`, so a sign-out still works when it races with a
    refresh — the client may hold either value at that moment.
    """
    hashed = hash_token(token)
    await session.execute(
        delete(RefreshToken).where(
            or_(RefreshToken.token_hash == hashed, RefreshToken.previous_hash == hashed)
        )
    )
    await session.commit()


async def revoke_all(session: AsyncSession, user_id: uuid.UUID) -> None:
    """End every session of one user.

    Used by "sign out everywhere" and, once it exists, by changing a password:
    changing it has to lock out whoever is already inside, otherwise the change
    protects nothing.
    """
    await session.execute(delete(RefreshToken).where(RefreshToken.user_id == user_id))
    await session.commit()
