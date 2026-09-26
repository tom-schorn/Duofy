import uuid
from calendar import monthrange
from datetime import date
from decimal import Decimal

from sqlalchemy import CheckConstraint, Date, ForeignKey, Numeric, String
from sqlalchemy.orm import Mapped, mapped_column

from app.db.base import Base
from app.db.types import enum_column
from app.models.enums import (
    CATEGORY_LENGTH,
    Budget,
    Category,
    CommitmentType,
    PaymentMethod,
)
from app.models.mixins import TimestampMixin, UUIDMixin


class Commitment(UUIDMixin, TimestampMixin, Base):
    """A recurring commitment — contract, budget, savings goal or debt.

    All four are the same pattern: an amount that falls due in certain months and
    produces a position in the plan. They differ only in their type and in one or
    two extra fields, which is why they share a table.

    Belongs to **exactly one person**. Even in a shared flat a contract runs on
    whoever signed it.
    """

    __tablename__ = "commitments"
    __table_args__ = (
        CheckConstraint("due_day BETWEEN 1 AND 31", name="ck_commitment_due_day"),
        # Extra fields only on the matching type, enforced in the database so the
        # rule also holds for imports and direct SQL.
        CheckConstraint(
            "type = 'savings_goal' OR (target_amount IS NULL AND target_date IS NULL)",
            name="ck_commitment_target_only_for_savings_goal",
        ),
        CheckConstraint(
            "type = 'debt' OR remaining_debt IS NULL",
            name="ck_commitment_remaining_debt_only_for_debt",
        ),
        # Without a first due date the generator would know neither the months nor
        # the starting year for anything but a monthly recurrence.
        CheckConstraint(
            "interval_months = 1 OR first_due_date IS NOT NULL",
            name="ck_commitment_first_due_date_required",
        ),
        CheckConstraint(
            "interval_months BETWEEN 1 AND 120",
            name="ck_commitment_interval_months_range",
        ),
    )

    owner_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"))

    type: Mapped[CommitmentType] = mapped_column(enum_column(CommitmentType))
    name: Mapped[str] = mapped_column(String(200))
    amount: Mapped[Decimal] = mapped_column(Numeric(12, 2))

    #: The user's choice. BUDGET_SUGGESTION preselects it in the frontend; for
    #: `debt` and `savings_goal`, resolve_budget() overrides it.
    category: Mapped[Category] = mapped_column(enum_column(Category, length=CATEGORY_LENGTH))
    budget: Mapped[Budget] = mapped_column(enum_column(Budget))

    #: NULL means private. Set means generated positions appear in that household
    #: plan. Decided once, it applies to every future month.
    household_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("households.id", ondelete="SET NULL"), nullable=True
    )

    #: **Where** the money goes when it moves to another own account.
    #:
    #: Set on savings goals and repayments: money leaves the current account and
    #: lands on the savings account. Ticking the position off then books a
    #: **transfer** instead of an expense. Without it the money would vanish from
    #: the books — the source is correct, the target never grows, and the total
    #: drops by an amount that never left the household.
    #:
    #: Empty for everything that really leaves: rent, electricity, groceries.
    counter_account_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("accounts.id", ondelete="SET NULL"), nullable=True
    )

    #: A pass-through position — money that was never there to be spent.
    #:
    #: Earmarked benefits and refunds arrive and move straight on. They stay
    #: visible in the plan and they move the account balance, but they count
    #: **neither** towards the budget nor towards any quota.
    #:
    #: Without that distinction such an amount inflates the budget and the savings
    #: quota along with it: money merely passed on would look like money saved. The
    #: difference from ordinary saving is the decision — there one puts own money
    #: aside, here one forwards somebody else's.
    pass_through: Mapped[bool] = mapped_column(default=False)

    #: The planned amount is a **limit**, not a single payment.
    #:
    #: Rent is 890 and is paid once: it gets a tick, and the tick is the truth.
    #: Groceries are 600 and fill up over the month from single purchases: a tick
    #: there would claim August is finished because one receipt arrived. So a
    #: limit position carries no tick — what it shows is a fill level, and the
    #: month ends it.
    #:
    #: It sits on the commitment rather than on the position because groceries are
    #: planned every month. `create_plan` copies it onto each position, the way it
    #: copies `category` and `payment_method`.
    is_limit: Mapped[bool] = mapped_column(default=False)

    #: Every how many months it falls due, 1 to 120. Monthly is 1, quarterly 3,
    #: half-yearly 6, yearly 12 — and anything in between, because real contracts
    #: run every 2, 4 or 18 months. Counted from `first_due_date`.
    interval_months: Mapped[int]

    #: When it falls due for the first time — day, month **and year**.
    #:
    #: Only for a non-monthly recurrence, and mandatory there (see the CHECK above).
    #: The month defines the cadence, the year defines the start:
    #: 2026-02-15 every 3 months means Feb, May, Aug, Nov, starting in 2026.
    first_due_date: Mapped[date | None] = mapped_column(Date, nullable=True)

    #: Day of the month, 1–31. For a non-monthly recurrence the same day as in
    #: `first_due_date` — `effective_due_day()` clamps it per month.
    due_day: Mapped[int]

    active: Mapped[bool] = mapped_column(default=True)

    #: Which account it is paid from. Empty means the default account.
    #:
    #: Needed because a contract does not necessarily run off the current account
    #: — some are charged to a card because they accept nothing else. Without this
    #: field, ticking the position off would book against the wrong account.
    #:
    #: SET NULL rather than RESTRICT: deleting an account should keep the contract
    #: and let it fall back to the default.
    account_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("accounts.id", ondelete="SET NULL"), nullable=True
    )

    #: How it is paid — a property of the contract, not of a single month. Copied
    #: into the position on generation and overridable there, in case one transfers
    #: manually for once instead of letting it be debited.
    #: Nullable: a savings goal often has no payment method at all.
    payment_method: Mapped[PaymentMethod | None] = mapped_column(
        enum_column(PaymentMethod), nullable=True
    )

    # only for type = savings_goal
    target_amount: Mapped[Decimal | None] = mapped_column(Numeric(12, 2), nullable=True)
    target_date: Mapped[date | None] = mapped_column(Date, nullable=True)

    # only for type = debt
    remaining_debt: Mapped[Decimal | None] = mapped_column(Numeric(12, 2), nullable=True)

    @property
    def first_month(self) -> int | None:
        """The month the cadence counts from — taken from `first_due_date`."""
        return self.first_due_date.month if self.first_due_date else None

    def is_due_in(self, year: int, month: int) -> bool:
        """Does this commitment fall due in the given month?

        Two conditions, both have to hold:

        1. **After the start.** Before `first_due_date` the commitment does not
           exist yet, otherwise positions would appear retroactively.
        2. **On the cadence.** Months are counted absolutely from the start, so the
           cadence continues across the turn of the year: every 3 months from July
           means Jan, Apr, Jul, Oct — and every 5 months from November means April
           and September, which a count within the year could never say.
        """
        if not self.active:
            return False

        if self.first_due_date is None:
            # Only a monthly commitment may have no start (the CHECK on
            # `first_due_date`), so anything else here cannot occur — and is not due
            # rather than guessed at.
            return self.interval_months == 1

        start_total = self.first_due_date.year * 12 + self.first_due_date.month
        distance = year * 12 + month - start_total
        return distance >= 0 and distance % self.interval_months == 0

    def effective_due_day(self, year: int, month: int) -> int:
        """The day it actually falls due in the given month.

        A `due_day` of 31 exists in seven months only. Rather than dropping the
        position or sliding it into the next month, it moves to the last day of
        this one — the 28th or 29th in February.
        """
        return min(self.due_day, monthrange(year, month)[1])
