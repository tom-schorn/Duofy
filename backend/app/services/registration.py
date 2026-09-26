"""Who may register, and what registering costs an invitation.

The rule comes from `REGISTRATION_MODE` (see `Settings`):

* `open`   — anybody
* `invite` — somebody holding a valid, unused, unexpired invitation, whose address
             matches if the invitation names one
* `closed` — nobody

`ADMIN_EMAIL` may register in every mode, so a fresh instance is never left
without an admin. That person becomes admin right there, see `is_admin_email`.

Every refusal is a code, never a sentence. Missing and wrong invitations get
different codes only where it helps the person (`invitation_required` tells the
page to show the field); *why* a token is wrong stays one code, so nobody can probe
which tokens exist.
"""

from datetime import UTC, datetime

from fastapi import HTTPException, status
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.config import settings
from app.models.instance_invitation import InstanceInvitation


def _refuse(code: str) -> HTTPException:
    return HTTPException(status.HTTP_403_FORBIDDEN, detail={"code": code})


async def admit(
    session: AsyncSession, email: str, token: str | None
) -> InstanceInvitation | None:
    """Decide whether `email` may register; return the invitation to redeem, if any.

    The invitation row is locked (`FOR UPDATE`) until the registration commits, so
    two people racing for one token cannot both get in.
    """
    if settings.is_admin_email(email) or settings.registration_mode == "open":
        return None
    if settings.registration_mode == "closed":
        raise _refuse("registration_closed")
    if not token:
        raise _refuse("invitation_required")

    invitation = await session.scalar(
        select(InstanceInvitation).where(InstanceInvitation.token == token).with_for_update()
    )
    if (
        invitation is None
        or invitation.used_at is not None
        or invitation.expires_at <= datetime.now(UTC)
        or (invitation.email is not None and invitation.email.lower() != email.strip().lower())
    ):
        raise _refuse("invitation_invalid")
    return invitation
