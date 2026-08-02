import uuid
from calendar import monthrange
from datetime import date
from decimal import Decimal

from sqlalchemy import CheckConstraint, Date, ForeignKey, Numeric, String
from sqlalchemy.orm import Mapped, mapped_column

from app.db.base import Base
from app.db.types import enum_column
from app.models.enums import Block, Category, CommitmentType, PaymentMethod, Rhythm
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
        # Ohne erste Fälligkeit wüsste die Generierung bei quartalsweise & Co.
        # weder in welchen Monaten noch ab welchem Jahr.
        CheckConstraint(
            "rhythm = 'monthly' OR first_due_date IS NOT NULL",
            name="ck_commitment_first_due_date_required",
        ),
    )

    owner_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"))

    type: Mapped[CommitmentType] = mapped_column(enum_column(CommitmentType))
    name: Mapped[str] = mapped_column(String(200))
    amount: Mapped[Decimal] = mapped_column(Numeric(12, 2))

    #: Wählt der Nutzer. BLOCK_SUGGESTION belegt das Feld im Frontend vor.
    #: Bei debt und savings_goal überstimmt resolve_block() die Wahl.
    category: Mapped[Category] = mapped_column(enum_column(Category))
    block: Mapped[Block] = mapped_column(enum_column(Block))

    #: NULL = privat. Gesetzt = erzeugte Posten wandern in diesen Haushaltsplan.
    #: Einmal entschieden, gilt für alle künftigen Monate.
    household_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("households.id", ondelete="SET NULL"), nullable=True
    )

    rhythm: Mapped[Rhythm] = mapped_column(enum_column(Rhythm))

    #: Wann es das erste Mal fällig wird — Tag, Monat **und Jahr**.
    #:
    #: Nur bei nicht-monatlichem Rhythmus, dort Pflicht (siehe CHECK oben).
    #: Aus dem Monat ergibt sich der Takt, aus dem Jahr der Beginn:
    #: GEZ mit 2026-02-15 + quarterly → Feb, Mai, Aug, Nov, erstmals 2026.
    first_due_date: Mapped[date | None] = mapped_column(Date, nullable=True)

    #: Tag im Monat, 1–31. Bei nicht-monatlichem Rhythmus derselbe Tag wie in
    #: `first_due_date` — `effective_due_day()` klemmt ihn je Monat ab.
    due_day: Mapped[int]

    active: Mapped[bool] = mapped_column(default=True)

    #: Von welchem Konto es abgeht. Leer = Standardkonto.
    #:
    #: Nötig, weil ein Vertrag nicht zwangsläufig vom Girokonto läuft: das
    #: Claude-Abo geht über die Kreditkarte, weil dort keine Lastschrift geht.
    #: Ohne diese Angabe bucht das Abhaken später auf das falsche Konto.
    #:
    #: SET NULL statt RESTRICT: löscht man ein Konto, soll der Vertrag bleiben
    #: und aufs Standardkonto zurückfallen.
    account_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("accounts.id", ondelete="SET NULL"), nullable=True
    )

    #: Wie gezahlt wird — gehört an den Vertrag, nicht an den einzelnen Monat.
    #: Wird beim Erzeugen in den Posten kopiert und ist dort überschreibbar,
    #: falls man ausnahmsweise überweist statt abbuchen zu lassen.
    #: Nullable: bei einem Sparziel gibt es oft keine.
    payment_method: Mapped[PaymentMethod | None] = mapped_column(
        enum_column(PaymentMethod), nullable=True
    )

    # nur bei type = savings_goal
    target_amount: Mapped[Decimal | None] = mapped_column(Numeric(12, 2), nullable=True)
    target_date: Mapped[date | None] = mapped_column(Date, nullable=True)

    # nur bei type = debt
    remaining_debt: Mapped[Decimal | None] = mapped_column(Numeric(12, 2), nullable=True)

    @property
    def first_month(self) -> int | None:
        """Ab welchem Monat der Takt zählt — steckt in `first_due_date`."""
        return self.first_due_date.month if self.first_due_date else None

    def is_due_in(self, year: int, month: int) -> bool:
        """Fällt diese Verpflichtung in diesem Monat an?

        Zwei Bedingungen, beide müssen stimmen:

        1. **Nach dem Beginn.** Vor `first_due_date` gibt es den Vertrag noch
           nicht — sonst entstünden rückwirkend Posten.
        2. **Im Takt.** Der Rhythmus läuft über den Jahreswechsel weiter:
           quartalsweise ab Juli heißt Jan, Apr, Jul, Okt — nicht nur Jul
           und Okt.
        """
        if not self.active:
            return False

        if self.first_due_date is not None:
            started = (year, month) >= (
                self.first_due_date.year,
                self.first_due_date.month,
            )
            if not started:
                return False

        if self.rhythm is Rhythm.MONTHLY:
            return True

        start = self.first_month or 1
        return (month - start) % self.rhythm.interval == 0

    def effective_due_day(self, year: int, month: int) -> int:
        """Der Tag, an dem es in diesem Monat wirklich fällig wird.

        Ein `due_day` von 31 existiert nur in sieben Monaten. Statt den Posten
        ausfallen zu lassen oder in den Folgemonat rutschen zu lassen, wandert
        er auf den letzten Tag — im Februar also auf den 28. bzw. 29.
        """
        return min(self.due_day, monthrange(year, month)[1])
