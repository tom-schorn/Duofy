"""The rules a long-lived session has to follow.

These are the first tests in the project that touch the database, and they exist
because the next change to authentication has to be able to break something
visibly. What is checked here is not "does the endpoint answer 200" but the four
rules the design rests on:

* a token works exactly once
* the one it replaced is treated as theft, not as an error
* an expired session cannot be revived
* signing out takes effect immediately, not when the token would have expired
"""

from datetime import UTC, datetime, timedelta

from httpx import AsyncClient
from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.config import settings
from app.models.refresh_token import RefreshToken
from app.services import refresh_tokens

PASSWORD = "test-password-123"


async def register_and_login(client: AsyncClient, email: str = "someone@example.org") -> str:
    """Create a user, sign in, and return the refresh token from the cookie."""
    response = await client.post(
        "/api/v1/auth/register",
        json={
            "email": email,
            "password": PASSWORD,
            "first_name": "Test",
            "last_name": "Person",
        },
    )
    assert response.status_code == 201, response.text

    response = await client.post(
        "/api/v1/auth/login",
        data={"username": email, "password": PASSWORD},
    )
    assert response.status_code == 200, response.text
    assert response.json()["access_token"]
    return response.cookies[settings.cookie_name]


async def count_sessions(session: AsyncSession) -> int:
    return await session.scalar(select(func.count()).select_from(RefreshToken)) or 0


async def test_login_starts_a_session_and_hides_the_token_from_javascript(
    client: AsyncClient, session: AsyncSession
) -> None:
    await register_and_login(client)

    assert await count_sessions(session) == 1

    # The response body carries the access token only. Were the refresh token in
    # there as well, putting it in an HttpOnly cookie would achieve nothing.
    response = await client.post(
        "/api/v1/auth/login",
        data={"username": "someone@example.org", "password": PASSWORD},
    )
    assert "refresh" not in response.text.lower()

    header = response.headers["set-cookie"]
    assert "HttpOnly" in header
    assert "SameSite=lax" in header


async def test_only_the_hash_reaches_the_database(
    client: AsyncClient, session: AsyncSession
) -> None:
    """A database dump must not hand out sessions."""
    token = await register_and_login(client)

    row = await session.scalar(select(RefreshToken))
    assert row is not None
    assert row.token_hash != token
    assert row.token_hash == refresh_tokens.hash_token(token)
    assert len(row.token_hash) == 64


async def test_refreshing_replaces_the_token(client: AsyncClient, session: AsyncSession) -> None:
    """A new token every time, and still exactly one session."""
    first = await register_and_login(client)

    response = await client.post("/api/v1/auth/refresh")
    assert response.status_code == 200, response.text

    second = response.cookies[settings.cookie_name]
    assert second != first
    assert await count_sessions(session) == 1


async def test_the_replaced_token_ends_every_session(
    client: AsyncClient, session: AsyncSession
) -> None:
    """The core of the design.

    A real client never sends a token it has already exchanged — it holds the new
    one. So a replaced token turning up means two parties have the same value, and
    there is no telling which is the owner. Refusing the one request would leave the
    thief with something that works.
    """
    stolen = await register_and_login(client)

    assert (await client.post("/api/v1/auth/refresh")).status_code == 200
    assert await count_sessions(session) == 1

    # Replay the token from before the rotation.
    client.cookies.set(settings.cookie_name, stolen)
    response = await client.post("/api/v1/auth/refresh")

    assert response.status_code == 401
    assert response.json()["detail"]["code"] == "refresh_token_reused"
    assert await count_sessions(session) == 0


async def test_an_unknown_token_is_refused_without_saying_why(client: AsyncClient) -> None:
    await register_and_login(client)

    client.cookies.set(settings.cookie_name, "made-up")
    response = await client.post("/api/v1/auth/refresh")

    assert response.status_code == 401
    # Not distinguishable from an expired one. Telling somebody which of their
    # guesses came close is help they should not get.
    assert response.json()["detail"]["code"] == "refresh_token_invalid"


