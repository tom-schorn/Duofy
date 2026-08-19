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
from datetime import UTC, datetime

from fastapi import APIRouter, Depends, HTTPException, UploadFile, status
from sqlalchemy import select, tuple_
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
from app.services.camt import CamtError, read_upload

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
    """Read a CAMT file and park what it holds.

    The account comes **from the file**: a report names the IBAN whose turnover
    it contains. On the first upload of an unknown IBAN the client is asked once
    which account is meant and repeats the call with `account=`; the IBAN is then
    remembered on that account and never asked again.
    """
    owner_id = owner or user.id
    await _may_act_for(session, owner_id, user)

    try:
        reports = read_upload(await _read_body(file))
    except CamtError as error:
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
            Transaction.account_id == account_id, Transaction.external_ref.in_(refs)
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
    """
    months = {(entry.occurred_on.year, entry.occurred_on.month) for entry in entries}
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


def _position_for(
    entry: ImportedEntry,
    category: Category,
    positions: dict[tuple[int, int, Category], list[PlanPosition]],
) -> uuid.UUID | None:
    """The position this entry most likely belongs to, or none.

    One candidate means it is that one — a paid position included, since a
    budget takes many bookings and a single payment may arrive in instalments.

    Several candidates are what the amount is for: the closest match wins, and
    only if it is within a euro. Two insurances of 18.40 and 91.00 are told
    apart that way; two of 18.40 and 18.39 are not, and then nothing is offered
    rather than a coin toss.
    """
    candidates = positions.get((entry.occurred_on.year, entry.occurred_on.month, category))
    if not candidates:
        return None
    if len(candidates) == 1:
        return candidates[0].id

    closest = min(candidates, key=lambda p: abs(p.amount_planned - entry.amount))
    return closest.id if abs(closest.amount_planned - entry.amount) <= 1 else None


def _suggest(
    entry: ImportedEntry,
    learned: dict[uuid.UUID, tuple[Category, str]],
    positions: dict[tuple[int, int, Category], list[PlanPosition]],
) -> Suggestion | None:
    """One suggestion, or none.

    Two cases, and the second needs nothing learned at all:

    * **Nothing assigned yet** — the counterparty says what this was last time,
      and a matching budget position comes along with it
    * **A category chosen by hand, no position** — the budget position follows
      from that category alone. Leaving this out meant the moment somebody
      decided for themselves, the import stopped helping — exactly when the
      answer was most certain

    An entry that already has a position is left alone. There is nothing left to
    suggest, and second-guessing a decision is not a suggestion.
    """
    if entry.position_id is not None:
        return None

    if entry.category is not None:
        position_id = _position_for(entry, entry.category, positions)
        if position_id is None:
            return None
        return Suggestion(
            category=entry.category,
            position_id=position_id,
            reason="einziger Posten dieses Monats für deine Kategorie",
        )

    hit = learned.get(entry.id)
    if hit is None:
        return None

    category, how = hit
    where = "der IBAN" if how == "iban" else "dem Namen"
    return Suggestion(
        category=category,
        position_id=_position_for(entry, category, positions),
        reason=f"zuletzt so gebucht, erkannt an {where}",
    )


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

    learned = await _learned(session, owner_id, entries)
    positions = await _positions_by_category(session, owner_id, entries)

    return [
        ImportedEntryRead.model_validate(entry).model_copy(
            update={"suggestion": _suggest(entry, learned, positions)}
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
    """Put a position or a category on a parked entry.

    A position wins over a category: `PlanPosition.category` is not nullable, so
    a position always carries one, and having the two disagree would leave the
    booking with a category its position does not share.
    """
    entry = await _load(session, entry_id, user)
    changes = payload.model_dump(exclude_unset=True)

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

    await session.commit()
    await session.refresh(entry)
    return entry


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

    ## A savings position makes this a transfer

    `PlanPosition.counter_account_id` says where the money goes when it moves to
    another own account, and `mark_paid` has always honoured it. Booking an
    import has to do the same: without it a transfer to the savings account
    becomes an **expense**, the savings account never sees the money, and the
    savings quota stays empty while the money is demonstrably saved.
    """
    entry = await _load(session, entry_id, user)
    require(entry.category is not None, "category_missing")

    position = (
        await session.get(PlanPosition, entry.position_id)
        if entry.position_id is not None
        else None
    )
    counter_account_id = position.counter_account_id if position else None

    session.add(
        Transaction(
            owner_id=entry.owner_id,
            account_id=entry.account_id,
            counter_account_id=counter_account_id,
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
