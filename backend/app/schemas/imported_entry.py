"""What the parking area looks like over the wire."""

import uuid
from datetime import date, datetime
from decimal import Decimal

from pydantic import Field

from app.models.enums import Block, Category
from app.schemas.base import Schema


class ImportedEntryRead(Schema):
    """One parked entry.

    The upper half is what the bank reported and is not editable — a row whose
    amount somebody corrected could no longer be checked against the account.
    The lower half is the interpretation.
    """

    id: uuid.UUID
    account_id: uuid.UUID
    owner_id: uuid.UUID

    occurred_on: date
    value_on: date
    amount: Decimal
    incoming: bool
    counterparty_name: str | None
    counterparty_iban: str | None
    purpose: str | None

    position_id: uuid.UUID | None
    category: Category | None
    block: Block | None

    #: Set means thrown out without booking. Such rows stay so that a second
    #: import of the same file does not bring them back.
    discarded_at: datetime | None


class ImportedEntryUpdate(Schema):
    """Assigning a parked entry.

    A position carries its own category, so sending both is redundant — the
    endpoint takes the position's and ignores whatever category came with it.
    """

    position_id: uuid.UUID | None = None
    category: Category | None = None


class ImportSummary(Schema):
    """What one upload did, in the words of the screen that shows it."""

    account_id: uuid.UUID
    iban: str
    #: Entries the file contained, pending ones already excluded.
    read: int
    #: Newly parked.
    parked: int
    #: Skipped because they are already in the book or already parked.
    known: int

    #: Opening plus every booked entry has to equal closing. `False` means the
    #: file is incomplete or something was misread — worth showing, because the
    #: import cannot tell which of the two it is.
    balances_match: bool

    #: Set when the file names an IBAN that belongs to no account yet. The client
    #: then asks once which account is meant and repeats the upload with it.
    unknown_iban: str | None = Field(default=None)