async def test_without_a_cookie_there_is_nothing_to_refresh(client: AsyncClient) -> None:
    response = await client.post("/api/v1/auth/refresh")

    assert response.status_code == 401
    assert response.json()["detail"]["code"] == "refresh_token_missing"


async def test_an_expired_session_cannot_be_revived(
    client: AsyncClient, session: AsyncSession
) -> None:
    await register_and_login(client)

    row = await session.scalar(select(RefreshToken))
    assert row is not None
    row.expires_at = datetime.now(UTC) - timedelta(seconds=1)
    await session.commit()

    response = await client.post("/api/v1/auth/refresh")

    assert response.status_code == 401
    assert response.json()["detail"]["code"] == "refresh_token_invalid"
    # The row is cleared out on the way — an expired session is not worth keeping.
    assert await count_sessions(session) == 0


async def test_refreshing_pushes_the_expiry_back(
    client: AsyncClient, session: AsyncSession
) -> None:
    """This is what makes the month a month of inactivity rather than a hard limit."""
    await register_and_login(client)

    row = await session.scalar(select(RefreshToken))
    assert row is not None
    row.expires_at = datetime.now(UTC) + timedelta(days=2)
    await session.commit()
    kurz_vor_ablauf = row.expires_at

    assert (await client.post("/api/v1/auth/refresh")).status_code == 200

    row = await session.scalar(select(RefreshToken))
    assert row is not None
    assert row.expires_at > kurz_vor_ablauf
    assert row.last_used_at is not None


async def test_logout_takes_effect_at_once(client: AsyncClient, session: AsyncSession) -> None:
    await register_and_login(client)

    assert (await client.post("/api/v1/auth/logout")).status_code == 204
    assert await count_sessions(session) == 0

    # And the cookie is gone, so the next refresh has nothing to work with.
    response = await client.post("/api/v1/auth/refresh")
    assert response.status_code == 401


async def test_logout_without_a_session_is_not_an_error(client: AsyncClient) -> None:
    """Whoever asks to be signed out is signed out. There is nothing to report."""
    assert (await client.post("/api/v1/auth/logout")).status_code == 204


async def test_logout_everywhere_ends_the_other_devices(
    client: AsyncClient, session: AsyncSession
) -> None:
    email = "many-devices@example.org"
    await register_and_login(client, email)

    # A second and third sign-in stand for two more devices.
    for _ in range(2):
        response = await client.post(
            "/api/v1/auth/login", data={"username": email, "password": PASSWORD}
        )
        assert response.status_code == 200
    assert await count_sessions(session) == 3

    access = (
        await client.post("/api/v1/auth/login", data={"username": email, "password": PASSWORD})
    ).json()["access_token"]

    response = await client.post(
        "/api/v1/auth/logout-everywhere",
        headers={"Authorization": f"Bearer {access}"},
    )

    assert response.status_code == 204
    assert await count_sessions(session) == 0


async def test_signing_in_clears_out_expired_sessions(
    client: AsyncClient, session: AsyncSession
) -> None:
    """Otherwise the table grows with every abandoned login and nothing removes it."""
    email = "housekeeping@example.org"
    await register_and_login(client, email)

    row = await session.scalar(select(RefreshToken))
    assert row is not None
    row.expires_at = datetime.now(UTC) - timedelta(days=1)
    await session.commit()

    response = await client.post(
        "/api/v1/auth/login", data={"username": email, "password": PASSWORD}
    )
    assert response.status_code == 200

    # The dead one is gone, only the fresh one is left.
    assert await count_sessions(session) == 1


async def test_a_wrong_password_says_the_same_as_an_unknown_address(
    client: AsyncClient,
) -> None:
    """Separate messages would tell anybody which addresses are registered here."""
    email = "exists@example.org"
    await register_and_login(client, email)

    wrong_password = await client.post(
        "/api/v1/auth/login", data={"username": email, "password": "not-the-password"}
    )
    unknown_address = await client.post(
        "/api/v1/auth/login", data={"username": "nobody@example.org", "password": PASSWORD}
    )

    assert wrong_password.status_code == unknown_address.status_code == 400
    assert wrong_password.json() == unknown_address.json()
