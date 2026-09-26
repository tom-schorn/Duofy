"""The date cases shared with the frontend (`testdata/date_cases.json`).

The rule that the 31st becomes the last day of a shorter month exists twice:
`Commitment.effective_due_day` here, `effectiveDueDay` in `domain.ts`. If the two
drift apart, a position shows one due day in the plan and another in the backend,
and nobody notices. Both suites read the same table, so a disagreement fails one
of them — same idea as `test_enums_match_frontend.py`.

No database needed: the commitment is never stored.

Part of #13.
"""

import json
from calendar import monthrange
from datetime import date
from pathlib import Path

import pytest

from app.models.commitment import Commitment

CASES = json.loads(
    (Path(__file__).parents[2] / "testdata" / "date_cases.json").read_text(encoding="utf-8")
)


@pytest.mark.parametrize(
    "case", CASES["days_in_month"], ids=[case["case"] for case in CASES["days_in_month"]]
)
def test_days_in_month_as_the_frontend_counts_them(case):
    """`effective_due_day` clamps against `monthrange`, so that is what counts here."""
    assert monthrange(case["year"], case["month"])[1] == case["expected"]


@pytest.mark.parametrize(
    "case",
    CASES["effective_due_day"],
    ids=[case["case"] for case in CASES["effective_due_day"]],
)
def test_effective_due_day_as_the_frontend_clamps_it(case):
    # January has 31 days, so every day of the cases exists in the start date.
    commitment = Commitment(first_due_date=date(2027, 1, case["due_day"]))
    assert commitment.effective_due_day(case["year"], case["month"]) == case["expected"]


@pytest.mark.parametrize(
    "case", CASES["is_due_in"], ids=[case["case"] for case in CASES["is_due_in"]]
)
def test_is_due_in_as_the_frontend_counts_it(case):
    commitment = Commitment(
        interval_months=case["interval_months"],
        first_due_date=date.fromisoformat(case["first_due_date"]),
    )
    assert commitment.is_due_in(case["year"], case["month"]) is case["expected"]


def _old_is_due_in(interval: int, start: date, year: int, month: int) -> bool:
    """The rule from before #107, kept as a witness: months counted within the year."""
    if (year, month) < (start.year, start.month):
        return False
    if interval == 1:
        return True
    return (month - start.month) % interval == 0


@pytest.mark.parametrize("interval", [1, 3, 6, 12])
@pytest.mark.parametrize("start_month", range(1, 13))
def test_the_four_old_recurrences_fall_due_in_the_same_months_as_before(interval, start_month):
    """The migration must not move a single month of any existing commitment."""
    start = date(2026, start_month, 1)
    commitment = Commitment(interval_months=interval, first_due_date=start)
    for offset in range(-6, 31):
        total = 2026 * 12 + start_month - 1 + offset
        year, month = divmod(total, 12)
        month += 1
        assert commitment.is_due_in(year, month) == _old_is_due_in(interval, start, year, month)


def test_a_commitment_is_due_up_to_and_including_the_month_of_its_end():
    """`ends_on` is the last month; only year and month count, not the day."""
    commitment = Commitment(
        interval_months=1, first_due_date=date(2026, 1, 15), ends_on=date(2026, 9, 1)
    )
    assert commitment.is_due_in(2026, 9) is True
    assert commitment.is_due_in(2026, 10) is False
    assert commitment.is_due_in(2027, 1) is False

    end_of_month = Commitment(
        interval_months=1, first_due_date=date(2026, 1, 15), ends_on=date(2026, 9, 30)
    )
    assert end_of_month.is_due_in(2026, 9) is True
    assert end_of_month.is_due_in(2026, 10) is False


def test_a_commitment_without_an_end_is_due_in_every_month_of_its_cadence():
    commitment = Commitment(interval_months=1, first_due_date=date(2026, 1, 15), ends_on=None)
    assert all(commitment.is_due_in(2040, month) for month in range(1, 13))
