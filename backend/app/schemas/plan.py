import uuid
from datetime import date, datetime
from decimal import Decimal

from pydantic import Field

from app.models.enums import Block, Category, PaymentMethod
from app.schemas.base import Schema


class PositionBase(Schema):
    label: str = Field(min_length=1, max_length=200)
    amount_planned: Decimal = Field(ge=0, max_digits=12, decimal_places=2)
    amount_actual: Decimal | None = Field(default=None, ge=0, max_digits=12, decimal_places=2)
    category: Category
    block: Block
    due_day: int = Field(ge=1, le=31)
    #: Copied from the commitment, overridable per month. Empty means the default.
    account_id: uuid.UUID | None = None
    payment_method: PaymentMethod | None = None
    household_id: uuid.UUID | None = None
    #: Fills up from bookings instead of being ticked off.
    is_budget: bool = False
    #: Where the money is saved to. Set means ticking off books a transfer.
    counter_account_id: uuid.UUID | None = None
    #: Passes through only — counts towards no quota and no budget.
    pass_through: bool = False


class PositionCreate(PositionBase):
    pass


class PositionUpdate(Schema):
    """Everything optional — `paid_at` is set through its own endpoint."""

    label: str | None = Field(default=None, min_length=1, max_length=200)
    amount_planned: Decimal | None = Field(default=None, ge=0, max_digits=12, decimal_places=2)
    amount_actual: Decimal | None = Field(default=None, ge=0, max_digits=12, decimal_places=2)
    category: Category | None = None
    block: Block | None = None
    due_day: int | None = Field(default=None, ge=1, le=31)
    account_id: uuid.UUID | None = None
    payment_method: PaymentMethod | None = None
    household_id: uuid.UUID | None = None
    is_budget: bool | None = None
    counter_account_id: uuid.UUID | None = None
    pass_through: bool | None = None


class PositionPaid(Schema):
    """What ticking off books. Both optional — empty means today, as planned.

    The date is here because the day of payment and the month of the plan often
    differ: income for August can arrive at the end of July. The position stays in
    August, the booking carries its real date.
    """

    occurred_on: date | None = None
    amount: Decimal | None = Field(default=None, gt=0, max_digits=12, decimal_places=2)


class PositionRead(PositionBase):
    id: uuid.UUID
    plan_id: uuid.UUID
    #: Empty on one-off positions, set when generated from a commitment.
    commitment_id: uuid.UUID | None
    #: NULL means still open.
    paid_at: datetime | None


class PlanBase(Schema):
    target_needs: Decimal = Field(ge=0, le=100)
    target_wants: Decimal = Field(ge=0, le=100)
    target_savings: Decimal = Field(ge=0, le=100)
    buffer_percent: Decimal = Field(ge=0, le=100)


class PlanCreate(Schema):
    """Create a month.

    Deliberately **no** "copy last month": the recurring part comes from the
    commitments, one-off items are entered by hand.
    """

    year: int = Field(ge=2000, le=2100)
    month: int = Field(ge=1, le=12)


class PlanUpdate(Schema):
    target_needs: Decimal | None = Field(default=None, ge=0, le=100)
    target_wants: Decimal | None = Field(default=None, ge=0, le=100)
    target_savings: Decimal | None = Field(default=None, ge=0, le=100)
    buffer_percent: Decimal | None = Field(default=None, ge=0, le=100)


class BudgetTotals(Schema):
    needs: Decimal
    wants: Decimal
    savings: Decimal


class PlanSummary(PlanBase):
    """One row in the plan overview.

    The totals arrive ready-made from the backend — the overview must not load
    every position of every month just to add them up.
    """

    year: int
    month: int

    #: Sum of the income positions.
    income: Decimal
    #: Income minus buffer — the basis the quotas are computed on.
    #: **Not** the same as what is left to allocate; that is the remainder of it.
    budget: Decimal
    #: Allocated per block.
    spent: BudgetTotals
    #: Sum of the positions that are not ticked off yet.
    unpaid: Decimal
    #: Households that positions of this plan feed into. Empty means fully private.
    household_ids: list[uuid.UUID]


class PlanRead(PlanSummary):
    id: uuid.UUID
    positions: list[PositionRead]


class HouseholdPositionRead(PositionRead):
    """A position in the shared plan — including the person behind it.

    In your own plan that would be redundant; here it is the point. "Who carries
    what" is the question a shared plan exists to answer.
    """

    owner_id: uuid.UUID
    #: The first name is enough — the surname is in the member list already.
    owner_name: str


class HouseholdPlanRead(PlanSummary):
    """The shared plan. Composed, not stored — hence no `id`: there is no row behind
    it, only the positions of every member that carry this `household_id`."""

    household_id: uuid.UUID
    household_name: str
    positions: list[HouseholdPositionRead]

