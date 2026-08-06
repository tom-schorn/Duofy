import uuid
from calendar import monthrange
from datetime import date
from decimal import Decimal

from sqlalchemy import CheckConstraint, Date, ForeignKey, Numeric, String
from sqlalchemy.orm import Mapped, mapped_column

from app.db.base import Base
from app.db.types import enum_column
from app.models.enums import Block, Category, CommitmentType, PaymentMethod, Rhythm
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
        # the starting year for anything but a monthly rhythm.
        CheckConstraint(
            "rhythm = 'monthly' OR first_due_date IS NOT NULL",
            name="ck_commitment_first_due_date_required",
        ),
    )

    owner_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"))

    type: Mapped[CommitmentType] = mapped_column(enum_column(CommitmentType))
    name: Mapped[str] = mapped_column(String(200))
    amount: Mapped[Decimal] = mapped_column(Numeric(12, 2))

    #: The user's choice. BLOCK_SUGGESTION preselects it in the frontend; for
    #: `debt` and `savings_goal`, resolve_block() overrides it.
    category: Mapped[Category] = mapped_column(enum_column(Category))
    block: Mapped[Block] = mapped_column(enum_column(Block))

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

    rhythm: Mapped[Rhythm] = mapped_column(enum_column(Rhythm))

    #: When it falls due for the first time — day, month **and year**.
    #:
    #: Only for a non-monthly rhythm, and mandatory there (see the CHECK above).
    #: The month defines the cadence, the year defines the start:
    #: 2026-02-15 plus quarterly means Feb, May, Aug, Nov, starting in 2026.
    first_due_date: Mapped[date | None] = mapped_column(Date, nullable=True)

    #: Day of the month, 1–31. For a non-monthly rhythm the same day as in
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
        2. **On the cadence.** The rhythm continues across the turn of the year:
           quarterly from July means Jan, Apr, Jul, Oct — not only Jul and Oct.
        """
        if not self.active:
            return False

        if self.first_due_date is not None:
            started = (year, month) >= (
                self.first_due_date.year,
                self.first_due_date.month,
            )
            if not started:
                return False

        if self.rhythm is Rhythm.MONTHLY:
            return True

        start = self.first_month or 1
        return (month - start) % self.rhythm.interval == 0

    def effective_due_day(self, year: int, month: int) -> int:
        """The day it actually falls due in the given month.

        A `due_day` of 31 exists in seven months only. Rather than dropping the
        position or sliding it into the next month, it moves to the last day of
        this one — the 28th or 29th in February.
        """
        return min(self.due_day, monthrange(year, month)[1])
