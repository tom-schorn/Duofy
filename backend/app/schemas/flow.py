import uuid
from datetime import date
from decimal import Decimal
from enum import StrEnum

from app.models.enums import FlowLimitsBy
from app.schemas.base import Schema
from app.schemas.plan import Hint


class FlowEntryKind(StrEnum):
    #: Still a plan: the planned amount on its due day.
    PLAN = "plan"
    #: Happened: a booking with its real date and amount.
    BOOKING = "booking"


class FlowEntry(Schema):
    """One movement on the curve. The amount carries the sign: income plus."""

    #: The real date. A booking can lie outside the month, then `day` is clamped.
    date: date
    #: The day of the month the curve moves on, `1` to the last day.
    day: int
    amount: Decimal
    kind: FlowEntryKind
    #: The position or the note. Empty on a manual booking without a note.
    label: str
    position_id: uuid.UUID | None = None
    #: The curve after this entry.
    balance: Decimal


class FlowDay(Schema):
    day: int
    #: After every movement of that day.
    balance: Decimal


class FlowRead(Schema):
    """The flow of one plan month, ready to draw.

    The curve starts at zero: it shows the change since the 1st, not a balance.
    Computed on the server so the shortfall is a statement, not a drawing.
    """

    year: int
    month: int
    #: The viewer's setting, echoed so the switch can show it.
    flow_limits_by: FlowLimitsBy
    start: Decimal
    entries: list[FlowEntry]
    days: list[FlowDay]
    hints: list[Hint]
