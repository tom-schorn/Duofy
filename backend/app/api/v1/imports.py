"""Uploading a bank file, parking what it contains, turning that into bookings.

Three steps, deliberately separate:

1. **Upload** reads the file and parks every entry it does not already know.
   Nothing is booked, nothing is decided.
2. **Assign** puts a position or a category on a parked entry. Reversible, and
   it can sit half-done for a week.
3. **Book** turns one parked entry into a `Transaction` and is the only step
   that touches the account balance.

The file itself is never stored. It is read and dropped — the account holder's
name, the bank identifiers and the balances in it are not needed once the
entries are out.
"""

import uuid
from datetime import UTC, datetime, timedelta

from fastapi import APIRouter, Depends, HTTPException, UploadFile, status
from sqlalchemy import or_, select, tuple_
from sqlalchemy.ext.asyncio import AsyncSession

from app.api.v1.transactions import _recalc_position
from app.core.auth import current_active_user
from app.core.permissions import Area, granted_level, require
from app.db.session import get_session
from app.models.account import Account
from app.models.enums import AccessLevel, Category
from app.models.imported_entry import ImportedEntry
from app.models.plan import Plan, PlanPosition
from app.models.transaction import Transaction
from app.models.user import User
from app.schemas.imported_entry import (
    ImportedEntryRead,
    ImportedEntryUpdate,
    ImportSummary,
    Suggestion,
)
from app.services.statements import StatementError, read_upload

router = APIRouter()

#: Upper bound on an upload. A quarter of turnover on two accounts came to about
#: 200 KB of XML, so this is generous by a wide margin. It exists because the
#: whole file is held in memory while it is parsed.
MAX_UPLOAD_BYTES = 10 * 1024 * 1024


async def _may_act_for(session: AsyncSession, owner_id: uuid.UUID, user: User) -> None:
    """Importing and booking both hang off `Area.ACCOUNTS` at level `edit`.

    The same rule `transactions.py` uses — an import is a way of writing
    bookings, so it cannot be an easier one.
    """
    if owner_id == user.id:
        return
    level = await granted_level(session, owner_id, user.id, Area.ACCOUNTS)
    require(level.rank >= AccessLevel.EDIT.rank, "no_edit_granted")


async def _load(
    session: AsyncSession, entry_id: uuid.UUID, user: User
) -> ImportedEntry:
    entry = await session.get(ImportedEntry, entry_id)
    if entry is None:
        raise HTTPException(
            status.HTTP_404_NOT_FOUND, detail={"code": "imported_entry_not_found"}
        )
    await _may_act_for(session, entry.owner_id, user)
    return entry


async def _read_body(file: UploadFile) -> bytes:
    """The upload, refused if it is too large.

    Read in chunks rather than in one go: `await file.read()` on a huge upload
    allocates it all before anything can object.
    """
    chunks: list[bytes] = []
    size = 0
    while chunk := await file.read(64 * 1024):
        size += len(chunk)
        if size > MAX_UPLOAD_BYTES:
            raise HTTPException(
                status.HTTP_413_REQUEST_ENTITY_TOO_LARGE,
                detail={"code": "upload_too_large"},
            )
        chunks.append(chunk)
    return b"".join(chunks)


