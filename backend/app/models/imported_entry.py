import uuid
from datetime import date, datetime
from decimal import Decimal

from sqlalchemy import Date, DateTime, ForeignKey, Numeric, String, UniqueConstraint
from sqlalchemy.orm import Mapped, mapped_column

from app.db.base import Base
from app.db.types import enum_column
from app.models.enums import CATEGORY_LENGTH, Block, Category
from app.models.mixins import TimestampMixin, UUIDMixin


class ImportedEntry(Base, UUIDMixin, TimestampMixin):
    """A booking read out of a bank file, waiting to be understood.

    ## Why this is not a `Transaction` with a status

    The balance of an account is not stored — it follows from `opening_balance`
    plus every transaction. A parked row is not part of that balance yet, so as a
    transaction it would have to be excluded from every single query that touches
    money: the balance, the book, the 50/30/20 evaluation. Forgetting that filter
    once makes an account silently wrong.

    A separate table cannot be forgotten. When the user assigns the row, a real
    `Transaction` is written and this row is done.

    ## The row belongs to the account's owner

    Not to whoever uploaded the file. Somebody with `Area.ACCOUNTS` at level
    `edit` may import for a household member, and the pile has to appear in that
    member's parking area — otherwise the owner would never see it.

    ## What the file says and what the user says

    The upper half of this table is what the bank reported: dates, amount,
    direction, counterparty, purpose. None of it is editable. What the bank
    reports is fact, and a row whose amount somebody corrected could no longer be
    checked against the account balance.

    The lower half is the interpretation — position, category, block. It may be
    prefilled from what an earlier booking of the same counterparty was assigned
    to, and the user overrides it.
    """

    __tablename__ = "imported_entries"

    __table_args__ = (
        # The same entry may not sit in the parking area twice. `external_ref` is
        # the bank's own identifier, so importing a file a second time simply
        # finds every row already there and adds nothing.
        #
        # Per account, not globally: two accounts of different banks can hand out
        # the same reference, and neither knows about the other.
        UniqueConstraint("account_id", "external_ref", name="uq_imported_entry_ref_per_account"),
    )

    #: Whose data this becomes. See the class docstring.
    owner_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"))

    #: Who uploaded the file. Kept because an import can be done on someone
    #: else's behalf, and a row nobody remembers creating is unsettling.
    imported_by_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"))

    #: RESTRICT like on `Transaction`: an account with a pending pile is not
    #: deleted, it is deactivated.
    account_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("accounts.id", ondelete="RESTRICT"))

    # ---- What the file says -------------------------------------------------

    #: `AcctSvcrRef` from CAMT. Goes to `Transaction.external_ref` on booking, so
    #: the entry stays recognisable after the parked row is gone.
    external_ref: Mapped[str] = mapped_column(String(200))

    occurred_on: Mapped[date] = mapped_column(Date, index=True)
    #: When the money counted for interest. Differs from the booking date often
    #: enough to be worth keeping, and it is what a bank statement shows.
    value_on: Mapped[date] = mapped_column(Date)

    #: No sign, like `Transaction.amount`. `incoming` carries the direction.
    amount: Mapped[Decimal] = mapped_column(Numeric(12, 2))
    incoming: Mapped[bool]

    #: The other side of the payment. Both nullable, and often absent: one real
    #: account had no counterparty at all on its direct debits.
    counterparty_name: Mapped[str | None] = mapped_column(String(200), nullable=True)
    #: 34 characters is the longest an IBAN gets.
    counterparty_iban: Mapped[str | None] = mapped_column(String(34), nullable=True)

    #: Remittance information, several lines joined. Generous length because a
    #: batch booking carries the purposes of all its parts.
    purpose: Mapped[str | None] = mapped_column(String(1000), nullable=True)

    # ---- What the user says -------------------------------------------------

    #: SET NULL: deleting a position must not delete the parked row with it.
    position_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("plan_positions.id", ondelete="SET NULL"), nullable=True
    )

    #: Both nullable while the row is still unassigned. A position brings its own
    #: category, so these stay empty whenever `position_id` is set.
    category: Mapped[Category | None] = mapped_column(
        enum_column(Category, length=CATEGORY_LENGTH), nullable=True
    )
    block: Mapped[Block | None] = mapped_column(enum_column(Block), nullable=True)

    #: Thrown out without booking, and **kept** as a marker.
    #:
    #: Deleting the row instead would mean the next import of the same file
    #: brings the entry straight back — the discard would have to be repeated
    #: every month. The row stays, it only leaves the list.
    discarded_at: Mapped[datetime | None] = mapped_column(
        DateTime(timezone=True), nullable=True
    )
