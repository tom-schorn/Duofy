"""end a commitment on a date instead of switching it off

Revision ID: e5b2c7a94d16
Revises: d8a4e1b6c093
Create Date: 2026-09-26 16:00:00.000000

`commitments.active` was a switch and lost the *when*: switching a contract off in
December hid it in March too, and turning it back on brought it back into every
month, including the ones it had been off for. `ends_on` keeps the **last month**
in which the commitment is due (#109); empty means it runs indefinitely. Only the
year and month count, the day is never compared.

**Upgrade.** `active = false` becomes `ends_on = CURRENT_DATE`, the day of the
migration: the commitment still appears in the month of the migration and not in
the next one. `active = true` becomes NULL. Then `active` is dropped.

**Downgrade.** `active` comes back, true where `ends_on` is empty or its month is
the current one or later (`ends_on >= CURRENT_DATE` in the sense of the month, so
a contract ending this month is still active). **The dates are lost**: a
contract that ended last year and one that ended last week both come back as
`active = false`, and an end still in the future comes back as `active = true`
without its date.
"""
from typing import Sequence, Union

import sqlalchemy as sa
from alembic import op

# revision identifiers, used by Alembic.
revision: str = "e5b2c7a94d16"
down_revision: Union[str, Sequence[str], None] = "d8a4e1b6c093"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    """Upgrade schema."""
    op.add_column("commitments", sa.Column("ends_on", sa.Date(), nullable=True))
    op.execute(sa.text("UPDATE commitments SET ends_on = CURRENT_DATE WHERE NOT active"))
    op.drop_column("commitments", "active")


def downgrade() -> None:
    """Downgrade schema."""
    op.add_column(
        "commitments",
        sa.Column("active", sa.Boolean(), nullable=False, server_default=sa.true()),
    )
    op.execute(
        sa.text(
            "UPDATE commitments SET active = (ends_on IS NULL"
            " OR ends_on >= date_trunc('month', CURRENT_DATE)::date)"
        )
    )
    op.alter_column("commitments", "active", server_default=None)
    op.drop_column("commitments", "ends_on")
