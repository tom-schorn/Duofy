"""add flow_limits_by to users

Revision ID: a3e6c9d2b715
Revises: f7c1a3d58e29
Create Date: 2026-09-27 01:00:00.000000

The flow chart counts limit positions either by plan or by bookings (#93). The
choice belongs to the person and lives on the server, so it follows them across
sessions and devices.

**Upgrade.** A new NOT NULL column with `server_default 'plan'`, so every existing
user starts on the plan. A CHECK keeps it to the two known values.

**Downgrade.** The CHECK and the column go; what people chose is lost, nothing else.
"""
from typing import Sequence, Union

import sqlalchemy as sa
from alembic import op

# revision identifiers, used by Alembic.
revision: str = "a3e6c9d2b715"
down_revision: Union[str, Sequence[str], None] = "f7c1a3d58e29"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None

CHECK = "ck_user_flow_limits_by"


def upgrade() -> None:
    """Upgrade schema."""
    op.add_column(
        "users",
        sa.Column("flow_limits_by", sa.String(length=20), nullable=False, server_default="plan"),
    )
    op.create_check_constraint(CHECK, "users", "flow_limits_by IN ('plan', 'bookings')")


def downgrade() -> None:
    """Downgrade schema."""
    op.drop_constraint(CHECK, "users", type_="check")
    op.drop_column("users", "flow_limits_by")
