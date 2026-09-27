"""Hints on a plan — computed on every read, never stored.

The backend decides, the frontend only shows: a hint is a stable `code`, a
`severity` and the `params` its text needs, never the text itself. A stored hint
would be stale the moment a position is ticked off, so nothing here touches the
database.

One function per rule, all collected in `plan_hints`. A new rule is a new
function and one more line there.
"""

from calendar import monthrange
from collections.abc import Iterable
from datetime import date
from decimal import Decimal

from app.models.enums import Budget
from app.models.plan import PlanPosition
from app.schemas.plan import Hint, HintSeverity


def today() -> date:
    """Separate so tests can pin the day."""
    return date.today()


def _position_overdue(year: int, month: int, position: PlanPosition, now: date) -> Hint | None:
    """A commitment that fell due before today and is not ticked off.

    Overdue from the day **after** it fell due, with no grace period: the bank
    booking later does not change that the position is still open. A limit has no
    tick, so "not reached yet" is its normal state and it is never overdue.
    """
    if position.is_limit or position.paid_at is not None:
        return None
    day = min(position.due_day, monthrange(year, month)[1])
    due = date(year, month, day)
    if due >= now:
        return None
    return Hint(
        code="position_overdue",
        severity=HintSeverity.WARNING,
        position_id=position.id,
        params={"due_date": due.isoformat(), "days_overdue": (now - due).days},
    )


def _nothing_free(positions: list[PlanPosition]) -> Hint | None:
    """Everything the income brings in is already allocated — or more.

    Replaces the old buffer percentage: instead of setting money aside on paper,
    the plan says so when nothing is left over. Pass-through positions stay out,
    as in the totals. A month without any income says nothing: there is no
    remainder to speak of yet.
    """
    counting = [p for p in positions if not p.pass_through]
    income = sum((p.amount_planned for p in counting if p.budget is Budget.INCOME), Decimal(0))
    if income <= 0:
        return None
    allocated = sum(
        (p.amount_planned for p in counting if p.budget is not Budget.INCOME), Decimal(0)
    )
    free = income - allocated
    if free > 0:
        return None
    return Hint(code="plan_nothing_free", severity=HintSeverity.INFO, params={"free": str(free)})


def plan_hints(year: int, month: int, positions: Iterable[PlanPosition]) -> list[Hint]:
    """Every hint that applies to these positions of the given month."""
    now = today()
    positions = list(positions)
    hints: list[Hint] = []
    for position in positions:
        hint = _position_overdue(year, month, position, now)
        if hint is not None:
            hints.append(hint)
    nothing_free = _nothing_free(positions)
    if nothing_free is not None:
        hints.append(nothing_free)
    return hints
