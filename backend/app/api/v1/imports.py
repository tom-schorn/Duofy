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
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.auth import current_active_user
from app.core.permissions import Area, granted_level, require
from app.db.session import get_session
from app.models.account import Account
from app.models.enums import AccessLevel
from app.models.imported_entry import ImportedEntry
from app.models.plan import PlanPosition
from app.models.transaction import Transaction
from app.models.user import User
from app.schemas.imported_entry import (
    ImportedEntryRead,
    ImportedEntryUpdate,
    ImportSummary,
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


@router.get("", response_model=list[ImportedEntryRead])
async def list_entries(
    owner: uuid.UUID | None = None,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> list[ImportedEntry]:
    """The parking area, newest day first. Discarded rows stay out of the list."""
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
        .order_by(ImportedEntry.occurred_on.desc(), ImportedEntry.created_at)
    )
    return list(rows.scalars())


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
    file from bringing it back.
    """
    entry = await _load(session, entry_id, user)
    require(entry.category is not None, "category_missing")

    session.add(
        Transaction(
            owner_id=entry.owner_id,
            account_id=entry.account_id,
            occurred_on=entry.occurred_on,
            amount=entry.amount,
            note=entry.counterparty_name or entry.purpose,
            category=entry.category,
            block=entry.block,
            position_id=entry.position_id,
            external_ref=entry.external_ref,
        )
    )
    await session.delete(entry)
    await session.commit()
    return entry


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
