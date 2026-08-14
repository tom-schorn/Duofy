import uuid
from datetime import datetime

from sqlalchemy import DateTime, ForeignKey, String
from sqlalchemy.orm import Mapped, mapped_column

from app.db.base import Base
from app.models.mixins import TimestampMixin, UUIDMixin


class RefreshToken(UUIDMixin, TimestampMixin, Base):
    """One long-lived session of one user on one device.

    The access token stays a short-lived JWT and is never stored anywhere on the
    client. This row is the long-lived half: it is what lets somebody come back
    weeks later without signing in again.

    **The token itself is not in here — only its hash.** Whoever reads this table
    holds nothing usable: a SHA-256 hash cannot be turned back into a token. A
    plaintext column would mean that one database dump hands out every open
    session.

    Rows are **deleted rather than flagged**. A session that has ended does not
    need a history; keeping it would only be one more place to check when deciding
    whether a token is valid.
    """

    __tablename__ = "refresh_tokens"

    #: The foreign key carries an explicit name. Without one, Alembic writes
    #: `drop_constraint(None, ...)` into the downgrade, and rolling the migration
    #: back fails.
    user_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("users.id", ondelete="CASCADE", name="fk_refresh_tokens_user_id_users"),
        index=True,
    )

    #: SHA-256 of the token, hex encoded — always 64 characters.
    #:
    #: Unique so that a collision cannot silently hand one session to another
    #: user, and indexed because every refresh looks a token up by it.
    token_hash: Mapped[str] = mapped_column(String(64), unique=True, index=True)

    #: The hash this token replaced, kept for exactly one step.
    #:
    #: Rotation means the previous token stops working. If it turns up anyway,
    #: that is not a mistake — a legitimate client has no reason to send a token it
    #: already exchanged. Somebody has a copy. Matching here therefore ends **every**
    #: session of that user rather than just refusing the request.
    #:
    #: One step back is enough to catch the realistic case: an attacker replaying a
    #: token they captured. Keeping the whole chain would cost a row per refresh for
    #: no additional insight.
    previous_hash: Mapped[str | None] = mapped_column(String(64), index=True, nullable=True)

    #: When this session ends for good. Rotation moves it along, so an active
    #: session keeps living; an abandoned one expires.
    expires_at: Mapped[datetime] = mapped_column(DateTime(timezone=True))

    #: When the token was last exchanged for a new one. Only there so a session
    #: list can say "last used two days ago" — the decision whether a token is
    #: valid never looks at it.
    last_used_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
