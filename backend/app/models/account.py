import uuid
from datetime import date
from decimal import Decimal

from sqlalchemy import Date, ForeignKey, Index, Numeric, String, text
from sqlalchemy.orm import Mapped, mapped_column

from app.db.base import Base
from app.db.types import enum_column
from app.models.enums import AccountType
from app.models.mixins import TimestampMixin, UUIDMixin


class Account(UUIDMixin, TimestampMixin, Base):
    """Ein Zahlungskonto — Giro, Tagesgeld, Kreditkarte, PayPal, Bargeld.

    Gehört **immer genau einer Person**, wie alles andere in Duofy. Ein
    Gemeinschaftskonto gibt es bewusst nicht; käme es später dazu, wäre es ein
    nullable `household_id` wie bei den Posten.

    **Der Stand wird nicht gespeichert.** Er ergibt sich aus dem Anfangsbestand
    plus allen Buchungen danach — sonst müsste er bei jeder Änderung
    nachgeführt werden und driftet früher oder später auseinander.

    Depots sind hier nicht abgebildet: ihr Wert kommt von Kursen, nicht von
    Buchungen. Siehe `AccountType`.
    """

    __tablename__ = "accounts"

    __table_args__ = (
        # Höchstens ein Standardkonto je Person — als partieller Index, damit
        # die Datenbank es erzwingt und nicht die Anwendung daran denken muss.
        Index(
            "uq_account_one_default_per_owner",
            "owner_id",
            unique=True,
            postgresql_where=text("is_default"),
        ),
    )

    owner_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"))

    name: Mapped[str] = mapped_column(String(100))
    type: Mapped[AccountType] = mapped_column(enum_column(AccountType))

    #: Stand zum Stichtag darunter. Wer Duofy mitten im Leben anfängt, trägt
    #: hier den heutigen Stand ein und bucht ab da weiter.
    opening_balance: Mapped[Decimal] = mapped_column(
        Numeric(12, 2), default=Decimal("0.00")
    )
    #: **Wann** der Anfangsbestand galt. Ohne dieses Datum wäre der Stand zu
    #: einem beliebigen Zeitpunkt nicht berechenbar — man wüsste nicht, welche
    #: Buchungen schon darin enthalten sind.
    opening_date: Mapped[date] = mapped_column(Date)

    #: Wird bei der Schnelleingabe im Buch vorausgewählt. Das Konto ist dort
    #: Pflicht — ohne Vorauswahl müsste man es bei jedem Kioskkauf angeben.
    is_default: Mapped[bool] = mapped_column(default=False)

    #: Aufgelöste Konten bleiben stehen, damit alte Buchungen ihren Bezug
    #: behalten — sie tauchen nur nicht mehr in der Auswahl auf.
    active: Mapped[bool] = mapped_column(default=True)

    #: Kennung beim Anbieter, für den späteren Bankabruf über GoCardless.
    #: Jetzt kostenlos, später eine Migration auf einer gefüllten Tabelle.
    external_ref: Mapped[str | None] = mapped_column(String(200), nullable=True)
