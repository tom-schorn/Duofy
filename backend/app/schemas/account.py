import uuid
from datetime import date
from decimal import Decimal

from pydantic import Field

from app.models.enums import AccountType
from app.schemas.base import Schema


class AccountBase(Schema):
    name: str = Field(min_length=1, max_length=100)
    type: AccountType
    opening_balance: Decimal = Field(default=Decimal("0.00"), max_digits=12, decimal_places=2)
    opening_date: date
    #: Wird bei der Schnelleingabe im Buch vorausgewählt. Höchstens eines je
    #: Person — setzt man ein zweites, verliert das alte die Markierung.
    is_default: bool = False
    active: bool = True
    external_ref: str | None = Field(default=None, max_length=200)


class AccountCreate(AccountBase):
    pass


class AccountUpdate(Schema):
    """Alles optional. Der Anfangsbestand bleibt änderbar — man tippt ihn
    beim Anlegen leicht falsch, und ohne Buchungen gibt es nichts, was daran
    hinge."""

    name: str | None = Field(default=None, min_length=1, max_length=100)
    type: AccountType | None = None
    opening_balance: Decimal | None = Field(default=None, max_digits=12, decimal_places=2)
    opening_date: date | None = None
    is_default: bool | None = None
    active: bool | None = None
    external_ref: str | None = Field(default=None, max_length=200)


class AccountRead(AccountBase):
    id: uuid.UUID
    owner_id: uuid.UUID

    #: Anfangsbestand plus alles, was seitdem gebucht wurde. Berechnet, nicht
    #: gespeichert — eine gespeicherte Zahl liefe irgendwann auseinander, und
    #: die Buchungen sind ohnehin die Wahrheit.
    balance: Decimal = Decimal("0.00")


class BalancePoint(Schema):
    """Ein Tag mit Bewegung, mit dem Stand an seinem Ende."""

    day: date
    balance: Decimal
    #: Was an diesem Tag zusammengerechnet passiert ist — die Höhe der Stufe.
    change: Decimal


class BalanceHistory(Schema):
    """Der Gesamtstand über einen Kalendermonat.

    `opening_balance` ist der Stand **vor** dem Ersten. Ohne ihn stünde die
    Kurve am Monatsanfang bei null und jeder Monat sähe aus wie ein Neustart.
    """

    opening_balance: Decimal
    closing_balance: Decimal
    points: list[BalancePoint]
