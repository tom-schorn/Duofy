"""Who may register (#168): the three modes, invitations, and the admin's limits."""

from datetime import UTC, datetime, timedelta

import pytest
from httpx import AsyncClient
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker

from app.core.config import Settings, settings
from app.models.instance_invitation import InstanceInvitation
from app.models.user import User
from app.services.admin import promote_admin

PASSWORD = "test-password-123"
ADMIN = "boss@example.org"


def payload(email: str, token: str | None = None) -> dict:
    body = {"email": email, "password": PASSWORD, "first_name": "Test", "last_name": "Person"}
    if token is not None:
        body["invitation_token"] = token
    return body


async def register(client: AsyncClient, email: str, token: str | None = None):
    return await client.post("/api/v1/auth/register", json=payload(email, token))


async def sign_in(client: AsyncClient, email: str) -> dict:
    response = await client.post(
        "/api/v1/auth/login", data={"username": email, "password": PASSWORD}
    )
    assert response.status_code == 200, response.text
    return {"Authorization": f"Bearer {response.json()['access_token']}"}


async def make_admin(client: AsyncClient, monkeypatch: pytest.MonkeyPatch) -> dict:
    """The first admin, the way a fresh closed instance gets one."""
    monkeypatch.setattr(settings, "admin_email", ADMIN)
    monkeypatch.setattr(settings, "registration_mode", "closed")
    assert (await register(client, ADMIN)).status_code == 201
    return await sign_in(client, ADMIN)


async def invite(client: AsyncClient, admin: dict, email: str | None = None) -> dict:
    response = await client.post("/api/v1/admin/invitations", json={"email": email}, headers=admin)
    assert response.status_code == 201, response.text
    return response.json()


def test_registration_defaults_to_invite_only() -> None:
    fresh = Settings(
        jwt_secret="x",
        postgres_host="h",
        postgres_db="d",
        postgres_user="u",
        postgres_password="p",
        _env_file=None,
    )

    assert fresh.registration_mode == "invite"


async def test_open_mode_registers_without_a_token(client: AsyncClient) -> None:
    assert (await register(client, "anyone@example.org")).status_code == 201


