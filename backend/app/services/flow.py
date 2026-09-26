"""The flow of a plan month: when does money come in, when does it go out.

Plan until something is booked, then bookings: an open commitment counts with its
planned amount on its due day; a ticked-off one is replaced by its bookings, with
real dates and real amounts. Manual bookings on the account count as they are.
Limit positions follow the viewer's `flow_limits_by`, never an estimate.

The curve is for **one account**, the default account of the plan owner. The
household view merges the curves of its members. Pure functions over loaded rows,
so the rules can be read (and tested) without a request around them.

A ticked-off position without any booking counts nothing: its plan is replaced by
what was booked, and nothing was booked.
"""

import uuid
from calendar import monthrange
from dataclasses import dataclass
from datetime import date
from decimal import Decimal

from app.models.enums import Budget, FlowLimitsBy
from app.models.plan import PlanPosition
from app.models.transaction import Transaction
from app.schemas.flow import FlowDay, FlowEntry, FlowEntryKind, FlowRead
from app.schemas.plan import Hint, HintSeverity

ZERO = Decimal("0.00")

#: date, signed amount, kind, label, position
Raw = tuple[date, Decimal, FlowEntryKind, str, uuid.UUID | None]


@dataclass
class Source:
    """What one person contributes: a default account, positions and bookings."""

    account_id: uuid.UUID | None
    account_name: str | None
    positions: list[PlanPosition]
    #: Bookings linked to those positions, plus manual ones on the account.
    transactions: list[Transaction]


def _booking_effect(tx: Transaction, account_id: uuid.UUID | None) -> Decimal | None:
    """What a booking does to this account, `None` if it does not touch it."""
    if tx.counter_account_id is not None:
        if tx.account_id == account_id:
            return -tx.amount
        if tx.counter_account_id == account_id:
            return tx.amount
        return None
    if tx.account_id != account_id:
        return None
    return tx.amount if tx.budget is Budget.INCOME else -tx.amount


def _plan_effect(position: PlanPosition, account_id: uuid.UUID | None) -> Decimal | None:
    """Same for a position. One without an account counts as the default account."""
    amount = position.amount_planned
    source = position.account_id or account_id
    if position.counter_account_id is not None:
        if source == account_id:
            return -amount
        if position.counter_account_id == account_id:
            return amount
        return None
    if source != account_id:
        return None
    return amount if position.budget is Budget.INCOME else -amount


def _clamp_day(when: date, year: int, month: int) -> int:
    """The day of the month the curve moves on; outside the month it is an edge."""
    if (when.year, when.month) < (year, month):
        return 1
    if (when.year, when.month) > (year, month):
        return monthrange(year, month)[1]
    return when.day


def _entries(source: Source, year: int, month: int, limits_by: FlowLimitsBy) -> list[Raw]:
    last = monthrange(year, month)[1]
    by_position: dict[uuid.UUID, list[Transaction]] = {}
    manual: list[Transaction] = []
    for tx in source.transactions:
        if tx.position_id is None:
            manual.append(tx)
        else:
            by_position.setdefault(tx.position_id, []).append(tx)

    out: list[Raw] = []

    for position in source.positions:
        # A limit follows the switch; anything else counts by plan until it is
        # ticked off, then by what was booked.
        by_plan = limits_by is FlowLimitsBy.PLAN if position.is_limit else position.paid_at is None
        if by_plan:
            effect = _plan_effect(position, source.account_id)
            if effect is not None:
                due = date(year, month, min(position.due_day, last))
                out.append((due, effect, FlowEntryKind.PLAN, position.label, position.id))
            continue
        for tx in by_position.get(position.id, []):
            effect = _booking_effect(tx, source.account_id)
            if effect is not None:
                out.append(
                    (tx.occurred_on, effect, FlowEntryKind.BOOKING, position.label, position.id)
                )

    for tx in manual:
        effect = _booking_effect(tx, source.account_id)
        if effect is not None:
            out.append((tx.occurred_on, effect, FlowEntryKind.BOOKING, tx.note or "", None))
    return out


def build_flow(
    sources: list[Source],
    year: int,
    month: int,
    limits_by: FlowLimitsBy,
    *,
    merged: bool,
    start: Decimal = ZERO,
) -> FlowRead:
    """The curve over every source. `merged` is the household view: one curve with
    no single account to name. `start` is where the curve begins: zero, or the
    carry-over of the default account when the month has one."""
    raw = [e for source in sources for e in _entries(source, year, month, limits_by)]
    raw.sort(key=lambda e: (_clamp_day(e[0], year, month), e[0], -e[1]))

    entries: list[FlowEntry] = []
    balance = start
    for when, amount, kind, label, position_id in raw:
        balance += amount
        entries.append(
            FlowEntry(
                date=when,
                day=_clamp_day(when, year, month),
                amount=amount,
                kind=kind,
                label=label,
                position_id=position_id,
                balance=balance,
            )
        )

    days: list[FlowDay] = []
    running = start
    cursor = 0
    for day in range(1, monthrange(year, month)[1] + 1):
        while cursor < len(entries) and entries[cursor].day == day:
            running = entries[cursor].balance
            cursor += 1
        days.append(FlowDay(day=day, balance=running))

    hints: list[Hint] = []
    lowest = min(days, key=lambda d: (d.balance, d.day))
    if lowest.balance < 0:
        single = sources[0] if not merged and len(sources) == 1 else None
        hints.append(
            Hint(
                code="flow_shortfall",
                severity=HintSeverity.WARNING,
                position_id=None,
                params={
                    "day": lowest.day,
                    "amount": str(-lowest.balance),
                    "account_id": str(single.account_id) if single and single.account_id else None,
                    "account_name": single.account_name if single else None,
                },
            )
        )

    return FlowRead(
        year=year,
        month=month,
        flow_limits_by=limits_by,
        start=start,
        entries=entries,
        days=days,
        hints=hints,
    )
