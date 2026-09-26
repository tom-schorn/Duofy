import secrets
import uuid
from datetime import datetime, timedelta

from sqlalchemy import DateTime, ForeignKey, String
from sqlalchemy.orm import Mapped, mapped_column

from app.db.base import Base
from app.models.mixins import TimestampMixin, UUIDMixin

#: How long an instance invitation stays valid. One rule instead of a setting.
INSTANCE_INVITATION_LIFETIME = timedelta(days=7)


class InstanceInvitation(UUIDMixin, TimestampMixin, Base):
    """A ticket to register on this instance — not to join a household.

    Only admins create them (see `app/api/v1/admin.py`), and they matter only while
    `REGISTRATION_MODE=invite`. The admin hands the link over themselves; Duofy
    sends no mail.

    **Revoking deletes the row.** A revoked invitation needs no history, and a
    missing row is the one state nobody can misread as valid.
    """

    __tablename__ = "instance_invitations"

    #: Random and not guessable; it is the credential inside the link.
    token: Mapped[str] = mapped_column(
        String(64), unique=True, index=True, default=lambda: secrets.token_urlsafe(32)
    )

    created_by_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("users.id", ondelete="CASCADE", name="fk_instance_invitations_created_by_id"),
        index=True,
    )

    #: Optional. When set, only this address can redeem the invitation.
    email: Mapped[str | None] = mapped_column(String(320), default=None)

    expires_at: Mapped[datetime] = mapped_column(DateTime(timezone=True))

    used_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), default=None)
    used_by_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("users.id", ondelete="SET NULL", name="fk_instance_invitations_used_by_id"),
        default=None,
    )
