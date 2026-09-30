import uuid
from datetime import date
from decimal import Decimal

from pydantic import Field, computed_field, model_validator

from app.models.enums import Budget, Category, TransactionKind
from app.schemas.base import Schema
from app.services.plan_month import is_fixed, is_unplanned, month_of


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


def check_plan_month_pair(year: int | None, month: int | None) -> None:
    """Year and month are one choice — one of them alone says nothing."""
    if (year is None) != (month is None):
        raise ValueError("plan_month_incomplete")


class TransactionCreate(TransactionBase):
    #: The plan month, only for a booking without a position (#239). Left out it is
    #: derived: the position's plan, otherwise the month of `occurred_on`. Whether
    #: a chosen month is allowed is decided in the endpoint, where the position
    #: and the date are known.
    plan_year: int | None = Field(default=None, ge=2000, le=2101)
    plan_month: int | None = Field(default=None, ge=1, le=12)

    @model_validator(mode="after")
    def check_shape(self) -> "TransactionCreate":
        """The same rules as the CHECK constraints, only earlier and with an error
        **code** the frontend can translate."""
        check_plan_month_pair(self.plan_year, self.plan_month)
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
    #: Choosing a plan month, see `TransactionCreate`.
    plan_year: int | None = Field(default=None, ge=2000, le=2101)
    plan_month: int | None = Field(default=None, ge=1, le=12)

    @model_validator(mode="after")
    def check_plan_month(self) -> "TransactionUpdate":
        check_plan_month_pair(self.plan_year, self.plan_month)
        return self


class TransactionRead(TransactionBase):
    id: uuid.UUID
    owner_id: uuid.UUID
    #: The month of the plan this booking counts in (#239), which can differ from
    #: the month of `occurred_on`.
    plan_year: int
    plan_month: int
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

    # Derived, never stored: the frontend reads them instead of repeating the rules
    # (#254).

    @computed_field
    @property
    def unplanned(self) -> bool:
        """Counts as "Ungeplant" in the plan and the book's filter."""
        return is_unplanned(
            self.kind, self.position_id, self.counter_account_id is not None
        )

    @computed_field
    @property
    def plan_month_fixed(self) -> bool:
        """The plan month cannot be chosen (#239)."""
        return is_fixed(
            self.kind, self.position_id, self.counter_account_id is not None
        )

    @computed_field
    @property
    def counts_elsewhere(self) -> bool:
        """Booked in one month, counting in another (a salary paid for the next)."""
        return (self.plan_year, self.plan_month) != month_of(self.occurred_on)
