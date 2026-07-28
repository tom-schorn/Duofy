import uuid
from decimal import Decimal

from sqlalchemy import CheckConstraint, ForeignKey, Numeric, String
from sqlalchemy import Enum as SAEnum
from sqlalchemy.orm import Mapped, mapped_column, relationship

from app.db.base import Base
from app.models.enums import Block, Category, PaymentMethod
from app.models.mixins import TimestampMixin, UUIDMixin
from app.models.plan import Plan


class Position(UUIDMixin, TimestampMixin, Base):
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
    #: Wird aus der Kategorie vorbelegt und hier **gespeichert** — eine spätere
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
