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


def plan_hints(year: int, month: int, positions: Iterable[PlanPosition]) -> list[Hint]:
    """Every hint that applies to these positions of the given month."""
    now = today()
    hints: list[Hint] = []
    for position in positions:
        hint = _position_overdue(year, month, position, now)
        if hint is not None:
            hints.append(hint)
    return hints
