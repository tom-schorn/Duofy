import secrets
import uuid
from datetime import UTC, datetime, timedelta
from decimal import Decimal

from sqlalchemy import DateTime, ForeignKey, Numeric, String, UniqueConstraint
from sqlalchemy.orm import Mapped, mapped_column, relationship

from app.db.base import Base
from app.db.types import enum_column
from app.models.enums import InvitationStatus, Role
from app.models.mixins import TimestampMixin, UUIDMixin

#: Wie lange eine Einladung gültig bleibt.
INVITATION_LIFETIME = timedelta(days=14)


class Household(UUIDMixin, TimestampMixin, Base):
    """Die Planungsebene für mehrere Personen.

    Der Haushalt **besitzt nichts** — keine Konten, keine Verträge, keine
    Posten. Er sagt nur, wer zusammen plant. Ein Nutzer kann in mehreren
    Haushalten sein (WG und Partnerin gleichzeitig).

    Die Quoten liegen hier zusätzlich zu denen auf `Plan`: der Haushaltsplan
    ist keine eigene Tabelle, hat aber ein eigenes 50/30/20. Toms Plan und der
    gemeinsame Plan dürfen unterschiedlich aufteilen.
    """

    __tablename__ = "households"

    name: Mapped[str] = mapped_column(String(100))

    #: Quoten in Prozent. Richtwerte, keine Regel — wie bei `Plan`.
    target_needs: Mapped[Decimal] = mapped_column(Numeric(5, 2), default=Decimal("50.00"))
    target_wants: Mapped[Decimal] = mapped_column(Numeric(5, 2), default=Decimal("30.00"))
    target_savings: Mapped[Decimal] = mapped_column(Numeric(5, 2), default=Decimal("20.00"))

    #: Wieviel Prozent der eingebrachten Einnahmen unverplant bleiben.
    buffer_percent: Mapped[Decimal] = mapped_column(Numeric(5, 2), default=Decimal("0.00"))

    members: Mapped[list["HouseholdMember"]] = relationship(
        back_populates="household", cascade="all, delete-orphan"
    )
    invitations: Mapped[list["HouseholdInvitation"]] = relationship(
        back_populates="household", cascade="all, delete-orphan"
    )


class HouseholdMember(UUIDMixin, TimestampMixin, Base):
    """Verbindet Nutzer und Haushalt.

    Entsteht entweder beim Anlegen (der Ersteller wird `owner`) oder beim
    Annehmen einer `HouseholdInvitation`.
    """

    __tablename__ = "household_members"
    __table_args__ = (UniqueConstraint("household_id", "user_id", name="uq_household_member"),)

    household_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("households.id", ondelete="CASCADE"))
    user_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"))
    role: Mapped[Role] = mapped_column(enum_column(Role))

    household: Mapped[Household] = relationship(back_populates="members")


class HouseholdInvitation(UUIDMixin, TimestampMixin, Base):
    """Eine offene Einladung in einen Haushalt.

    Nötig, weil `HouseholdMember` nur bestehende Nutzer verknüpft. Der
    Normalfall ist aber, dass die eingeladene Person noch gar kein Konto hat —
    sie bekommt einen Link, registriert sich und nimmt damit an.

    Die Einladung geht an eine **E-Mail**, nicht an einen Nutzer: zum Zeitpunkt
    des Einladens gibt es den Nutzer eventuell noch nicht.
    """

    __tablename__ = "household_invitations"
    __table_args__ = (
        # Pro Haushalt nur eine offene Einladung je Adresse. Abgelehnte und
        # angenommene stören nicht, weil sie einen anderen Status tragen.
        UniqueConstraint(
            "household_id", "email", "status", name="uq_invitation_household_email_status"
        ),
    )

    household_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("households.id", ondelete="CASCADE"))
    invited_by_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"))

    email: Mapped[str] = mapped_column(String(320), index=True)

    #: Zufälliger Wert für den Einladungslink. Nicht erratbar.
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
        """Kann diese Einladung noch angenommen werden?"""
        return self.status is InvitationStatus.PENDING and self.expires_at > datetime.now(UTC)
