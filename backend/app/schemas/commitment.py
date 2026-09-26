import uuid
from datetime import date
from decimal import Decimal

from pydantic import Field, field_validator, model_validator

from app.models.enums import Budget, Category, CommitmentType, PaymentMethod
from app.schemas.base import Schema

MIN_INTERVAL_MONTHS = 1
MAX_INTERVAL_MONTHS = 120


def month_of(day: date) -> tuple[int, int]:
    return (day.year, day.month)


def check_interval(months: int) -> None:
    """The same range the database enforces, but as an error code."""
    if not MIN_INTERVAL_MONTHS <= months <= MAX_INTERVAL_MONTHS:
        raise ValueError("interval_months_out_of_range")


class CommitmentBase(Schema):
    name: str = Field(min_length=1, max_length=200)
    amount: Decimal = Field(ge=0, max_digits=12, decimal_places=2)
    category: Category
    budget: Budget
    household_id: uuid.UUID | None = None
    #: Every how many months it falls due, 1 to 120. Checked in the validators.
    interval_months: int
    #: The start of the cadence and, through its day, the due day.
    first_due_date: date
    #: The last month it falls due. Empty means it runs indefinitely.
    ends_on: date | None = None
    #: Which account it is paid from. Empty means the default account.
    account_id: uuid.UUID | None = None
    #: Copied into the generated positions, overridable per month there.
    payment_method: PaymentMethod | None = None
    #: The amount is a limit that fills up from bookings, not a single payment
    #: that gets ticked off. Copied into the generated positions.
    is_limit: bool = False
    #: Where the money is saved to. Set means ticking off books a transfer.
    counter_account_id: uuid.UUID | None = None
    #: A pass-through position — counts towards no quota. Also copied.
    pass_through: bool = False

    # only for savings_goal
    target_amount: Decimal | None = Field(default=None, ge=0, max_digits=12, decimal_places=2)
    target_date: date | None = None


class CommitmentCreate(CommitmentBase):
    type: CommitmentType

    @model_validator(mode="after")
    def check_shape(self) -> "CommitmentCreate":
        """The same rules the database enforces, only earlier.

        A CHECK constraint yields a database error. Here the result is an error
        **code** the frontend can translate instead.
        """
        check_interval(self.interval_months)

        if self.ends_on is not None and month_of(self.ends_on) < month_of(self.first_due_date):
            raise ValueError("ends_on_before_start")

        if self.type is not CommitmentType.SAVINGS_GOAL and (
            self.target_amount is not None or self.target_date is not None
        ):
            raise ValueError("target_only_for_savings_goal")

        return self


class CommitmentUpdate(Schema):
    """Everything optional. The type cannot be changed — create a new one instead."""

    name: str | None = Field(default=None, min_length=1, max_length=200)
    amount: Decimal | None = Field(default=None, ge=0, max_digits=12, decimal_places=2)
    category: Category | None = None
    budget: Budget | None = None
    household_id: uuid.UUID | None = None
    interval_months: int | None = None
    first_due_date: date | None = None
    ends_on: date | None = None
    account_id: uuid.UUID | None = None
    payment_method: PaymentMethod | None = None
    is_limit: bool | None = None
    counter_account_id: uuid.UUID | None = None
    pass_through: bool | None = None
    target_amount: Decimal | None = Field(default=None, ge=0, max_digits=12, decimal_places=2)
    target_date: date | None = None

    @field_validator("interval_months")
    @classmethod
    def check_interval_months(cls, value: int | None) -> int | None:
        # An explicit null is left to the endpoint, which rejects it as `null_not_allowed`.
        if value is not None:
            check_interval(value)
        return value


class CommitmentRead(CommitmentBase):
    id: uuid.UUID
    type: CommitmentType
    owner_id: uuid.UUID
    #: No month position refers to it yet, so it may still be deleted. Computed.
    deletable: bool = False

