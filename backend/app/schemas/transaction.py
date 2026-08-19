import uuid
from datetime import date
from decimal import Decimal

from pydantic import Field, model_validator

from app.models.enums import Block, Category
from app.schemas.base import Schema


class TransactionBase(Schema):
    account_id: uuid.UUID
    #: Set means a transfer to another own account.
    counter_account_id: uuid.UUID | None = None

    occurred_on: date
    #: Always positive. The direction comes from `block`, or from the two accounts
    #: on a transfer.
    amount: Decimal = Field(gt=0, max_digits=12, decimal_places=2)
    note: str | None = Field(default=None, max_length=200)

    #: Omittable on a pure transfer only.
    category: Category | None = None
    block: Block | None = None

    #: Set means it counts towards that position — independent of any transfer.
    position_id: uuid.UUID | None = None
    external_ref: str | None = Field(default=None, max_length=200)


class TransactionCreate(TransactionBase):
    @model_validator(mode="after")
    def check_shape(self) -> "TransactionCreate":
        """The same rules as the CHECK constraints, only earlier and with an error
        **code** the frontend can translate."""
        transfer = self.counter_account_id is not None

        if not transfer and (self.category is None or self.block is None):
            raise ValueError("purpose_required")

        if transfer and self.counter_account_id == self.account_id:
            raise ValueError("transfer_needs_two_accounts")

        return self


class TransactionUpdate(Schema):
    """Everything optional. The account stays editable — it is easy to pick the
    wrong one during quick entry."""

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
    #: Only set in the household view: who booked it. In your own book the
    #: information would be redundant.
    owner_name: str | None = None
    #: Created by ticking a position off. The frontend marks such bookings and
    #: warns before un-ticking removes them again.
    auto_booked: bool = False

    #: The other side, where the booking came out of an import. Read-only —
    #: nobody types this in, it is what the bank reported.
    counterparty_name: str | None = None
    counterparty_iban: str | None = None
