import uuid
from datetime import datetime
from decimal import Decimal

from sqlalchemy import CheckConstraint, DateTime, ForeignKey, Numeric, String, UniqueConstraint
from sqlalchemy.orm import Mapped, mapped_column, relationship

from app.db.base import Base
from app.db.types import enum_column
from app.models.enums import Block, Category, PaymentMethod, PlanStatus
from app.models.mixins import TimestampMixin, UUIDMixin


class Plan(UUIDMixin, TimestampMixin, Base):
    """One person's plan for one month.

    Always belongs to a person, never to a household. The household plan is not a
    table of its own — it is the composition of every member's positions that have
    `household_id` set.

    The quotas are **guidelines**, not rules: there is a target, the actual figure
    stands next to it, and the household decides whether that is acceptable.
    """

    __tablename__ = "plans"
    __table_args__ = (
        UniqueConstraint("user_id", "year", "month", name="uq_plan_user_month"),
        CheckConstraint("month BETWEEN 1 AND 12", name="ck_plan_month"),
    )

    user_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"))

    year: Mapped[int]
    month: Mapped[int]
    status: Mapped[PlanStatus] = mapped_column(enum_column(PlanStatus), default=PlanStatus.DRAFT)

    target_needs: Mapped[Decimal] = mapped_column(Numeric(5, 2), default=Decimal("50.00"))
    target_wants: Mapped[Decimal] = mapped_column(Numeric(5, 2), default=Decimal("30.00"))
    target_savings: Mapped[Decimal] = mapped_column(Numeric(5, 2), default=Decimal("20.00"))

    #: How many percent of the income is deliberately left unplanned.
    buffer_percent: Mapped[Decimal] = mapped_column(Numeric(5, 2), default=Decimal("0.00"))

    confirmed_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)

    positions: Mapped[list["PlanPosition"]] = relationship(
        back_populates="plan", cascade="all, delete-orphan"
    )


class PlanPosition(UUIDMixin, TimestampMixin, Base):
    """A single item in exactly one monthly plan.

    Created either from a commitment (`commitment_id` set) or by hand as a one-off.

    A position is a **copy, not a pointer**: it takes amount, category and day from
    the commitment and is independent afterwards. Changing the contract does not
    rewrite months that already exist.

    `household_id` decides whether it stays private or appears in a household plan
    — in **exactly one**.
    """

    __tablename__ = "plan_positions"
    __table_args__ = (CheckConstraint("due_day BETWEEN 1 AND 31", name="ck_position_due_day"),)

    plan_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("plans.id", ondelete="CASCADE"))

    #: Empty for one-off positions that do not come from a commitment.
    commitment_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("commitments.id", ondelete="SET NULL"), nullable=True
    )

    #: NULL means private. Set means it appears in that household plan.
    household_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("households.id", ondelete="SET NULL"), nullable=True
    )

    label: Mapped[str] = mapped_column(String(200))
    amount_planned: Mapped[Decimal] = mapped_column(Numeric(12, 2))
    amount_actual: Mapped[Decimal | None] = mapped_column(Numeric(12, 2), nullable=True)

    #: When the position was ticked off. NULL means it is still open.
    #:
    #: Deliberately separate from the amount: "ticked off" and "amount recorded"
    #: are two different things. A payment can match the planned amount exactly —
    #: without this field it would wrongly count as open.
    paid_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)

    category: Mapped[Category] = mapped_column(enum_column(Category))
    #: Derived on creation and **stored** here — changing the mapping later must
    #: not rewrite plans that already exist.
    block: Mapped[Block] = mapped_column(enum_column(Block))

    #: Day of the month the position falls due.
    #
    # TODO: when generating from a commitment, clamp its `due_day` to the last day
    # of **this** month — a contract with `due_day = 31` falls due on the 28th or
    # 29th in February, not never. This field would then hold the clamped day.
    due_day: Mapped[int]
    #: Copied from the commitment, overridable per month. Empty means the default
    #: account.
    account_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("accounts.id", ondelete="SET NULL"), nullable=True
    )

    payment_method: Mapped[PaymentMethod | None] = mapped_column(
        enum_column(PaymentMethod), nullable=True
    )

    #: A budget rather than a single payment — groceries, fuel, pocket money.
    #:
    #: Such a position is not ticked off: it fills up over the month from
    #: individual bookings. A tick would mean nothing there, a fill level does.
    #: Comes from commitment type `budget`, freely choosable on one-off positions.
    is_budget: Mapped[bool] = mapped_column(default=False)

    #: **Where** the money goes when it moves to another own account.
    #:
    #: Set on savings goals and repayments. Ticking the position off then books a
    #: **transfer** instead of an expense — otherwise the total would drop by an
    #: amount that never left the household.
    #:
    #: Empty for everything that really leaves: rent, electricity, groceries.
    counter_account_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("accounts.id", ondelete="SET NULL"), nullable=True
    )

    #: A pass-through position — money that was never there to be spent.
    #:
    #: It stays visible in the plan and it moves the account balance, but it counts
    #: **neither** towards the budget nor towards any quota. Without that, money
    #: merely passed on would inflate the budget and look like money saved.
    pass_through: Mapped[bool] = mapped_column(default=False)

    #: Protects manual corrections from being overwritten the next time the month
    #: is generated from the commitments.
    manually_changed: Mapped[bool] = mapped_column(default=False)

    plan: Mapped[Plan] = relationship(back_populates="positions")
    changes: Mapped[list["PlanPositionChange"]] = relationship(
        back_populates="position", cascade="all, delete-orphan"
    )


class PlanPositionChange(UUIDMixin, TimestampMixin, Base):
    """Audit trail for a change to a position.

    In a household, members may change each other's positions once access has been
    granted. Recording every change keeps that traceable — the alternative would be
    locking the positions, which would get in the way far more often than it helps.

    Values are stored as text: the log has to be readable, not computable.
    """

    __tablename__ = "plan_position_changes"

    position_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("plan_positions.id", ondelete="CASCADE")
    )
    changed_by_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"))

    field: Mapped[str] = mapped_column(String(50))
    old_value: Mapped[str | None] = mapped_column(String(200), nullable=True)
    new_value: Mapped[str | None] = mapped_column(String(200), nullable=True)

    position: Mapped[PlanPosition] = relationship(back_populates="changes")
