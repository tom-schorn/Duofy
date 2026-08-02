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

    # Kein `balance`: der Stand ergibt sich aus Anfangsbestand plus Buchungen,
    # und Buchungen gibt es noch nicht. Ein Feld, das jetzt nur den
    # Anfangsbestand wiederholt, wäre eine Behauptung.
