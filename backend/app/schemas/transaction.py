import uuid
from datetime import date
from decimal import Decimal

from pydantic import Field, model_validator

from app.models.enums import Block, Category
from app.schemas.base import Schema


class TransactionBase(Schema):
    account_id: uuid.UUID
    #: Gesetzt = Umbuchung auf ein eigenes Konto.
    counter_account_id: uuid.UUID | None = None

    occurred_on: date
    #: Immer positiv. Die Richtung kommt aus `block`, bei einer Umbuchung aus
    #: den beiden Konten.
    amount: Decimal = Field(gt=0, max_digits=12, decimal_places=2)
    note: str | None = Field(default=None, max_length=200)

    #: Nur bei einer reinen Umbuchung weglassbar.
    category: Category | None = None
    block: Block | None = None

    #: Gesetzt = zählt auf diesen Posten. Unabhängig davon, ob umgebucht wird.
    position_id: uuid.UUID | None = None
    external_ref: str | None = Field(default=None, max_length=200)


class TransactionCreate(TransactionBase):
    @model_validator(mode="after")
    def check_shape(self) -> "TransactionCreate":
        """Dieselben Regeln wie die CHECK-Constraints — nur früher und mit
        einem Fehler-**Code**, den das Frontend übersetzen kann."""
        transfer = self.counter_account_id is not None

        if not transfer and (self.category is None or self.block is None):
            raise ValueError("purpose_required")

        if transfer and self.counter_account_id == self.account_id:
            raise ValueError("transfer_needs_two_accounts")

        return self


class TransactionUpdate(Schema):
    """Alles optional. Das Konto bleibt änderbar — man greift beim schnellen
    Erfassen leicht daneben."""

    account_id: uuid.UUID | None = None
    counter_account_id: uuid.UUID | None = None
    occurred_on: date | None = None
    amount: Decimal | None = Field(default=None, gt=0, max_digits=12, decimal_places=2)
    note: str | None = Field(default=None, max_length=200)
    category: Category | None = None
    block: Block | None = None
    position_id: uuid.UUID | None = None
    external_ref: str | None = Field(default=None, max_length=200)


class TransactionRead(TransactionBase):
    id: uuid.UUID
    owner_id: uuid.UUID
