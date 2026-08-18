import uuid
from datetime import date
from decimal import Decimal

from pydantic import Field, model_validator

from app.models.enums import Block, Category, CommitmentType, PaymentMethod, Rhythm
from app.schemas.base import Schema


class CommitmentBase(Schema):
    name: str = Field(min_length=1, max_length=200)
    amount: Decimal = Field(ge=0, max_digits=12, decimal_places=2)
    category: Category
    block: Block
    household_id: uuid.UUID | None = None
    rhythm: Rhythm
    first_due_date: date | None = None
    due_day: int = Field(ge=1, le=31)
    active: bool = True
    #: Which account it is paid from. Empty means the default account.
    account_id: uuid.UUID | None = None
    #: Copied into the generated positions, overridable per month there.
    payment_method: PaymentMethod | None = None
    #: Where the money is saved to. Set means ticking off books a transfer.
    counter_account_id: uuid.UUID | None = None
    #: A pass-through position — counts towards no quota. Also copied.
    pass_through: bool = False

    # only for savings_goal
    target_amount: Decimal | None = Field(default=None, ge=0, max_digits=12, decimal_places=2)
    target_date: date | None = None

    # only for debt
    remaining_debt: Decimal | None = Field(default=None, ge=0, max_digits=12, decimal_places=2)


class CommitmentCreate(CommitmentBase):
    type: CommitmentType

    @model_validator(mode="after")
    def check_shape(self) -> "CommitmentCreate":
        """The same rules the database enforces, only earlier.

        A CHECK constraint yields a database error. Here the result is an error
        **code** the frontend can translate instead.
        """
        if self.rhythm is not Rhythm.MONTHLY and self.first_due_date is None:
            raise ValueError("first_due_date_required")

        if self.type is not CommitmentType.SAVINGS_GOAL and (
            self.target_amount is not None or self.target_date is not None
        ):
            raise ValueError("target_only_for_savings_goal")

        if self.type is not CommitmentType.DEBT and self.remaining_debt is not None:
            raise ValueError("remaining_debt_only_for_debt")

        # For a non-monthly rhythm the due day has to match the start date,
        # otherwise two fields contradict each other about the same thing.
        if self.first_due_date is not None and self.due_day != self.first_due_date.day:
            raise ValueError("due_day_must_match_first_due_date")

        return self


class CommitmentUpdate(Schema):
    """Everything optional. The type cannot be changed — create a new one instead."""

    name: str | None = Field(default=None, min_length=1, max_length=200)
    amount: Decimal | None = Field(default=None, ge=0, max_digits=12, decimal_places=2)
    category: Category | None = None
    block: Block | None = None
    household_id: uuid.UUID | None = None
    rhythm: Rhythm | None = None
    first_due_date: date | None = None
    due_day: int | None = Field(default=None, ge=1, le=31)
    active: bool | None = None
    account_id: uuid.UUID | None = None
    payment_method: PaymentMethod | None = None
    counter_account_id: uuid.UUID | None = None
    pass_through: bool | None = None
    target_amount: Decimal | None = Field(default=None, ge=0, max_digits=12, decimal_places=2)
    target_date: date | None = None
    remaining_debt: Decimal | None = Field(default=None, ge=0, max_digits=12, decimal_places=2)


class CommitmentRead(CommitmentBase):
    id: uuid.UUID
    type: CommitmentType
    owner_id: uuid.UUID

