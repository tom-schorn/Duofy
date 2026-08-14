import uuid
from datetime import date
from decimal import Decimal

from sqlalchemy import CheckConstraint, Date, ForeignKey, Numeric, String
from sqlalchemy.orm import Mapped, mapped_column

from app.db.base import Base
from app.db.types import enum_column
from app.models.enums import CATEGORY_LENGTH, Block, Category
from app.models.mixins import TimestampMixin, UUIDMixin


class Transaction(Base, UUIDMixin, TimestampMixin):
    """A booking in the household book — what actually happened.

    ## Effect on balances and effect on the budget are independent

    Two fields, two questions that do not imply each other:

    * **`counter_account_id`** decides the **balances**. If it is set, the money
      moves from `account_id` to there — a transfer.
    * **`position_id`** decides the **budget**. If it is set, the booking fills
      that position.

    All four cases follow from this without any special rules:

    | Transfer | Position | Meaning |
    |---|---|---|
    | no  | yes | a purchase, counts against a budget |
    | no  | no  | an unplanned expense |
    | yes | no  | topping up a wallet, pure movement |
    | yes | yes | moving money to savings — **fulfils the savings quota** |

    The last case is the reason for the split. Excluding transfers from the budget
    across the board would mean saving never meets its target.

    ## Direction

    No sign on the amount. On a normal booking `block` says where it goes: `income`
    is inbound, everything else outbound. On a transfer the direction is already
    fixed by the two accounts.
    """

    __tablename__ = "transactions"

    __table_args__ = (
        # A normal booking always has a purpose. Only a transfer may go without —
        # there the answer is "where to", not "what for".
        CheckConstraint(
            "counter_account_id IS NOT NULL OR (category IS NOT NULL AND block IS NOT NULL)",
            name="ck_transaction_purpose_unless_transfer",
        ),
        # Booking from an account to itself makes no sense and would touch the
        # balance twice.
        CheckConstraint(
            "counter_account_id IS NULL OR counter_account_id <> account_id",
            name="ck_transaction_transfer_needs_two_accounts",
        ),
    )

    owner_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"))

    #: RESTRICT rather than CASCADE: an account with bookings is not deleted, it is
    #: set to `active = false`. Otherwise history would disappear with it.
    account_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("accounts.id", ondelete="RESTRICT")
    )
    #: Set means transfer. The account the money moves to.
    counter_account_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("accounts.id", ondelete="RESTRICT"), nullable=True
    )

    occurred_on: Mapped[date] = mapped_column(Date, index=True)
    amount: Mapped[Decimal] = mapped_column(Numeric(12, 2))
    note: Mapped[str | None] = mapped_column(String(200), nullable=True)

    #: Nullable because a pure transfer has no purpose. See the constraint above.
    category: Mapped[Category | None] = mapped_column(
        enum_column(Category, length=CATEGORY_LENGTH), nullable=True
    )
    block: Mapped[Block | None] = mapped_column(enum_column(Block), nullable=True)

    #: SET NULL: deleting a position keeps the booking. The money moved either
    #: way, only the link to the plan is gone.
    position_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("plan_positions.id", ondelete="SET NULL"), nullable=True
    )

    #: Created by ticking a position off, not entered by hand.
    #:
    #: A position can have several bookings — for a budget that is the normal case.
    #: Without this flag, un-ticking would not know which of them to remove again.
    auto_booked: Mapped[bool] = mapped_column(default=False)

    #: Identifier at the provider — prevents duplicates on a later CSV or bank
    #: import. Free to add now, a migration on real data later.
    external_ref: Mapped[str | None] = mapped_column(String(200), nullable=True)