@router.post("", response_model=ImportSummary, status_code=status.HTTP_201_CREATED)
async def upload(
    file: UploadFile,
    owner: uuid.UUID | None = None,
    account: uuid.UUID | None = None,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> ImportSummary:
    """Read a statement and park what it holds.

    CAMT or CSV — the format is recognised from the content, and everything
    after this point works the same either way.

    The account comes **from the file** where it says so: CAMT names the IBAN of
    the report, and a CSV export usually carries it in the block above the table.
    On the first upload of an unknown IBAN the client is asked once which account
    is meant and repeats the call with `account=`; the IBAN is then remembered on
    that account and never asked again.

    Where a CSV names no IBAN at all, the chosen account supplies it — which is
    why `account` is passed down into the reader.
    """
    owner_id = owner or user.id
    await _may_act_for(session, owner_id, user)

    chosen = await session.get(Account, account) if account is not None else None
    try:
        reports = read_upload(
            await _read_body(file),
            iban=(chosen.external_ref or "") if chosen else "",
        )
    except StatementError as error:
        raise HTTPException(
            status.HTTP_422_UNPROCESSABLE_CONTENT,
            detail={"code": "not_a_bank_file", "message": str(error)},
        ) from error

    ibans = {report.iban for report in reports}
    if len(ibans) != 1:
        raise HTTPException(
            status.HTTP_422_UNPROCESSABLE_CONTENT,
            detail={"code": "file_covers_several_accounts"},
        )
    iban = ibans.pop()

    target = await _target_account(session, iban, account, owner_id)
    if target is None:
        # Not an error: the client asks which account is meant and tries again.
        return ImportSummary(
            account_id=uuid.UUID(int=0),
            iban=iban,
            read=sum(len(report.entries) for report in reports),
            parked=0,
            known=0,
            balances_match=True,
            unknown_iban=iban,
        )

    entries = [entry for report in reports for entry in report.entries]
    known = await _already_known(session, target.id, [e.external_ref for e in entries])

    for entry in entries:
        if entry.external_ref in known:
            continue
        session.add(
            ImportedEntry(
                owner_id=owner_id,
                imported_by_id=user.id,
                account_id=target.id,
                external_ref=entry.external_ref,
                occurred_on=entry.booked_on,
                value_on=entry.value_on,
                amount=entry.amount,
                incoming=entry.incoming,
                counterparty_name=entry.counterparty_name,
                counterparty_iban=entry.counterparty_iban,
                purpose=entry.purpose,
            )
        )

    await session.commit()

    moved = sum(
        (entry.amount if entry.incoming else -entry.amount) for entry in entries
    )
    first, last = reports[0], reports[-1]

    return ImportSummary(
        account_id=target.id,
        iban=iban,
        read=len(entries),
        parked=len(entries) - len(known),
        known=len(known),
        balances_match=first.opening_balance + moved == last.closing_balance,
    )


async def _target_account(
    session: AsyncSession,
    iban: str,
    chosen: uuid.UUID | None,
    owner_id: uuid.UUID,
) -> Account | None:
    """Which account this file belongs to.

    Found by the IBAN the file names. When the client supplies one instead, the
    IBAN is written onto it — so the question is asked once per account, not once
    per upload.
    """
    if chosen is not None:
        account = await session.get(Account, chosen)
        if account is None or account.owner_id != owner_id:
            raise HTTPException(
                status.HTTP_404_NOT_FOUND, detail={"code": "account_not_found"}
            )
        account.external_ref = iban
        return account

    rows = await session.execute(
        select(Account).where(Account.owner_id == owner_id, Account.external_ref == iban)
    )
    return rows.scalars().first()


async def _already_known(
    session: AsyncSession, account_id: uuid.UUID, refs: list[str]
) -> set[str]:
    """Which of these entries the account has seen before.

    Both tables are asked. A booked entry lives in `transactions`, a parked or
    discarded one in `imported_entries` — checking only the first would bring
    back everything the user threw out last month.
    """
    if not refs:
        return set()

    booked = await session.execute(
        select(Transaction.external_ref).where(
            # Either column, because a transfer is booked as leaving one account
            # and arriving at another: an entry read on the receiving side ends
            # up as a booking whose `account_id` is the *other* account. Asking
            # only for `account_id` would let the next import of that file park
            # the same entry a second time.
            or_(
                Transaction.account_id == account_id,
                Transaction.counter_account_id == account_id,
            ),
            Transaction.external_ref.in_(refs),
        )
    )
    parked = await session.execute(
        select(ImportedEntry.external_ref).where(
            ImportedEntry.account_id == account_id,
            ImportedEntry.external_ref.in_(refs),
        )
    )
    return {ref for ref in booked.scalars() if ref} | set(parked.scalars())


# ---------------------------------------------------------------------------
# Suggestions
#
# Worked out while the list is read, never stored. A suggestion is derived, and
# derived values that get written down go stale: somebody assigns a category and
# every row suggested from the old state keeps pointing at it.
#
# It also means the answer is always current without a job or a queue. The
# frontend reloads after every booking anyway — which is what lets the
# recognition learn **inside** the pile: book the first of eight supermarket
# rows and the other seven have a suggestion in the same breath.
# ---------------------------------------------------------------------------


#: How far apart the two sides of one transfer may be booked.
#:
#: Money leaves on Friday and arrives on Monday; between two banks three working
#: days plus a weekend is ordinary. Five covers that. Wider would start pairing
#: up unrelated movements of the same amount — a standing order to the savings
#: account is exactly the same amount every month, and only the date tells two
#: of them apart.
_TRANSFER_DAYS = 5


async def _accounts_of(
    session: AsyncSession, owner_id: uuid.UUID
) -> dict[uuid.UUID, Account]:
    """Every account this person has, by id. One query, three uses below."""
    rows = await session.execute(
        select(Account).where(Account.owner_id == owner_id)
    )
    return {account.id: account for account in rows.scalars()}


def _by_iban(accounts: dict[uuid.UUID, Account]) -> dict[str, Account]:
    """The same accounts by IBAN — the surest basis for recognising a transfer.

    An account without an IBAN cannot appear here, and the IBAN rule is blind to
    it. That is why the IBAN is editable by hand: the savings account, the one
    people move money to most, is usually the one that never delivers a
    statement to import.
    """
    return {
        account.external_ref: account
        for account in accounts.values()
        if account.external_ref
    }


def _transfer_target(entry: ImportedEntry, own: dict[str, Account]) -> Account | None:
    """The own account on the other side of this entry, if there is one.

    Only the IBAN counts. A name would be tempting — "Tom Schorn" on both sides
    of a transfer to oneself — but the counterparty name on a statement is the
    account *holder's*, and everything a person pays to themselves at a shop
    till would look the same.
    """
    if not entry.counterparty_iban:
        return None
    other = own.get(entry.counterparty_iban)
    if other is None or other.id == entry.account_id:
        return None
    return other


def _guessed_transfers(
    entries: list[ImportedEntry], by_id: dict[uuid.UUID, Account]
) -> dict[uuid.UUID, tuple[ImportedEntry, Account]]:
    """Transfers recognised without an IBAN — the two halves finding each other.

    Not every bank names the other side. A credit card top-up in particular
    tends to arrive as an amount and a date and nothing else, and then the IBAN
    rule above sees nothing at all.

    What is left is the shape of the movement: the same amount, opposite
    directions, on two accounts of the same person, a few days apart. That is
    weak evidence and it is offered as a question, never applied — hence
    `certain=False` on the suggestion it produces.

    Two rules keep it from guessing wildly:

    * **Both halves must be nameless.** An entry whose counterparty IBAN belongs
      to somebody else is not a transfer, whatever its amount says, and pairing
      it with a nameless one would invent a movement out of two real payments
    * **Exactly one candidate.** Two possible mates mean the amount does not
      identify anything, and the same silence applies as everywhere else in this
      file: no suggestion beats a plausible wrong one
    """
    nameless = [entry for entry in entries if not entry.counterparty_iban]

    found: dict[uuid.UUID, tuple[ImportedEntry, Account]] = {}
    for entry in nameless:
        mates = [
            other
            for other in nameless
            if other.id != entry.id
            and other.account_id != entry.account_id
            and other.incoming is not entry.incoming
            and other.amount == entry.amount
            and abs((other.occurred_on - entry.occurred_on).days) <= _TRANSFER_DAYS
        ]
        if len(mates) != 1:
            continue
        other_account = by_id.get(mates[0].account_id)
        if other_account is not None:
            found[entry.id] = (entry, other_account)
    return found


async def _already_moved(
    session: AsyncSession,
    owner_id: uuid.UUID,
    entries: list[ImportedEntry],
    own: dict[str, Account],
    by_id: dict[uuid.UUID, Account],
) -> dict[uuid.UUID, Account]:
    """Which entries the book already holds, seen from the other side.

    A movement between two own accounts turns up in **both** statements — once
    outgoing, once incoming. Booked twice it becomes an expense that was not one
    and an income that was not one; the balances still come out right, which is
    what makes it so easy to miss.

    `external_ref` cannot answer this: the two sides carry different references,
    each bank numbering its own statement. So the movement itself is matched —
    this account on the matching side of a booked transfer, the same amount, a
    few days apart.

    ## What is deliberately not asked

    Whether the pair was ever recognised as a transfer beforehand. The entry
    that produced the booking is gone; only its other half is still parked, and
    it may have arrived from a bank that names nobody. Requiring the IBAN here
    would mean the recognition works right up until the moment it matters.

    An entry whose counterparty IBAN belongs to **somebody else** is skipped —
    that is a real payment to a real third party, and no coincidence of amount
    and date makes it anything else.

    Each booking may only answer for **one** entry. Two transfers of 200 € in a
    week are two movements, and letting one booking cover both would hide the
    second one for good.
    """
    if not entries:
        return {}

    days = [entry.occurred_on for entry in entries]
    rows = await session.execute(
        select(Transaction).where(
            Transaction.owner_id == owner_id,
            Transaction.counter_account_id.is_not(None),
            Transaction.occurred_on >= min(days) - timedelta(days=_TRANSFER_DAYS),
            Transaction.occurred_on <= max(days) + timedelta(days=_TRANSFER_DAYS),
        )
    )
    booked = list(rows.scalars())
    if not booked:
        return {}

    found: dict[uuid.UUID, Account] = {}
    spoken_for: set[uuid.UUID] = set()
    for entry in entries:
        # A named third party rules the whole question out.
        if entry.counterparty_iban and entry.counterparty_iban not in own:
            continue

        fits = [
            transaction
            for transaction in booked
            if transaction.id not in spoken_for
            and transaction.amount == entry.amount
            and abs((transaction.occurred_on - entry.occurred_on).days) <= _TRANSFER_DAYS
            # Money arriving here means this account is the transfer's target;
            # money leaving means it is the source. Ignoring the direction would
            # pair an entry with a movement that ran the other way.
            and entry.account_id
            == (
                transaction.counter_account_id
                if entry.incoming
                else transaction.account_id
            )
            and _other_side(transaction, entry) is not None
        ]
        if not fits:
            continue

        closest = min(
            fits, key=lambda t: abs((t.occurred_on - entry.occurred_on).days)
        )
        other_id = _other_side(closest, entry)
        other_account = by_id.get(other_id) if other_id else None
        if other_account is None:
            continue
        # Where the bank did name the other side, it has to be the same account.
        # The amount agreeing is not enough to overrule an IBAN.
        if entry.counterparty_iban and own[entry.counterparty_iban].id != other_account.id:
            continue

        found[entry.id] = other_account
        spoken_for.add(closest.id)
    return found


def _other_side(transaction: Transaction, entry: ImportedEntry) -> uuid.UUID | None:
    """The account at the far end of this booking, from the entry's point of view."""
    return transaction.account_id if entry.incoming else transaction.counter_account_id


#: Legal forms, dropped from a merchant name before comparing.
#:
#: The same shop appears as "REWE Markt GmbH" on one statement and as
#: "rewe markt 4471" on the next; without this they are two different
#: counterparties. The list is short on purpose — it covers the German forms
#: that actually turn up, and an unknown one costs a suggestion, not a booking.
_LEGAL_FORMS = frozenset(
    {"gmbh", "mbh", "ag", "kg", "kgaa", "ohg", "ug", "se", "co", "ev", "eg", "gbr"}
)


def _normalise(name: str) -> str:
    """A merchant name reduced to what stays the same between two receipts.

    Case, punctuation, digits and the legal form go — branch numbers and
    "GmbH & Co. KG" are exactly what differs between two visits to one shop.

    What is left is **weak evidence**, and it is only used where the bank gave no
    IBAN. That is most card payments, so it earns its keep; but two unrelated
    shops sharing a first word would be conflated, which is why an IBAN always
    wins where there is one.
    """
    letters = "".join(c if c.isalpha() or c.isspace() else " " for c in name.lower())
    return " ".join(word for word in letters.split() if word not in _LEGAL_FORMS)


#: How many past bookings of one counterparty have to agree.
#:
#: A payment service is one counterparty for many purposes: PayPal, Klarna,
#: Amazon, a card statement. Suggesting the last category there is wrong most of
#: the time, and confidently wrong is worse than silent.
#:
#: So the last few bookings have to agree before anything is offered. Three,
#: because it also lets a single mistyped category fall out of the window
#: instead of poisoning that counterparty for good — and because a counterparty
#: booked only once is still worth following, which is what makes the first pile
#: teach itself.
_AGREEING_BOOKINGS = 3


async def _learned(
    session: AsyncSession, owner_id: uuid.UUID, entries: list[ImportedEntry]
) -> dict[uuid.UUID, tuple[Category, str]]:
    """What each entry's counterparty was booked as, where that is unambiguous.

    One query for the whole batch. The newest bookings per counterparty decide —
    "last one wins" — but only while the last `_AGREEING_BOOKINGS` of them say
    the same thing. Where they disagree the counterparty says nothing about the
    purpose, and no suggestion is better than a plausible wrong one.

    The bookings themselves are the memory. A separate table of rules would be a
    second truth to keep in step with the first.
    """
    ibans = {entry.counterparty_iban for entry in entries if entry.counterparty_iban}
    names = {
        _normalise(entry.counterparty_name)
        for entry in entries
        if entry.counterparty_name and not entry.counterparty_iban
    }
    if not ibans and not names:
        return {}

    rows = await session.execute(
        select(
            Transaction.counterparty_iban,
            Transaction.counterparty_name,
            Transaction.category,
            Transaction.occurred_on,
        )
        .where(
            Transaction.owner_id == owner_id,
            Transaction.category.is_not(None),
            Transaction.counterparty_name.is_not(None),
        )
        .order_by(Transaction.occurred_on.desc(), Transaction.created_at.desc())
    )

    # Collect the newest few per key, then let them vote.
    recent_iban: dict[str, list[Category]] = {}
    recent_name: dict[str, list[Category]] = {}
    for iban, name, category, _ in rows:
        if iban:
            seen = recent_iban.setdefault(iban, [])
            if len(seen) < _AGREEING_BOOKINGS:
                seen.append(category)
        if name:
            seen = recent_name.setdefault(_normalise(name), [])
            if len(seen) < _AGREEING_BOOKINGS:
                seen.append(category)

    def agreed(seen: list[Category]) -> Category | None:
        """The category, if the recent bookings are of one mind about it."""
        return seen[0] if seen and len(set(seen)) == 1 else None

    by_iban = {key: found for key, seen in recent_iban.items() if (found := agreed(seen))}
    by_name = {key: found for key, seen in recent_name.items() if (found := agreed(seen))}

    found: dict[uuid.UUID, tuple[Category, str]] = {}
    for entry in entries:
        if entry.counterparty_iban and entry.counterparty_iban in by_iban:
            found[entry.id] = (by_iban[entry.counterparty_iban], "iban")
        elif entry.counterparty_name:
            key = _normalise(entry.counterparty_name)
            if key in by_name:
                found[entry.id] = (by_name[key], "name")
    return found


async def _positions_by_category(
    session: AsyncSession, owner_id: uuid.UUID, entries: list[ImportedEntry]
) -> dict[tuple[int, int, Category], list[PlanPosition]]:
    """The positions of the months this pile falls into, grouped by category.

    Budget positions and single payments together, because the question is the
    same for both: **is there exactly one candidate?** A month holds one
    groceries budget, one rent, one mobile contract — the category settles it,
    and no amount has to be compared.

    Only where a category has several positions does the amount decide, and only
    then are the tolerances of #61 needed at all. Which is most of the time not.

    ## The month after is loaded as well

    A planning month is not a calendar month. Rent leaves the account on the
    28th for the month that starts on the 1st, and salary arrives before the
    month it pays for. Offering only the calendar month means the entries that
    are easiest to place are the ones with no position to place them on.
    """
    months = {
        month
        for entry in entries
        for month in (
            (entry.occurred_on.year, entry.occurred_on.month),
            _next_month(entry.occurred_on.year, entry.occurred_on.month),
        )
    }
    if not months:
        return {}

    rows = await session.execute(
        select(Plan.year, Plan.month, PlanPosition)
        .join(PlanPosition, PlanPosition.plan_id == Plan.id)
        .where(
            Plan.user_id == owner_id,
            tuple_(Plan.year, Plan.month).in_(months),
        )
    )

    grouped: dict[tuple[int, int, Category], list[PlanPosition]] = {}
    for year, month, position in rows:
        grouped.setdefault((year, month, position.category), []).append(position)
    return grouped


def _next_month(year: int, month: int) -> tuple[int, int]:
    """The month after this one. December rolls over into the next year."""
    return (year + 1, 1) if month == 12 else (year, month + 1)


def _position_for(
    entry: ImportedEntry,
    category: Category,
    positions: dict[tuple[int, int, Category], list[PlanPosition]],
) -> uuid.UUID | None:
    """The position this entry most likely belongs to, or none.

    The month of the booking is asked first and, where it holds positions of
    that category, it **decides alone**. Only a month with nothing at all hands
    the question on to the one after it — the payment made on the 28th for next
    month's rent. Letting the next month answer where this one was merely
    ambiguous would move the choice a month sideways to avoid saying "I don't
    know", which is the worse of the two.
    """
    own = (entry.occurred_on.year, entry.occurred_on.month)
    for year, month in (own, _next_month(*own)):
        candidates = positions.get((year, month, category))
        if candidates:
            return _closest(candidates, entry)
    return None


def _closest(candidates: list[PlanPosition], entry: ImportedEntry) -> uuid.UUID | None:
    """Which of one month's positions of a category this entry fits.

    One candidate means it is that one — a paid position included, since a
    budget takes many bookings and a single payment may arrive in instalments.

    Several candidates are what the amount is for: the closest match wins, and
    only if it is within a euro. Two insurances of 18.40 and 91.00 are told
    apart that way; two of 18.40 and 18.39 are not, and then nothing is offered
    rather than a coin toss.
    """
    if len(candidates) == 1:
        return candidates[0].id

    closest = min(candidates, key=lambda p: abs(p.amount_planned - entry.amount))
    return closest.id if abs(closest.amount_planned - entry.amount) <= 1 else None


def _suggest(
    entry: ImportedEntry,
    learned: dict[uuid.UUID, tuple[Category, str]],
    positions: dict[tuple[int, int, Category], list[PlanPosition]],
    transfers: dict[uuid.UUID, tuple[ImportedEntry, Account]],
    guessed: dict[uuid.UUID, tuple[ImportedEntry, Account]],
    already_moved: dict[uuid.UUID, Account],
) -> Suggestion | None:
    """One suggestion, or none.

    They are asked in the order of how much they change the answer:

    * **The movement is already in the book** — this entry is its other side.
      Booking it would count one movement twice, so this one is a warning, and
      it is raised even on an entry somebody has already given a category. Being
      about to make a duplicate outranks having decided something
    * **The counterparty is an own account** — a transfer. Where the money went,
      not what it was for
    * **Nothing assigned yet** — the counterparty says what this was last time,
      and a matching position comes along with it
    * **A category chosen by hand, no position** — the position follows from
      that category alone. Leaving this out meant the moment somebody decided
      for themselves, the import stopped helping — exactly when the answer was
      most certain

    An entry that already carries a decision of its own is otherwise left alone.
    Second-guessing a choice is not a suggestion.
    """
    if entry.id in already_moved:
        other = already_moved[entry.id]
        named = bool(entry.counterparty_iban)
        return Suggestion(
            kind="already_booked",
            counter_account_id=other.id,
            counter_account_name=other.name,
            certain=named,
            reason=(
                f"dieselbe Bewegung steht schon im Buch — {other.name}"
                if named
                else f"gleicher Betrag, Gegenrichtung, {other.name}"
            ),
        )

    if entry.counter_account_id is not None:
        return None

    hit_transfer = transfers.get(entry.id) or guessed.get(entry.id)
    if hit_transfer is not None:
        _, other = hit_transfer
        named = entry.id in transfers
        return Suggestion(
            kind="transfer",
            counter_account_id=other.id,
            counter_account_name=other.name,
            certain=named,
            reason=(
                f"die IBAN gehört zu {other.name} — keine Ausgabe"
                if named
                else f"gleicher Betrag in Gegenrichtung auf {other.name}"
            ),
        )

    if entry.position_id is not None:
        return None

    if entry.category is not None:
        position_id = _position_for(entry, entry.category, positions)
        if position_id is None:
            return None
        return Suggestion(
            kind="category",
            category=entry.category,
            position_id=position_id,
            reason=_position_reason(entry, position_id, positions),
        )

    hit = learned.get(entry.id)
    if hit is None:
        return None

    category, how = hit
    where = "der IBAN" if how == "iban" else "dem Namen"
    return Suggestion(
        kind="category",
        category=category,
        position_id=_position_for(entry, category, positions),
        reason=f"zuletzt so gebucht, erkannt an {where}",
    )


def _position_reason(
    entry: ImportedEntry,
    position_id: uuid.UUID,
    positions: dict[tuple[int, int, Category], list[PlanPosition]],
) -> str:
    """Why this position, in one line — and **which month** it came from.

    Naming the month matters only when it is not the obvious one. A position
    from the month after looks like a mistake until it says so itself.
    """
    own = (entry.occurred_on.year, entry.occurred_on.month)
    here = positions.get((*own, entry.category)) if entry.category else None
    if here and any(position.id == position_id for position in here):
        return "einziger Posten dieses Monats für deine Kategorie"
    return "einziger Posten im Folgemonat für deine Kategorie"


@router.get("", response_model=list[ImportedEntryRead])
async def list_entries(
    owner: uuid.UUID | None = None,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> list[ImportedEntryRead]:
    """The parking area, newest day first. Discarded rows stay out of the list.

    `external_ref` decides within a day, and it has to: every row of one import
    is written in a single transaction, and `now()` is the **transaction's**
    start time, so `created_at` is identical to the microsecond across the whole
    batch. Without a unique tiebreaker the order is undefined — and an updated
    row typically comes back last, which made entries jump the moment one of
    them was given a category.
    """
    owner_id = owner or user.id
    if owner_id != user.id:
        level = await granted_level(session, owner_id, user.id, Area.ACCOUNTS)
        require(level.rank >= AccessLevel.VIEW.rank, "no_insight_granted")

    rows = await session.execute(
        select(ImportedEntry)
        .where(
            ImportedEntry.owner_id == owner_id,
            ImportedEntry.discarded_at.is_(None),
        )
        .order_by(
            ImportedEntry.occurred_on.desc(),
            ImportedEntry.created_at,
            ImportedEntry.external_ref,
        )
    )
    entries = list(rows.scalars())

    accounts = await _accounts_of(session, owner_id)
    own = _by_iban(accounts)
    transfers = {
        entry.id: (entry, other)
        for entry in entries
        if (other := _transfer_target(entry, own)) is not None
    }
    # The pile itself can hold both halves. Accounts without an IBAN belong here
    # too — this rule never reads one, so it is the only recognition that works
    # on an account Duofy has no IBAN for at all.
    guessed = _guessed_transfers(entries, accounts)

    learned = await _learned(session, owner_id, entries)
    positions = await _positions_by_category(session, owner_id, entries)
    already_moved = await _already_moved(session, owner_id, entries, own, accounts)

    return [
        ImportedEntryRead.model_validate(entry).model_copy(
            update={
                "suggestion": _suggest(
                    entry, learned, positions, transfers, guessed, already_moved
                )
            }
        )
        for entry in entries
    ]


@router.patch("/{entry_id}", response_model=ImportedEntryRead)
async def assign(
    entry_id: uuid.UUID,
    payload: ImportedEntryUpdate,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> ImportedEntry:
    """Put a position, a category or an own account on a parked entry.

    A position wins over a category: `PlanPosition.category` is not nullable, so
    a position always carries one, and having the two disagree would leave the
    booking with a category its position does not share.

    A **counter account** is the other kind of answer altogether. Setting one
    says the money moved between the owner's own accounts, and then there is
    nothing to categorise — so category, block and position are cleared with it.
    Clearing the counter account leaves the row blank rather than restoring what
    was there before: the two readings are mutually exclusive, and guessing back
    into one of them would be a decision nobody made.
    """
    entry = await _load(session, entry_id, user)
    changes = payload.model_dump(exclude_unset=True)

    if "counter_account_id" in changes:
        entry.counter_account_id = await _counter_account(
            session, changes["counter_account_id"], entry
        )
        if entry.counter_account_id is not None:
            entry.position_id = None
            entry.category = None
            entry.block = None

    if "position_id" in changes:
        entry.position_id = changes["position_id"]
        if entry.position_id is not None:
            position = await session.get(PlanPosition, entry.position_id)
            if position is None:
                raise HTTPException(
                    status.HTTP_404_NOT_FOUND, detail={"code": "position_not_found"}
                )
            require(position.plan_id is not None, "position_not_found")
            entry.category = position.category
            entry.block = position.block

    if "category" in changes and entry.position_id is None:
        entry.category = changes["category"]
        entry.block = changes["category"].block if changes["category"] else None

    # A purpose and a transfer cannot both be true. Whichever arrived in this
    # call wins, so choosing a category on a row marked as a transfer takes the
    # mark off rather than being silently ignored.
    if entry.category is not None or entry.position_id is not None:
        entry.counter_account_id = None

    await session.commit()
    await session.refresh(entry)
    return entry


async def _counter_account(
    session: AsyncSession, account_id: uuid.UUID | None, entry: ImportedEntry
) -> uuid.UUID | None:
    """The own account a transfer points at, checked before it is stored.

    Two things have to hold, and the database enforces neither: it must belong to
    the same owner as the entry, and it must not be the entry's own account. A
    booking from an account to itself would move the balance twice — the check
    constraint on `transactions` refuses it, but at that point the parked row is
    already wrong.
    """
    if account_id is None:
        return None

    account = await session.get(Account, account_id)
    if account is None or account.owner_id != entry.owner_id:
        raise HTTPException(
            status.HTTP_404_NOT_FOUND, detail={"code": "account_not_found"}
        )
    require(account.id != entry.account_id, "transfer_needs_two_accounts")
    return account.id


@router.post("/{entry_id}/book", response_model=ImportedEntryRead)
async def book(
    entry_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> ImportedEntry:
    """Turn a parked entry into a booking.

    The parked row goes away afterwards. Its identity survives in
    `Transaction.external_ref`, which is what keeps a second import of the same
    file from bringing it back — and so do the counterparty's name and IBAN,
    which is what lets anything ever learn from this assignment.

    ## Two ways this becomes a transfer

    `PlanPosition.counter_account_id` says where the money goes when it moves to
    another own account, and `mark_paid` has always honoured it. Booking an
    import has to do the same: without it a transfer to the savings account
    becomes an **expense**, the savings account never sees the money, and the
    savings quota stays empty while the money is demonstrably saved.

    `ImportedEntry.counter_account_id` is the same thing without a plan behind
    it — topping up a wallet, moving what is left over to the savings account.
    Either way the booking gets a counter account and needs no category, which
    is what the check constraint on `transactions` allows.
    """
    entry = await _load(session, entry_id, user)

    position = (
        await session.get(PlanPosition, entry.position_id)
        if entry.position_id is not None
        else None
    )
    counter_account_id = entry.counter_account_id or (
        position.counter_account_id if position else None
    )
    require(
        entry.category is not None or counter_account_id is not None,
        "category_missing",
    )

    # A transfer **leaves** `account_id` and arrives at `counter_account_id` —
    # that is the whole of how the balances read it. On an entry the bank
    # reported as incoming, this account is therefore the *arrival*, and the two
    # have to swap. Booking it the other way round would move the same money the
    # wrong way on both accounts at once.
    source, target = entry.account_id, counter_account_id
    if counter_account_id is not None and entry.incoming:
        source, target = counter_account_id, entry.account_id

    session.add(
        Transaction(
            owner_id=entry.owner_id,
            account_id=source,
            counter_account_id=target,
            occurred_on=entry.occurred_on,
            amount=entry.amount,
            note=entry.counterparty_name or entry.purpose,
            category=entry.category,
            block=entry.block,
            position_id=entry.position_id,
            external_ref=entry.external_ref,
            counterparty_name=entry.counterparty_name,
            counterparty_iban=entry.counterparty_iban,
        )
    )
    await session.delete(entry)
    await session.flush()
    await _settle_position(session, position)
    await session.commit()
    return entry


async def _settle_position(session: AsyncSession, position: PlanPosition | None) -> None:
    """Let the plan know that this position was paid.

    Two separate things, and the model keeps them apart on purpose:

    * **`amount_actual`** is the sum of the bookings assigned to the position.
      `_recalc_position` maintains it, and without calling it the month shows
      nothing spent while the money demonstrably left the account.
    * **`paid_at`** is the tick, and a **single payment** only gets one once the
      bookings assigned to it reach the planned amount. Assigning a payment says
      "this is for that position"; assigning a **part** of it does not say the
      position is settled. A 200 € transfer against 890 € of rent leaves it
      open, and the next 690 € close it.

    A budget position is **not** ticked at all. It fills up over the month from
    many bookings, and a tick would claim groceries are finished for August
    because one receipt arrived.
    """
    if position is None:
        return

    await _recalc_position(session, position.id)

    if position.is_budget or position.paid_at is not None:
        return

    # `_recalc_position` has just written `amount_actual`, so this reads the
    # total of every booking on the position, not only the one just made.
    if (
        position.amount_actual is not None
        and position.amount_actual >= position.amount_planned
    ):
        position.paid_at = datetime.now(UTC)


@router.delete("/{entry_id}", response_model=ImportedEntryRead)
async def discard(
    entry_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> ImportedEntry:
    """Throw an entry out without booking it.

    The row **stays**, with `discarded_at` set. Deleting it would mean the next
    import of the same file brings the entry straight back, and the user would
    have to throw it out again every month.
    """
    entry = await _load(session, entry_id, user)
    entry.discarded_at = datetime.now(UTC)
    await session.commit()
    await session.refresh(entry)
    return entry
