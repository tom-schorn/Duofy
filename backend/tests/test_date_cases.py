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
    commitment = Commitment(due_day=case["due_day"])
    assert commitment.effective_due_day(case["year"], case["month"]) == case["expected"]
