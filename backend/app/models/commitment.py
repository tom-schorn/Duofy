import uuid
from datetime import date
from decimal import Decimal

from sqlalchemy import CheckConstraint, Date, ForeignKey, Numeric, String
from sqlalchemy import Enum as SAEnum
from sqlalchemy.orm import Mapped, mapped_column

from app.db.base import Base
from app.models.enums import Block, Category, CommitmentType, Rhythm
from app.models.mixins import TimestampMixin, UUIDMixin


class Commitment(UUIDMixin, TimestampMixin, Base):
    """Eine wiederkehrende Verpflichtung — Vertrag, Sparziel oder Schuld.

    Alle drei sind dasselbe Muster: ein Betrag, der in bestimmten Monaten
    fällig wird und daraus einen Posten im Plan erzeugt. Sie unterscheiden
    sich nur im Typ und in ein, zwei Zusatzfeldern.

    Gehört **immer genau einer Person** — auch in einer WG läuft der Vertrag
    auf den, der ihn unterschrieben hat.
    """

    __tablename__ = "commitments"
    __table_args__ = (
        CheckConstraint("due_day BETWEEN 1 AND 31", name="ck_commitment_due_day"),
        CheckConstraint(
            "first_month IS NULL OR first_month BETWEEN 1 AND 12",
            name="ck_commitment_first_month",
        ),
        # Zusatzfelder nur beim passenden Typ — in der Datenbank erzwungen,
        # damit sie auch bei Import oder direktem SQL nicht verrutschen.
        CheckConstraint(
            "type = 'savings_goal' OR (target_amount IS NULL AND target_date IS NULL)",
            name="ck_commitment_target_only_for_savings_goal",
        ),
        CheckConstraint(
            "type = 'debt' OR remaining_debt IS NULL",
            name="ck_commitment_remaining_debt_only_for_debt",
        ),
        CheckConstraint(
            "rhythm = 'monthly' OR first_month IS NOT NULL",
            name="ck_commitment_first_month_required",
        ),
    )

    owner_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"))

    type: Mapped[CommitmentType] = mapped_column(
        SAEnum(CommitmentType, native_enum=False, length=20)
    )
    name: Mapped[str] = mapped_column(String(200))
    amount: Mapped[Decimal] = mapped_column(Numeric(12, 2))

    #: Wählt der Nutzer. BLOCK_SUGGESTION belegt das Feld im Frontend vor.
    #: Bei debt und savings_goal überstimmt resolve_block() die Wahl.
    category: Mapped[Category] = mapped_column(SAEnum(Category, native_enum=False, length=20))
    block: Mapped[Block] = mapped_column(SAEnum(Block, native_enum=False, length=20))

    #: NULL = privat. Gesetzt = erzeugte Posten wandern in diesen Haushaltsplan.
    #: Einmal entschieden, gilt für alle künftigen Monate.
    household_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("households.id", ondelete="SET NULL"), nullable=True
    )

    rhythm: Mapped[Rhythm] = mapped_column(SAEnum(Rhythm, native_enum=False, length=20))
    #: Ab welchem Monat der Rhythmus zählt — nur bei nicht-monatlich.
    #: GEZ: quarterly + 2 → Feb, Mai, Aug, Nov. AVD: annual + 9 → September.
    first_month: Mapped[int | None] = mapped_column(nullable=True)
    due_day: Mapped[int]

    active: Mapped[bool] = mapped_column(default=True)

    # nur bei type = savings_goal
    target_amount: Mapped[Decimal | None] = mapped_column(Numeric(12, 2), nullable=True)
    target_date: Mapped[date | None] = mapped_column(Date, nullable=True)

    # nur bei type = debt
    remaining_debt: Mapped[Decimal | None] = mapped_column(Numeric(12, 2), nullable=True)

    def is_due_in(self, month: int) -> bool:
        """Fällt diese Verpflichtung im gegebenen Monat an?"""
        if self.rhythm is Rhythm.MONTHLY:
            return True
        start = self.first_month or 1
        return (month - start) % self.rhythm.interval == 0
