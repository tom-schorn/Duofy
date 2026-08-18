import secrets
import uuid
from datetime import UTC, datetime, timedelta
from decimal import Decimal

from sqlalchemy import DateTime, ForeignKey, Numeric, String, UniqueConstraint
from sqlalchemy.orm import Mapped, mapped_column, relationship

from app.db.base import Base
from app.db.types import enum_column
from app.models.enums import AccessLevel, InvitationStatus, Role
from app.models.mixins import TimestampMixin, UUIDMixin

#: How long an invitation stays valid.
INVITATION_LIFETIME = timedelta(days=14)


class Household(UUIDMixin, TimestampMixin, Base):
    """The planning layer for several people.

    A household **owns nothing** — no accounts, no commitments, no positions. It
    only says who plans together. A user can belong to several households at once;
    that is the normal case, not an exception.

    The quotas exist here in addition to the ones on `Plan`: the household plan is
    not a table of its own, but it has its own 50/30/20. A personal plan and the
    shared plan may split differently.
    """

    __tablename__ = "households"

    name: Mapped[str] = mapped_column(String(100))

    #: Quotas in percent. Guidelines, not rules — same as on `Plan`.
    target_needs: Mapped[Decimal] = mapped_column(Numeric(5, 2), default=Decimal("50.00"))
    target_wants: Mapped[Decimal] = mapped_column(Numeric(5, 2), default=Decimal("30.00"))
    target_savings: Mapped[Decimal] = mapped_column(Numeric(5, 2), default=Decimal("20.00"))

    #: How many percent of the contributed income stays unplanned.
    buffer_percent: Mapped[Decimal] = mapped_column(Numeric(5, 2), default=Decimal("0.00"))

    members: Mapped[list["HouseholdMember"]] = relationship(
        back_populates="household", cascade="all, delete-orphan"
    )
    invitations: Mapped[list["HouseholdInvitation"]] = relationship(
        back_populates="household", cascade="all, delete-orphan"
    )


class HouseholdMember(UUIDMixin, TimestampMixin, Base):
    """Links a user to a household.

    Created either when the household is created — the creator becomes `owner` —
    or when a `HouseholdInvitation` is accepted.
    """

    __tablename__ = "household_members"
    __table_args__ = (UniqueConstraint("household_id", "user_id", name="uq_household_member"),)

    household_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("households.id", ondelete="CASCADE"))
    user_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"))
    role: Mapped[Role] = mapped_column(enum_column(Role))

    #: What this person allows the other members to see about themselves — one
    #: level per kind of data, because the three answers genuinely differ. Sharing
    #: the month you are planning is a small step; handing over the contracts
    #: behind it is a much larger one.
    #:
    #: All three default to `plan`: the others see the shared positions and nothing
    #: else — no book, no accounts, no contracts, no private positions. Only the
    #: person themselves can raise them, see `AccessLevel`.
    grants_plan: Mapped[AccessLevel] = mapped_column(
        enum_column(AccessLevel), default=AccessLevel.PLAN
    )
    grants_commitments: Mapped[AccessLevel] = mapped_column(
        enum_column(AccessLevel), default=AccessLevel.PLAN
    )
    #: Covers the book as well. An account you may look at comes with its bookings —
    #: a level that shows the balance but hides how it got there would be a riddle,
    #: not a permission.
    grants_accounts: Mapped[AccessLevel] = mapped_column(
        enum_column(AccessLevel), default=AccessLevel.PLAN
    )

    household: Mapped[Household] = relationship(back_populates="members")


class HouseholdInvitation(UUIDMixin, TimestampMixin, Base):
    """A pending invitation into a household.

    Needed because `HouseholdMember` can only link users that already exist,
    while the normal case is that the invited person has no account yet.

    The invitation therefore targets an **email address**, not a user. Whoever
    signs up with that address finds the invitation waiting for them.
    """

    __tablename__ = "household_invitations"
    __table_args__ = (
        # One pending invitation per address and household. Declined and accepted
        # ones do not collide because they carry a different status.
        UniqueConstraint(
            "household_id", "email", "status", name="uq_invitation_household_email_status"
        ),
    )

    household_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("households.id", ondelete="CASCADE"))
    invited_by_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"))

    email: Mapped[str] = mapped_column(String(320), index=True)

    #: Random value identifying the invitation. Not guessable.
    token: Mapped[str] = mapped_column(
        String(64),
        unique=True,
        index=True,
        default=lambda: secrets.token_urlsafe(32),
    )

    status: Mapped[InvitationStatus] = mapped_column(
        enum_column(InvitationStatus),
        default=InvitationStatus.PENDING,
    )

    expires_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True),
        default=lambda: datetime.now(UTC) + INVITATION_LIFETIME,
    )

    household: Mapped[Household] = relationship(back_populates="invitations")

    @property
    def is_open(self) -> bool:
        """Can this invitation still be accepted?"""
        return self.status is InvitationStatus.PENDING and self.expires_at > datetime.now(UTC)
