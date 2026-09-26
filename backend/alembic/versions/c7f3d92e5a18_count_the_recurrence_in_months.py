"""count the recurrence in months

Revision ID: c7f3d92e5a18
Revises: b4e2a7d15f38
Create Date: 2026-09-26 10:30:00.000000

`commitments.rhythm` held one of four words. Real contracts run every 2, 4 or
18 months, so it becomes `interval_months`, a number from 1 to 120 (#107):

    monthly -> 1     quarterly -> 3     biannual -> 6     annual -> 12

Every existing commitment keeps falling due in exactly the same months; the
formula that says which ones is in `Commitment.is_due_in`, and a test compares
it with the old rule.

The order matters on a filled table: add the column **nullable**, fill it from
`rhythm`, only then make it NOT NULL. No `server_default` is needed for that (a
default would only give a raw INSERT a value nobody chose), and a row whose
`rhythm` is none of the four words stays NULL and makes `SET NOT NULL` fail
loudly instead of getting an invented number. `ck_commitment_first_due_date_required`
kept its name and now reads `interval_months = 1`; the range gets its own CHECK.

**The downgrade cannot always go back.** Only 1, 3, 6 and 12 have a word. A
commitment every 5 months has none, and rounding it to 6 would change when it
falls due without anybody having said so. So the downgrade **stops** if such a
row exists, names each one (id and name) and changes nothing: the check runs
before the first statement. Give those commitments an interval of 1, 3, 6
or 12 (or delete them) and run it again.
"""
from typing import Sequence, Union

import sqlalchemy as sa
from alembic import op

# revision identifiers, used by Alembic.
revision: str = "c7f3d92e5a18"
down_revision: Union[str, Sequence[str], None] = "b4e2a7d15f38"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None

FIRST_DUE_DATE_CHECK = "ck_commitment_first_due_date_required"
RANGE_CHECK = "ck_commitment_interval_months_range"


def upgrade() -> None:
    """Upgrade schema."""
    op.add_column("commitments", sa.Column("interval_months", sa.Integer(), nullable=True))
    op.execute(
        sa.text(
            "UPDATE commitments SET interval_months = CASE rhythm "
            "WHEN 'monthly' THEN 1 WHEN 'quarterly' THEN 3 "
            "WHEN 'biannual' THEN 6 WHEN 'annual' THEN 12 END"
        )
    )
    op.alter_column("commitments", "interval_months", nullable=False)

    op.drop_constraint(FIRST_DUE_DATE_CHECK, "commitments", type_="check")
    op.drop_column("commitments", "rhythm")
    op.create_check_constraint(
        FIRST_DUE_DATE_CHECK, "commitments", "interval_months = 1 OR first_due_date IS NOT NULL"
    )
    op.create_check_constraint(RANGE_CHECK, "commitments", "interval_months BETWEEN 1 AND 120")


def downgrade() -> None:
    """Downgrade schema."""
    # Before anything is changed: what has no word cannot go back, see the docstring.
    stuck = op.get_bind().execute(
        sa.text(
            "SELECT id, name, interval_months FROM commitments "
            "WHERE interval_months NOT IN (1, 3, 6, 12) ORDER BY name, id"
        )
    ).all()
    if stuck:
        listing = "; ".join(
            f"{row.id} ({row.name}, every {row.interval_months} months)" for row in stuck
        )
        raise RuntimeError(
            "Cannot downgrade: these commitments recur at an interval the old rhythm "
            "cannot express (only 1, 3, 6 and 12 months can) and rounding them would move "
            "their due months. Change or delete them first, then run the downgrade "
            f"again: {listing}"
        )

    op.add_column("commitments", sa.Column("rhythm", sa.String(length=20), nullable=True))
    op.execute(
        sa.text(
            "UPDATE commitments SET rhythm = CASE interval_months "
            "WHEN 1 THEN 'monthly' WHEN 3 THEN 'quarterly' "
            "WHEN 6 THEN 'biannual' WHEN 12 THEN 'annual' END"
        )
    )
    op.alter_column("commitments", "rhythm", nullable=False)

    op.drop_constraint(RANGE_CHECK, "commitments", type_="check")
    op.drop_constraint(FIRST_DUE_DATE_CHECK, "commitments", type_="check")
    op.drop_column("commitments", "interval_months")
    op.create_check_constraint(
        FIRST_DUE_DATE_CHECK, "commitments", "rhythm = 'monthly' OR first_due_date IS NOT NULL"
    )
