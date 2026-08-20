"""What the parking area looks like over the wire."""

import uuid
from datetime import date, datetime
from decimal import Decimal
from typing import Literal

from pydantic import Field

from app.models.enums import Block, Category
from app.schemas.base import Schema

#: What kind of answer this suggestion is. Three, because they lead to three
#: different buttons — and a screen that offers "Übernehmen" for all of them
#: would book a duplicate on the third.
#:
#: * **category** — what this was for. The everyday case.
#: * **transfer** — money moving to another account of the same owner. Not
#:   spending, so the question is "where to", not "what for".
#: * **already_booked** — the *other side* of a movement the book already holds.
#:   Nothing to book here; the entry is thrown out.
SuggestionKind = Literal["category", "transfer", "already_booked"]


class Suggestion(Schema):
    """What the import thinks this entry is, and why.

    Never stored — worked out while the list is read. A suggestion written into
    the row would go stale the moment somebody assigns something, and nothing
    would ever clean it up.

    The reason is not decoration: a suggestion nobody can check is magic, and
    magic is not what anyone wants near their money.
    """

    kind: SuggestionKind = "category"

    #: Empty on a transfer, and on the entry that is already booked. A movement
    #: between own accounts has no category — see `ImportedEntry`.
    category: Category | None = None
    #: The position this belongs to, where exactly one candidate fits. Looked for
    #: in the month of the booking and in the one after it: a payment made in
    #: late August often belongs to September's plan.
    position_id: uuid.UUID | None = None

    #: On a transfer: the own account the money goes to or comes from.
    counter_account_id: uuid.UUID | None = None
    #: Its name, so the row can say "nach Sparkonto" without a second lookup.
    counter_account_name: str | None = None

    #: Whether the other side was **named** or merely inferred.
    #:
    #: `True` means the bank supplied an IBAN and it belongs to one of the
    #: owner's accounts — there is nothing to doubt. `False` means the pairing
    #: comes from amount, direction and date alone, which is what is left when
    #: the bank names nobody. The screen says so: a guess is phrased as a
    #: question, a fact as a statement. Presenting the two the same way would
    #: make the reliable case look as shaky as the other one.
    certain: bool = True

    reason: str


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

    #: Set means this entry is a transfer to another own account, not spending.
    counter_account_id: uuid.UUID | None

    #: Set means thrown out without booking. Such rows stay so that a second
    #: import of the same file does not bring them back.
    discarded_at: datetime | None

    #: Filled only on rows nobody has assigned yet. See `Suggestion`.
    suggestion: Suggestion | None = None


class ImportedEntryUpdate(Schema):
    """Assigning a parked entry.

    A position carries its own category, so sending both is redundant — the
    endpoint takes the position's and ignores whatever category came with it.

    `counter_account_id` is the other kind of answer: it says the movement went
    to another own account. Setting it clears category and position, because a
    transfer is not spending and has nothing to fill.
    """

    position_id: uuid.UUID | None = None
    category: Category | None = None
    counter_account_id: uuid.UUID | None = None


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
