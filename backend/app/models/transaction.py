import uuid
from datetime import date
from decimal import Decimal

from sqlalchemy import CheckConstraint, Date, ForeignKey, Numeric, String
from sqlalchemy.orm import Mapped, mapped_column

from app.db.base import Base
from app.db.types import enum_column
from app.models.enums import Block, Category
from app.models.mixins import TimestampMixin, UUIDMixin


class Transaction(Base, UUIDMixin, TimestampMixin):
    """Eine Buchung im Haushaltsbuch — was tatsächlich geflossen ist.

    ## Kontowirkung und Budgetwirkung sind unabhängig

    Zwei Felder, zwei Fragen, die sich nicht gegenseitig bedingen:

    * **`counter_account_id`** entscheidet über die **Stände**. Ist es gesetzt,
      wandert das Geld von `account_id` dorthin — eine Umbuchung.
    * **`position_id`** entscheidet über das **Budget**. Ist es gesetzt, füllt
      die Buchung diesen Posten.

    Daraus fallen alle vier Fälle heraus, ohne Sonderregeln:

    | Umbuchung | Posten | Bedeutung |
    |---|---|---|
    | nein | ja  | Einkauf, zählt auf ein Budget |
    | nein | nein| Ausgabe ohne Planung — der Kiosk |
    | ja   | nein| PayPal aufladen, reines Schieben |
    | ja   | ja  | Geld aufs Tagesgeld legen — **erfüllt die Sparquote** |

    Der letzte Fall ist der Grund für diese Trennung. Würde man Umbuchungen
    pauschal aus dem Budget nehmen, erfüllte Sparen nie sein Soll.

    ## Richtung

    Kein Vorzeichen am Betrag. Bei einer normalen Buchung sagt `block`, wohin
    es geht: `income` ist Zufluss, alles andere Abfluss. Bei einer Umbuchung
    ist die Richtung durch die beiden Konten schon festgelegt.
    """

    __tablename__ = "transactions"

    __table_args__ = (
        # Eine normale Buchung hat immer einen Zweck. Nur eine Umbuchung darf
        # ohne auskommen — dort ist „wohin" die Antwort, nicht „wofür".
        CheckConstraint(
            "counter_account_id IS NOT NULL OR (category IS NOT NULL AND block IS NOT NULL)",
            name="ck_transaction_purpose_unless_transfer",
        ),
        # Von einem Konto auf dasselbe zu buchen ergibt keinen Sinn und würde
        # den Stand doppelt anfassen.
        CheckConstraint(
            "counter_account_id IS NULL OR counter_account_id <> account_id",
            name="ck_transaction_transfer_needs_two_accounts",
        ),
    )

    owner_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"))

    #: RESTRICT, nicht CASCADE: ein Konto mit Buchungen löscht man nicht, man
    #: setzt es auf `active = false`. Sonst verschwände Vergangenheit.
    account_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("accounts.id", ondelete="RESTRICT")
    )
    #: Gesetzt = Umbuchung. Das Zielkonto der Verschiebung.
    counter_account_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("accounts.id", ondelete="RESTRICT"), nullable=True
    )

    occurred_on: Mapped[date] = mapped_column(Date, index=True)
    amount: Mapped[Decimal] = mapped_column(Numeric(12, 2))
    note: Mapped[str | None] = mapped_column(String(200), nullable=True)

    #: Nullable, weil eine reine Umbuchung keinen Zweck hat. Siehe Constraint.
    category: Mapped[Category | None] = mapped_column(
        enum_column(Category), nullable=True
    )
    block: Mapped[Block | None] = mapped_column(enum_column(Block), nullable=True)

    #: SET NULL: wird ein Posten gelöscht, bleibt die Buchung bestehen. Das
    #: Geld ist ja trotzdem geflossen — nur die Zuordnung ist weg.
    position_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("plan_positions.id", ondelete="SET NULL"), nullable=True
    )

    #: Vom Abhaken erzeugt, nicht von Hand erfasst.
    #:
    #: Ein Posten kann mehrere Buchungen haben — beim Lebensmittel-Budget ist
    #: das der Normalfall. Ohne diese Markierung wüsste das Enthaken nicht,
    #: welche der Buchungen es wieder mitnehmen soll.
    auto_booked: Mapped[bool] = mapped_column(default=False)

    #: Kennung beim Anbieter — verhindert Dubletten beim späteren CSV- oder
    #: Bankimport. Jetzt kostenlos, später eine Migration auf echten Daten.
    external_ref: Mapped[str | None] = mapped_column(String(200), nullable=True)
