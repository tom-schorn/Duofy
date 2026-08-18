"""Payment accounts — current, savings, card, wallet, cash.

Accounts are **private** by default, even inside a shared household: everyone sees
their own unless insight was granted. For joint planning what matters is what the
positions say, not where the money sits.

The balance is deliberately not a column. It follows from the opening balance plus
the bookings after it.
"""

import uuid
from calendar import monthrange
from datetime import date
from decimal import Decimal

from fastapi import APIRouter, Depends, HTTPException, Query, status
from sqlalchemy import and_, case, func, literal, select, update
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.auth import current_active_user
from app.core.permissions import (
    Area,
    granted_level,
    is_member,
    require,
    viewable_members,
)
from app.db.session import get_session
from app.models.account import Account
from app.models.enums import AccessLevel, Block
from app.models.transaction import Transaction
from app.models.user import User
from app.schemas.account import (
    AccountCreate,
    AccountRead,
    AccountUpdate,
    BalanceHistory,
    BalanceMoves,
    BalancePoint,
)

router = APIRouter()

ZERO = Decimal("0.00")


async def _clear_other_defaults(
    session: AsyncSession, user: User, keep: uuid.UUID | None = None
) -> None:
    """Clear the default flag on every other account of this user.

    A partial unique index in the database allows only one. Without this cleanup,
    switching the default would raise an integrity error instead of doing the
    obvious thing — a new default should replace the old one, not be rejected.
    """
    query = update(Account).where(Account.owner_id == user.id, Account.is_default)
    if keep is not None:
        query = query.where(Account.id != keep)
    await session.execute(query.values(is_default=False))


async def _load(session: AsyncSession, account_id: uuid.UUID, user: User) -> Account:
    account = await session.get(Account, account_id)
    if account is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "account_not_found"})
    require(account.owner_id == user.id, "not_account_owner")
    return account


async def _scope(
    session: AsyncSession,
    owner: uuid.UUID | None,
    household: uuid.UUID | None,
    user: User,
) -> list[uuid.UUID]:
    """Whose accounts are meant — one person, or everyone in a household.

    `household` wins if both are given. Inside a household only members who granted
    insight count, see `viewable_members`.
    """
    if household is not None:
        require(await is_member(session, user.id, household), "not_household_member")
        return await viewable_members(session, household, user.id, Area.ACCOUNTS)
    return [await _target_owner(session, owner, user)]


async def _target_owner(
    session: AsyncSession, owner: uuid.UUID | None, user: User
) -> uuid.UUID:
    """Whose accounts are meant — and whether the asker may see them.

    Without `owner`, your own. With `owner`, those of a household member who granted
    at least level `view`. The level is set by the owner themselves, see
    `AccessLevel`.
    """
    if owner is None or owner == user.id:
        return user.id

    level = await granted_level(session, owner, user.id, Area.ACCOUNTS)
    require(level.rank >= AccessLevel.VIEW.rank, "no_insight_granted")
    return owner


async def _balances(
    session: AsyncSession, owner_ids: list[uuid.UUID]
) -> dict[uuid.UUID, Decimal]:
    """How much every booking moved the balance of its account.

    A booking carries no sign; the direction lives elsewhere:

    * **transfer** — leaves `account_id`, arrives at `counter_account_id`.
    * **otherwise** — `block = income` is inbound, everything else outbound.

    Two sums, because one account can appear in both columns: as the source of one
    transfer and the target of the next.
    """
    outgoing = await session.execute(
        select(
            Transaction.account_id,
            func.sum(
                case(
                    # A transfer leaves the account, whatever the block says.
                    (Transaction.counter_account_id.isnot(None), -Transaction.amount),
                    (Transaction.block == Block.INCOME, Transaction.amount),
                    else_=-Transaction.amount,
                )
            ),
        )
        .where(Transaction.owner_id.in_(owner_ids))
        .group_by(Transaction.account_id)
    )
    incoming = await session.execute(
        select(Transaction.counter_account_id, func.sum(Transaction.amount))
        .where(
            Transaction.owner_id.in_(owner_ids),
            Transaction.counter_account_id.isnot(None),
        )
        .group_by(Transaction.counter_account_id)
    )

    moved: dict[uuid.UUID, Decimal] = {}
    for account_id, total in list(outgoing.all()) + list(incoming.all()):
        moved[account_id] = moved.get(account_id, Decimal("0.00")) + total
    return moved


