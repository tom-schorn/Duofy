"""drop the buffer percentage

Revision ID: b4d7e19c3a58
Revises: a2c6e94b1d70
Create Date: 2026-09-27 12:00:00.000000

`buffer_percent` leaves `users`, `plans` and `households` (#191). A percentage set
aside on paper was unclear; the plan now says so with a hint when nothing is free.

**Upgrade.** The three columns are dropped; whatever was set is lost. The
distributable amount of a month becomes the whole income.

**Downgrade.** The columns come back on all three tables, NOT NULL with 0.00 —
the values that were dropped cannot be restored.
"""
from typing import Sequence, Union

import sqlalchemy as sa
from alembic import op

# revision identifiers, used by Alembic.
revision: str = "b4d7e19c3a58"
down_revision: Union[str, Sequence[str], None] = "a2c6e94b1d70"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None

TABLES = ("users", "plans", "households")


def upgrade() -> None:
    """Upgrade schema."""
    for table in TABLES:
        op.drop_column(table, "buffer_percent")


def downgrade() -> None:
    """Downgrade schema."""
    for table in TABLES:
        op.add_column(
            table,
            sa.Column(
                "buffer_percent", sa.Numeric(5, 2), nullable=False, server_default="0.00"
            ),
        )
