"""add personal default quotas to users

Revision ID: a3e8c5d14b72
Revises: f7c1a3d58e29
Create Date: 2026-09-27 01:00:00.000000

`users` gets the four numbers a new month starts from (#84): `target_needs`,
`target_wants`, `target_savings` and `buffer_percent`, the same shape as on
`plans` and `households`.

**Upgrade.** Every existing user receives 50 / 30 / 20 / 0 through the
`server_default`; existing months are not touched. The three quotas must add up
to 100 (`ck_user_targets_sum_100`).

**Downgrade.** The CHECK and the four columns go; whatever a person had set as
their default is lost, the months already created keep their own values.
"""
from typing import Sequence, Union

import sqlalchemy as sa
from alembic import op

# revision identifiers, used by Alembic.
revision: str = "a3e8c5d14b72"
down_revision: Union[str, Sequence[str], None] = "f7c1a3d58e29"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None

CHECK = "ck_user_targets_sum_100"
COLUMNS = (
    ("target_needs", "50.00"),
    ("target_wants", "30.00"),
    ("target_savings", "20.00"),
    ("buffer_percent", "0.00"),
)


def upgrade() -> None:
    """Upgrade schema."""
    for name, default in COLUMNS:
        op.add_column(
            "users",
            sa.Column(name, sa.Numeric(5, 2), nullable=False, server_default=default),
        )
    op.create_check_constraint(
        CHECK, "users", "target_needs + target_wants + target_savings = 100"
    )


def downgrade() -> None:
    """Downgrade schema."""
    op.drop_constraint(CHECK, "users", type_="check")
    for name, _ in reversed(COLUMNS):
        op.drop_column("users", name)