async def _with_balance(
    session: AsyncSession, account: Account, owner_id: uuid.UUID
) -> AccountRead:
    """One account including its balance.

    Needed because `balance` is computed and does not live on the object: without
    this step `POST /accounts` answered with the default 0.00 even though an opening
    balance had just been set. A client showing that response would display a figure
    that never existed.
    """
    moved = await _balances(session, [owner_id])
    return AccountRead.model_validate(account).model_copy(
        update={"balance": account.opening_balance + moved.get(account.id, ZERO)}
    )


@router.get("", response_model=list[AccountRead])
async def list_accounts(
    owner: uuid.UUID | None = Query(default=None),
    household: uuid.UUID | None = Query(default=None),
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> list[AccountRead]:
    """Accounts including balances, closed ones last.

    With neither parameter, your own. With `owner`, those of one member; with
    `household`, those of every member who granted insight.
    """
    owner_ids = await _scope(session, owner, household, user)
    geteilt = household is not None

    result = await session.execute(
        select(Account, User.first_name)
        .join(User, User.id == Account.owner_id)
        .where(Account.owner_id.in_(owner_ids))
        .order_by(Account.active.desc(), User.first_name, Account.name)
    )
    moved = await _balances(session, owner_ids)

    return [
        AccountRead.model_validate(account).model_copy(
            update={
                "balance": account.opening_balance + moved.get(account.id, ZERO),
                # Only in the household view: there the accounts of several people
                # sit side by side and the name is what tells them apart.
                "owner_name": name if geteilt else None,
            }
        )
        for account, name in result.all()
    ]


def _delta(spendable: set[uuid.UUID] | None):
    """How much a booking moves the pot under consideration.

    Two pots, two calculations:

    * **all accounts** (`spendable is None`) — transfers are neutral: they move
      money between own accounts and the total does not change.
    * **spendable accounts only** — a transfer to savings **leaves** the pot and
      counts as an outflow. That is the point of the flag on the account: money
      sitting there cannot be spent a second time.

    The second pot is the one the book wants to show. Only with it do the daily
    movement bars and the balance line add up: a transfer lowers the line by exactly
    the bar it produces.
    """
    if spendable is None:
        return case(
            (Transaction.counter_account_id.isnot(None), literal(0)),
            (Transaction.block == Block.INCOME, Transaction.amount),
            else_=-Transaction.amount,
        )

    is_transfer = Transaction.counter_account_id.isnot(None)
    source_spendable = Transaction.account_id.in_(spendable)
    target_spendable = Transaction.counter_account_id.in_(spendable)

    return case(
        # Leaves the spendable pot.
        (and_(is_transfer, source_spendable, target_spendable.is_(False)), -Transaction.amount),
        # Comes back, for instance pulling money out of savings again.
        (and_(is_transfer, source_spendable.is_(False), target_spendable), Transaction.amount),
        # Inside the pot or entirely outside it: neutral.
        (is_transfer, literal(0)),
        # A normal booking only counts if it touches a spendable account.
        (source_spendable.is_(False), literal(0)),
        (Transaction.block == Block.INCOME, Transaction.amount),
        else_=-Transaction.amount,
    )


@router.get("/history", response_model=BalanceHistory)
async def balance_history(
    year: int = Query(ge=2000, le=2100),
    month: int = Query(ge=1, le=12),
    owner: uuid.UUID | None = Query(default=None),
    household: uuid.UUID | None = Query(default=None),
    # An explicit alias: the camelCase generator applies to schemas, not to query
    # parameters, and the URL should still look like the rest of the API.
    only_available: bool = Query(default=False, alias="onlyAvailable"),
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> BalanceHistory:
    """How the overall balance developed across one calendar month.

    The counterpart to the flow chart in the plan: that one shows how the month was
    meant to go, this one how it actually went.

    **By date, not by position.** The book assigns a booking to the month of its
    position, which would be wrong for a balance curve — the money was on the
    account on the day it moved, whatever month it was earmarked for.

    Must be declared **before** `/{account_id}`, otherwise FastAPI tries to read
    "history" as a UUID.
    """
    owner_ids = await _scope(session, owner, household, user)
    first = date(year, month, 1)
    last = date(year, month, monthrange(year, month)[1])

    accounts = await session.execute(
        select(Account.id, Account.opening_balance, Account.counts_as_available).where(
            Account.owner_id.in_(owner_ids)
        )
    )
    rows = accounts.all()
    spendable = {account_id for account_id, _, counts in rows if counts} if only_available else None
    opening = sum(
        (
            balance
            for account_id, balance, counts in rows
            if spendable is None or account_id in spendable
        ),
        ZERO,
    )

    delta = _delta(spendable)
    before = await session.scalar(
        select(func.coalesce(func.sum(delta), ZERO)).where(
            Transaction.owner_id.in_(owner_ids), Transaction.occurred_on < first
        )
    )

    # Grouped by day **and block** so the chart can break the movement down. Pure
    # transfers have no block; they land under `savings`, because a transfer leaving
    # the spendable pot is money put aside.
    daily = await session.execute(
        select(Transaction.occurred_on, Transaction.block, func.sum(delta))
        .where(
            Transaction.owner_id.in_(owner_ids),
            Transaction.occurred_on >= first,
            Transaction.occurred_on <= last,
        )
        .group_by(Transaction.occurred_on, Transaction.block)
        .order_by(Transaction.occurred_on)
    )

    nach_tag: dict[date, dict[str, Decimal]] = {}
    for day, block, betrag in daily.all():
        eimer = nach_tag.setdefault(
            day, {"income": ZERO, "needs": ZERO, "wants": ZERO, "savings": ZERO}
        )
        if betrag > 0:
            # Anything entering the pot: income, or money pulled back in.
            eimer["income"] += betrag
        elif betrag < 0:
            schluessel = block.value if block in (Block.NEEDS, Block.WANTS) else "savings"
            eimer[schluessel] += -betrag

    # One point per day **with** movement. The chart fills the days in between as a
    # step — they carry no information of their own.
    running = opening + (before or ZERO)
    points = []
    for day in sorted(nach_tag):
        eimer = nach_tag[day]
        veraenderung = eimer["income"] - eimer["needs"] - eimer["wants"] - eimer["savings"]
        running += veraenderung
        points.append(
            BalancePoint(
                day=day,
                balance=running,
                change=veraenderung,
                moves=BalanceMoves(**eimer),
            )
        )

    return BalanceHistory(
        opening_balance=opening + (before or ZERO),
        closing_balance=running,
        points=points,
    )


@router.post("", response_model=AccountRead, status_code=status.HTTP_201_CREATED)
async def create_account(
    payload: AccountCreate,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> AccountRead:
    if payload.is_default:
        await _clear_other_defaults(session, user)

    account = Account(owner_id=user.id, **payload.model_dump())
    session.add(account)
    await session.commit()
    await session.refresh(account)
    return await _with_balance(session, account, user.id)


@router.patch("/{account_id}", response_model=AccountRead)
async def update_account(
    account_id: uuid.UUID,
    payload: AccountUpdate,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> AccountRead:
    account = await _load(session, account_id, user)
    changes = payload.model_dump(exclude_unset=True)

    if changes.get("is_default"):
        await _clear_other_defaults(session, user, keep=account.id)

    for field, value in changes.items():
        setattr(account, field, value)

    await session.commit()
    await session.refresh(account)
    return await _with_balance(session, account, user.id)


@router.delete("/{account_id}", status_code=status.HTTP_204_NO_CONTENT)
async def delete_account(
    account_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> None:
    """Deleting is for mistakes.

    An account no longer in use is set to `active = false` — that way its bookings
    keep their reference.
    """
    account = await _load(session, account_id, user)
    await session.delete(account)
    await session.commit()