async def test_invite_mode_refuses_without_a_token(
    client: AsyncClient, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setattr(settings, "registration_mode", "invite")

    response = await register(client, "anyone@example.org")

    assert response.status_code == 403
    assert response.json()["detail"] == {"code": "invitation_required"}


async def test_invitation_works_once_and_is_then_used_up(
    client: AsyncClient, session: AsyncSession, monkeypatch: pytest.MonkeyPatch
) -> None:
    admin = await make_admin(client, monkeypatch)
    token = (await invite(client, admin))["token"]
    monkeypatch.setattr(settings, "registration_mode", "invite")

    first = await register(client, "first@example.org", token)
    second = await register(client, "second@example.org", token)

    assert first.status_code == 201
    assert second.status_code == 403
    assert second.json()["detail"] == {"code": "invitation_invalid"}
    invitation = await session.scalar(select(InstanceInvitation))
    await session.refresh(invitation)
    user = await session.scalar(select(User).where(User.email == "first@example.org"))
    assert invitation.used_at is not None
    assert invitation.used_by_id == user.id


async def test_expired_revoked_and_unknown_tokens_are_refused(
    client: AsyncClient, session: AsyncSession, monkeypatch: pytest.MonkeyPatch
) -> None:
    admin = await make_admin(client, monkeypatch)
    expired = await invite(client, admin)
    revoked = await invite(client, admin)
    monkeypatch.setattr(settings, "registration_mode", "invite")

    row = await session.scalar(
        select(InstanceInvitation).where(InstanceInvitation.token == expired["token"])
    )
    row.expires_at = datetime.now(UTC) - timedelta(seconds=1)
    await session.commit()
    gone = await client.delete(f"/api/v1/admin/invitations/{revoked['id']}", headers=admin)
    assert gone.status_code == 204

    for token in (expired["token"], revoked["token"], "not-a-real-token"):
        response = await register(client, "late@example.org", token)
        assert response.status_code == 403, token
        assert response.json()["detail"] == {"code": "invitation_invalid"}


async def test_an_invitation_lasts_exactly_seven_days(
    client: AsyncClient, session: AsyncSession, monkeypatch: pytest.MonkeyPatch
) -> None:
    admin = await make_admin(client, monkeypatch)

    await invite(client, admin)

    invitation = await session.scalar(select(InstanceInvitation))
    assert invitation.expires_at - invitation.created_at == timedelta(days=7)


async def test_an_invitation_for_an_address_only_admits_that_address(
    client: AsyncClient, monkeypatch: pytest.MonkeyPatch
) -> None:
    admin = await make_admin(client, monkeypatch)
    token = (await invite(client, admin, "meant@example.org"))["token"]
    monkeypatch.setattr(settings, "registration_mode", "invite")

    wrong = await register(client, "other@example.org", token)
    right = await register(client, "Meant@Example.org", token)

    assert wrong.status_code == 403
    assert right.status_code == 201


async def test_closed_mode_refuses_everyone_but_the_admin_address(
    client: AsyncClient, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setattr(settings, "registration_mode", "closed")
    monkeypatch.setattr(settings, "admin_email", ADMIN)

    refused = await register(client, "anyone@example.org")
    admitted = await register(client, ADMIN)

    assert refused.status_code == 403
    assert refused.json()["detail"] == {"code": "registration_closed"}
    assert admitted.status_code == 201
    assert admitted.json()["isSuperuser"] is True


async def test_the_admin_address_is_promoted_at_start(
    engine, session: AsyncSession, monkeypatch: pytest.MonkeyPatch
) -> None:
    session.add(
        User(email=ADMIN, hashed_password="x", first_name="A", last_name="B", is_superuser=False)
    )
    await session.commit()
    monkeypatch.setattr(settings, "admin_email", ADMIN.upper())

    await promote_admin(async_sessionmaker(engine, expire_on_commit=False))

    user = await session.scalar(select(User))
    await session.refresh(user)
    assert user.is_superuser is True


async def test_registering_cannot_make_somebody_admin(client: AsyncClient) -> None:
    response = await client.post(
        "/api/v1/auth/register", json={**payload("sneaky@example.org"), "is_superuser": True}
    )

    assert response.status_code == 201
    assert response.json()["isSuperuser"] is False


async def test_only_admins_manage_invitations(
    client: AsyncClient, monkeypatch: pytest.MonkeyPatch
) -> None:
    admin = await make_admin(client, monkeypatch)
    invitation = await invite(client, admin)
    monkeypatch.setattr(settings, "registration_mode", "open")
    await register(client, "plain@example.org")
    plain = await sign_in(client, "plain@example.org")

    attempts = [
        await client.post("/api/v1/admin/invitations", json={}, headers=plain),
        await client.get("/api/v1/admin/invitations", headers=plain),
        await client.delete(f"/api/v1/admin/invitations/{invitation['id']}", headers=plain),
    ]

    for response in attempts:
        assert response.status_code == 403
        assert response.json()["detail"] == {"code": "admin_required"}


async def test_an_admin_cannot_reach_other_peoples_accounts_plans_or_profile(
    client: AsyncClient, monkeypatch: pytest.MonkeyPatch
) -> None:
    """The system level runs the instance; it does not look into it."""
    admin = await make_admin(client, monkeypatch)
    monkeypatch.setattr(settings, "registration_mode", "open")
    other = (await register(client, "other@example.org")).json()["id"]
    other_headers = await sign_in(client, "other@example.org")
    account = await client.post(
        "/api/v1/accounts",
        json={"name": "Giro", "type": "checking", "opening_date": "2026-01-01"},
        headers=other_headers,
    )
    assert account.status_code == 201, account.text
    account_id = account.json()["id"]

    # The admin's own view of the world stays their own: empty.
    assert (await client.get("/api/v1/accounts", headers=admin)).json() == []
    assert (await client.get("/api/v1/transactions", headers=admin)).json() == []

    # Asking for somebody else's data needs a grant, and the role is no grant.
    for path in (
        f"/api/v1/accounts?owner={other}",
        f"/api/v1/transactions?owner={other}",
        f"/api/v1/plans/2026/9?owner={other}",
    ):
        response = await client.get(path, headers=admin)
        assert response.status_code == 403, path

    assert (
        await client.patch(
            f"/api/v1/accounts/{account_id}", json={"name": "Mine now"}, headers=admin
        )
    ).status_code in (403, 404)
    assert (
        await client.delete(f"/api/v1/accounts/{account_id}", headers=admin)
    ).status_code in (403, 404)

    # fastapi-users' routes by user id (read, rewrite the password, delete) are gone.
    for method in ("get", "patch", "delete"):
        response = await getattr(client, method)(f"/api/v1/users/{other}", headers=admin)
        assert response.status_code in (404, 405), method


async def test_the_sign_up_page_can_ask_for_the_mode(
    client: AsyncClient, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setattr(settings, "registration_mode", "closed")

    response = await client.get("/api/v1/auth/registration")

    assert response.json() == {"mode": "closed"}


async def test_nobody_can_take_over_the_admin_address_by_changing_their_email(
    client: AsyncClient, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setattr(settings, "admin_email", ADMIN)
    assert (await register(client, "plain@example.org")).status_code == 201
    plain = await sign_in(client, "plain@example.org")

    for wanted in (ADMIN, f"  {ADMIN.upper()}"):
        response = await client.patch("/api/v1/users/me", json={"email": wanted}, headers=plain)
        assert response.status_code == 400, wanted
        assert response.json()["detail"] == {"code": "email_reserved"}

    # The address is still free for the person it is reserved for.
    assert (await register(client, ADMIN)).status_code == 201
    admin = await sign_in(client, ADMIN)
    same = await client.patch("/api/v1/users/me", json={"firstName": "Boss"}, headers=admin)
    assert same.status_code == 200


async def test_privileged_fields_in_profile_and_registration_are_ignored(
    client: AsyncClient,
) -> None:
    registered = await client.post(
        "/api/v1/auth/register",
        json={**payload("odd@example.org"), "is_active": False, "is_verified": True},
    )
    assert registered.status_code == 201
    assert registered.json()["isActive"] is True
    assert registered.json()["isVerified"] is False

    headers = await sign_in(client, "odd@example.org")
    patched = await client.patch(
        "/api/v1/users/me", json={"isSuperuser": True, "isVerified": True}, headers=headers
    )
    assert patched.status_code == 200
    assert patched.json()["isSuperuser"] is False
    assert patched.json()["isVerified"] is False
