"""keep the due day only in the first due date

Revision ID: d8a4e1b6c093
Revises: c7f3d92e5a18
Create Date: 2026-09-26 14:00:00.000000

A commitment stored its due day twice: `due_day` and, for anything but a monthly
one, `first_due_date`, with a validator holding them together (#108). Now the
date is the only source and `due_day` goes. `commitments.first_due_date` becomes
NOT NULL, so **a monthly commitment needs a start date too**.

Existing dates are kept as they are. Rows without one (until now only monthly
ones) get a start, decided with Tom on 26.09.:

* **Which month.** The earliest plan month in which the commitment already has a
  position — that is when it really started running. Without any position, the
  current month at the time of the migration, even if its day has already
  passed: the next plan creation then simply has it in that month.
* **Which day.** The old `due_day`.
* **A day the month does not have** (the 31st in February, the 31st in a 30-day
  month): the start moves to the next month that has it, at most two months on.
  Clamping to the 28th would change the due day for good, because from now on
  the day is read from this very date.

The two CHECK constraints on `due_day` and on the first due date are dropped;
NOT NULL replaces the second.

**Downgrade.** `due_day` comes back, filled from the day of `first_due_date`,
with its CHECK; `first_due_date` may be NULL again and the CHECK on it returns.
The dates the upgrade filled **stay**: they are valid under the old rules, and
telling them apart from the ones people entered would mean a guess.
"""
from typing import Sequence, Union

import sqlalchemy as sa
from alembic import op

# revision identifiers, used by Alembic.
revision: str = "d8a4e1b6c093"
down_revision: Union[str, Sequence[str], None] = "c7f3d92e5a18"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None

FIRST_DUE_DATE_CHECK = "ck_commitment_first_due_date_required"
DUE_DAY_CHECK = "ck_commitment_due_day"

#: `m` counts months as year * 12 + (month - 1). A start is tried in `m`, `m + 1`
#: and `m + 2`; the first month that has the day wins (every day exists in at
#: least one of any two consecutive months, so three tries are plenty).
BACKFILL = """
UPDATE commitments AS c SET first_due_date = (
    SELECT candidate.month_start + (c.due_day - 1)
    FROM (
        SELECT o AS step,
               make_date((s.m + o) / 12, (s.m + o) % 12 + 1, 1) AS month_start
        FROM generate_series(0, 2) AS o
    ) AS candidate
    WHERE c.due_day <= EXTRACT(
        DAY FROM (candidate.month_start + INTERVAL '1 month' - INTERVAL '1 day')
    )
    ORDER BY candidate.step
    LIMIT 1
)
FROM (
    SELECT c2.id,
           COALESCE(
               (SELECT MIN(p.year * 12 + p.month - 1)
                FROM plan_positions AS pp JOIN plans AS p ON p.id = pp.plan_id
                WHERE pp.commitment_id = c2.id),
               EXTRACT(YEAR FROM CURRENT_DATE)::int * 12
               + EXTRACT(MONTH FROM CURRENT_DATE)::int - 1
           ) AS m
    FROM commitments AS c2
    WHERE c2.first_due_date IS NULL
) AS s
WHERE c.id = s.id
"""


def upgrade() -> None:
    """Upgrade schema."""
    op.execute(sa.text(BACKFILL))
    op.alter_column("commitments", "first_due_date", nullable=False)
    op.drop_constraint(FIRST_DUE_DATE_CHECK, "commitments", type_="check")
    op.drop_constraint(DUE_DAY_CHECK, "commitments", type_="check")
    op.drop_column("commitments", "due_day")


def downgrade() -> None:
    """Downgrade schema."""
    op.add_column("commitments", sa.Column("due_day", sa.Integer(), nullable=True))
    op.execute(sa.text("UPDATE commitments SET due_day = EXTRACT(DAY FROM first_due_date)"))
    op.alter_column("commitments", "due_day", nullable=False)
    op.create_check_constraint(DUE_DAY_CHECK, "commitments", "due_day BETWEEN 1 AND 31")

    op.alter_column("commitments", "first_due_date", nullable=True)
    op.create_check_constraint(
        FIRST_DUE_DATE_CHECK, "commitments", "interval_months = 1 OR first_due_date IS NOT NULL"
    )
