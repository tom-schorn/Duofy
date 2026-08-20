import uuid
from datetime import date
from decimal import Decimal

from pydantic import Field, field_validator

from app.models.enums import AccountType
from app.schemas.base import Schema
from app.services.statements import normalise_iban


class _IbanField:
    """Shared by create and update: the IBAN arrives however the user typed it.

    Grouped in fours is how a bank prints it, so that is how it gets typed. It
    is compared against what a statement reports, and there it is unspaced —
    normalising on the way in means the comparison is a plain `==` everywhere
    else. An empty field means "no IBAN", not an empty string.
    """

    @field_validator("external_ref")
    @classmethod
    def _tidy_iban(cls, value: str | None) -> str | None:
        return normalise_iban(value) or None


class AccountBase(_IbanField, Schema):
    name: str = Field(min_length=1, max_length=100)
    type: AccountType
    opening_balance: Decimal = Field(default=Decimal("0.00"), max_digits=12, decimal_places=2)
    opening_date: date
    #: Preselected in the book quick entry. At most one per person — setting a
    #: second one clears the flag on the first.
    is_default: bool = False
    active: bool = True
    #: The account's IBAN. Optional, and the one thing that lets a transfer be
    #: recognised as one: a parked entry whose counterparty IBAN is this is not
    #: an expense, it is money moving between the user's own accounts.
    #:
    #: An import writes it by itself. Typing it by hand is for the account that
    #: never delivers a file — the savings account is the usual one.
    external_ref: str | None = Field(default=None, max_length=200)
    #: Does money here still count as spendable? True for a current account,
    #: false for savings, where the money is already earmarked.
    counts_as_available: bool = True


class AccountCreate(AccountBase):
    pass


class AccountUpdate(_IbanField, Schema):
    """Everything optional. The opening balance stays editable — it is easy to
    mistype when creating the account, and as long as there are no bookings
    nothing depends on it."""

    name: str | None = Field(default=None, min_length=1, max_length=100)
    type: AccountType | None = None
    opening_balance: Decimal | None = Field(default=None, max_digits=12, decimal_places=2)
    opening_date: date | None = None
    is_default: bool | None = None
    active: bool | None = None
    external_ref: str | None = Field(default=None, max_length=200)
    counts_as_available: bool | None = None


class AccountRead(AccountBase):
    id: uuid.UUID
    owner_id: uuid.UUID

    #: Only set in the household view: who owns the account. In your own plan the
    #: information would be redundant.
    owner_name: str | None = None
    #: Opening balance plus everything booked since. Computed, not stored — a
    #: stored figure would drift apart eventually, and the bookings are the truth
    #: anyway.
    balance: Decimal = Decimal("0.00")


class BalanceMoves(Schema):
    """One day of movement, broken down — every figure a positive amount.

    `change` is `income - needs - wants - savings`. Pure transfers leaving the
    spendable pot count under `savings`: they carry no block, but the money has
    been put aside.
    """

    income: Decimal
    needs: Decimal
    wants: Decimal
    savings: Decimal


class BalancePoint(Schema):
    """A day with movement, together with the balance at its end."""

    day: date
    balance: Decimal
    #: The net movement of that day — the height of the step.
    change: Decimal
    moves: BalanceMoves


class BalanceHistory(Schema):
    """The overall balance across one calendar month.

    `opening_balance` is the balance **before** the first day. Without it the curve
    would start at zero and every month would look like a fresh start.
    """

    opening_balance: Decimal
    closing_balance: Decimal
    points: list[BalancePoint]
