import uuid

from sqlalchemy import Enum as SAEnum
from sqlalchemy import ForeignKey, String, UniqueConstraint
from sqlalchemy.orm import Mapped, mapped_column, relationship

from app.db.base import Base
from app.models.enums import Role
from app.models.mixins import TimestampMixin, UUIDMixin


class Household(UUIDMixin, TimestampMixin, Base):
    """Die Planungsebene für mehrere Personen.

    Der Haushalt **besitzt nichts** — keine Konten, keine Verträge, keine
    Posten. Er sagt nur, wer zusammen plant. Ein Nutzer kann in mehreren
    Haushalten sein (WG und Partnerin gleichzeitig).
    """

    __tablename__ = "households"

    name: Mapped[str] = mapped_column(String(100))

    members: Mapped[list["HouseholdMember"]] = relationship(
        back_populates="household", cascade="all, delete-orphan"
    )


class HouseholdMember(UUIDMixin, TimestampMixin, Base):
    """Verbindet Nutzer und Haushalt."""

    __tablename__ = "household_members"
    __table_args__ = (UniqueConstraint("household_id", "user_id", name="uq_household_member"),)

    household_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("households.id", ondelete="CASCADE"))
    user_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"))
    role: Mapped[Role] = mapped_column(SAEnum(Role, native_enum=False, length=20))

    household: Mapped[Household] = relationship(back_populates="members")
