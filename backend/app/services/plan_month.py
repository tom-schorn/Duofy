"""Which plan month a booking counts in (#239).

One rule, kept in one place because four endpoints need it — creating and changing
a booking, ticking a position off, and booking an import:

* **with a position** the plan of the position decides; nobody chooses
* **without a position** the previous, own or next month of the date; the date's
  month when nothing is said
* a **carry-over** and a **pure transfer** count in the month of their date, fixed

A transfer that also has a position (moving money to savings) fulfils that
position's quota, so the position wins there.
"""

import uuid
from datetime import date

from fastapi import HTTPException, status
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.enums import TransactionKind
from app.models.plan import Plan, PlanPosition

PlanMonth = tuple[int, int]


def month_of(day: date) -> PlanMonth:
    return day.year, day.month


def _index(month: PlanMonth) -> int:
    return month[0] * 12 + month[1] - 1


def _from_index(index: int) -> PlanMonth:
    return index // 12, index % 12 + 1


def offset_from(day: date, month: PlanMonth) -> int:
    """How many months `month` lies after the month of `day`."""
    return _index(month) - _index(month_of(day))


def shift(day: date, offset: int) -> PlanMonth:
    return _from_index(_index(month_of(day)) + offset)



def is_unplanned(
    kind: TransactionKind, position_id: uuid.UUID | None, is_transfer: bool
) -> bool:
    """On no position, and neither a transfer nor a carry-over: the "Ungeplant" of
    the plan, the same rule its sum per budget uses (#240)."""
    return position_id is None and not is_transfer and kind is not TransactionKind.CARRY_OVER


async def month_of_position(session: AsyncSession, position_id: uuid.UUID) -> PlanMonth:
    position = await session.get(PlanPosition, position_id)
    plan = await session.get(Plan, position.plan_id) if position is not None else None
    if plan is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "position_not_found"})
    return plan.year, plan.month


def _refuse(code: str) -> HTTPException:
    return HTTPException(status.HTTP_422_UNPROCESSABLE_CONTENT, detail={"code": code})


async def resolve(
    session: AsyncSession,
    *,
    kind: TransactionKind,
    occurred_on: date,
    position_id: uuid.UUID | None,
    is_transfer: bool,
    chosen: PlanMonth | None,
    fallback_offset: int = 0,
) -> PlanMonth:
    """The plan month for a booking that is being created or changed.

    `chosen` is what the person picked, `None` when they said nothing.
    `fallback_offset` is what to use then, without a position: 0 is the date's
    month, and a change of date passes the offset the booking had so that a choice
    of "next month" travels with it.
    """
    if kind is TransactionKind.CARRY_OVER or (is_transfer and position_id is None):
        if chosen is not None:
            raise _refuse("plan_month_fixed")
        return month_of(occurred_on)

    if position_id is not None:
        if chosen is not None:
            raise _refuse("plan_month_follows_position")
        return await month_of_position(session, position_id)

    if chosen is None:
        return shift(occurred_on, fallback_offset if abs(fallback_offset) <= 1 else 0)
    if abs(offset_from(occurred_on, chosen)) > 1:
        raise _refuse("plan_month_out_of_range")
    return chosen
