import uuid
from datetime import datetime
from decimal import Decimal

from sqlalchemy import CheckConstraint, DateTime, ForeignKey, Numeric, String, UniqueConstraint
from sqlalchemy import Enum as SAEnum
from sqlalchemy.orm import Mapped, mapped_column, relationship

from app.db.base import Base
from app.models.enums import Block, Category, PaymentMethod, PlanStatus
from app.models.mixins import TimestampMixin, UUIDMixin


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

    positions: Mapped[list["PlanPosition"]] = relationship(
        back_populates="plan", cascade="all, delete-orphan"
    )


class PlanPosition(UUIDMixin, TimestampMixin, Base):
    """Ein Posten in genau einem Monatsplan.

    Entsteht entweder aus einer Verpflichtung (`commitment_id` gesetzt) oder
    von Hand als Einmal-Posten.

    `household_id` entscheidet, ob der Posten privat bleibt oder in einen
    Haushaltsplan wandert — in **genau einen**.
    """

    __tablename__ = "plan_positions"
    __table_args__ = (CheckConstraint("due_day BETWEEN 1 AND 31", name="ck_position_due_day"),)

    plan_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("plans.id", ondelete="CASCADE"))

    #: Leer bei Einmal-Posten, die nicht aus einer Verpflichtung stammen.
    commitment_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("commitments.id", ondelete="SET NULL"), nullable=True
    )

    #: NULL = privat. Gesetzt = wandert in diesen Haushaltsplan.
    household_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("households.id", ondelete="SET NULL"), nullable=True
    )

    label: Mapped[str] = mapped_column(String(200))
    amount_planned: Mapped[Decimal] = mapped_column(Numeric(12, 2))
    amount_actual: Mapped[Decimal | None] = mapped_column(Numeric(12, 2), nullable=True)

    category: Mapped[Category] = mapped_column(SAEnum(Category, native_enum=False, length=20))
    #: Wird beim Anlegen abgeleitet und hier **gespeichert** — eine spätere
    #: Änderung der Zuordnung verändert keine bestehenden Pläne.
    block: Mapped[Block] = mapped_column(SAEnum(Block, native_enum=False, length=20))

    due_day: Mapped[int]
    payment_method: Mapped[PaymentMethod | None] = mapped_column(
        SAEnum(PaymentMethod, native_enum=False, length=20), nullable=True
    )

    #: Schützt manuelle Korrekturen davor, beim nächsten Generieren
    #: von der Verpflichtung überschrieben zu werden.
    manually_changed: Mapped[bool] = mapped_column(default=False)

    plan: Mapped[Plan] = relationship(back_populates="positions")
    changes: Mapped[list["PlanPositionChange"]] = relationship(
        back_populates="position", cascade="all, delete-orphan"
    )


class PlanPositionChange(UUIDMixin, TimestampMixin, Base):
    """Protokoll einer Änderung an einem Posten.

    Im gemeinsamen Haushalt dürfen beide Mitglieder Posten ändern, auch die
    des anderen. Damit hinterher nachvollziehbar bleibt, wer was angefasst
    hat, wird jede Änderung hier festgehalten.

    Werte werden als Text abgelegt — das Protokoll muss lesbar sein, nicht
    rechenbar.
    """

    __tablename__ = "plan_position_changes"

    position_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("plan_positions.id", ondelete="CASCADE")
    )
    changed_by_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"))

    field: Mapped[str] = mapped_column(String(50))
    old_value: Mapped[str | None] = mapped_column(String(200), nullable=True)
    new_value: Mapped[str | None] = mapped_column(String(200), nullable=True)

    position: Mapped[PlanPosition] = relationship(back_populates="changes")
