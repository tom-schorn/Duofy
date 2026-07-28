import uuid
from datetime import datetime
from decimal import Decimal
from typing import TYPE_CHECKING

from sqlalchemy import CheckConstraint, DateTime, ForeignKey, Numeric, UniqueConstraint
from sqlalchemy import Enum as SAEnum
from sqlalchemy.orm import Mapped, mapped_column, relationship

from app.db.base import Base
from app.models.enums import PlanStatus
from app.models.mixins import TimestampMixin, UUIDMixin

if TYPE_CHECKING:
    from app.models.position import Position


class Plan(UUIDMixin, TimestampMixin, Base):
    """Der Monatsplan einer Person.

    Gehört immer einer Person, nie einem Haushalt. Der Haushaltsplan ist
    keine eigene Tabelle — er ist die Zusammenstellung aller Posten aller
    Mitglieder, bei denen `household_id` gesetzt ist.

    Die Quoten sind **Richtwerte**, keine Regel: es gibt ein Soll, daneben
    steht das Ist, und man schaut dass es passt.
    """

    __tablename__ = "plans"
    __table_args__ = (
        UniqueConstraint("user_id", "year", "month", name="uq_plan_user_month"),
        CheckConstraint("month BETWEEN 1 AND 12", name="ck_plan_month"),
    )

    user_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"))

    year: Mapped[int]
    month: Mapped[int]
    status: Mapped[PlanStatus] = mapped_column(
        SAEnum(PlanStatus, native_enum=False, length=20), default=PlanStatus.DRAFT
    )

    target_needs: Mapped[Decimal] = mapped_column(Numeric(5, 2), default=Decimal("50.00"))
    target_wants: Mapped[Decimal] = mapped_column(Numeric(5, 2), default=Decimal("30.00"))
    target_savings: Mapped[Decimal] = mapped_column(Numeric(5, 2), default=Decimal("20.00"))

    #: Wieviel Prozent der Einnahmen bewusst unverplant bleiben.
    buffer_percent: Mapped[Decimal] = mapped_column(Numeric(5, 2), default=Decimal("0.00"))

    confirmed_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)

    positions: Mapped[list["Position"]] = relationship(
        back_populates="plan", cascade="all, delete-orphan"
    )
