import uuid
from datetime import date
from decimal import Decimal

from sqlalchemy import Date, ForeignKey, Index, Numeric, String, text
from sqlalchemy.orm import Mapped, mapped_column

from app.db.base import Base
from app.db.types import enum_column
from app.models.enums import AccountType
from app.models.mixins import TimestampMixin, UUIDMixin


class Account(UUIDMixin, TimestampMixin, Base):
    """A payment account — current, savings, credit card, wallet, cash.

    Belongs to **exactly one person**, like everything else in Duofy. There is no
    joint account on purpose: a household is a planning layer, not an owner. If
    joint accounts are ever added, it would be a nullable `household_id` the way
    positions have one.

    **The balance is not stored.** It follows from `opening_balance` plus every
    transaction after `opening_date`. A stored balance would have to be updated on
    every change and drifts apart the first time one update is missed.

    Securities accounts are out of scope: their value comes from market prices,
    not from transactions. See `AccountType`.
    """

    __tablename__ = "accounts"

    __table_args__ = (
        # At most one default account per owner, enforced as a partial unique
        # index so the database guarantees it instead of the application having
        # to remember.
        Index(
            "uq_account_one_default_per_owner",
            "owner_id",
            unique=True,
            postgresql_where=text("is_default"),
        ),
    )

    owner_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"))

    name: Mapped[str] = mapped_column(String(100))
    type: Mapped[AccountType] = mapped_column(enum_column(AccountType))

    #: The balance at `opening_date`. Starting Duofy halfway through means
    #: entering today's balance here and booking from there on.
    opening_balance: Mapped[Decimal] = mapped_column(
        Numeric(12, 2), default=Decimal("0.00")
    )
    #: **When** the opening balance applied. Without this date the balance at any
    #: given point is not computable — it would be unknown which transactions are
    #: already contained in it.
    opening_date: Mapped[date] = mapped_column(Date)

    #: Preselected in the book's quick entry. An account is mandatory there, and
    #: without a default it would have to be picked for every single purchase.
    is_default: Mapped[bool] = mapped_column(default=False)

    #: Closed accounts stay in the table so old transactions keep their reference.
    #: They only disappear from the pickers.
    active: Mapped[bool] = mapped_column(default=True)

    #: Does money on this account still count as spendable?
    #:
    #: True for a current account, false for a savings account: money set aside
    #: for a purpose cannot be spent a second time. Without this flag "available"
    #: would be the sum of all accounts, which answers no question anyone has.
    #:
    #: A transfer to an account with `False` therefore counts as an **expense** in
    #: the book, even though the money never left the household. Topping up a
    #: wallet is the opposite case: the money stays reachable, so the transfer
    #: changes nothing.
    counts_as_available: Mapped[bool] = mapped_column(default=True)

    #: Identifier at the provider, for fetching transactions from the bank later.
    #: Free to add now, a migration on a populated table later.
    external_ref: Mapped[str | None] = mapped_column(String(200), nullable=True)
