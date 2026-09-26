import uuid
from datetime import date
from decimal import Decimal

from pydantic import Field, model_validator

from app.models.enums import Budget, Category, TransactionKind
from app.schemas.base import Schema


class TransactionBase(Schema):
    account_id: uuid.UUID
    #: Set means a transfer to another own account.
    counter_account_id: uuid.UUID | None = None

    #: A booking, or the balance an account starts a month with (#94).
    kind: TransactionKind = TransactionKind.BOOKING

    occurred_on: date
    #: Positive on a booking — the direction comes from `budget`, or from the two
    #: accounts on a transfer. Only a carry-over carries a sign. Checked in
    #: `check_shape`, because the bound depends on `kind`.
    amount: Decimal = Field(max_digits=12, decimal_places=2)
    note: str | None = Field(default=None, max_length=200)

    #: Omittable on a pure transfer only.
    category: Category | None = None
    budget: Budget | None = None

    #: Set means it counts towards that position — independent of any transfer.
    position_id: uuid.UUID | None = None
    external_ref: str | None = Field(default=None, max_length=200)


class TransactionCreate(TransactionBase):
    @model_validator(mode="after")
    def check_shape(self) -> "TransactionCreate":
        """The same rules as the CHECK constraints, only earlier and with an error
        **code** the frontend can translate."""
        if self.kind is TransactionKind.CARRY_OVER:
            if (
                self.category is not None
                or self.budget is not None
                or self.position_id is not None
                or self.counter_account_id is not None
            ):
                raise ValueError("carry_over_is_bare")
            if self.occurred_on.day != 1:
                raise ValueError("carry_over_needs_first_of_month")
            return self

        if self.amount <= 0:
            raise ValueError("amount_must_be_positive")

        transfer = self.counter_account_id is not None

        if not transfer and (self.category is None or self.budget is None):
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
    #: The bound (positive, except on a carry-over) is checked against the stored
    #: `kind` in the endpoint — `kind` itself cannot be changed.
    amount: Decimal | None = Field(default=None, max_digits=12, decimal_places=2)
    note: str | None = Field(default=None, max_length=200)
    category: Category | None = None
    budget: Budget | None = None
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
