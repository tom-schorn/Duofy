"""make the household owner an admin

Revision ID: d4a7b2e9c813
Revises: a8f2c6d1e437
Create Date: 2026-09-29 21:00:00.000000

A household has admins instead of one owner (#254 step 3, decision 58). Several
admins are simply several members with the role `admin`, so only the stored value
changes: `owner` becomes `admin`. The column is a plain varchar without a CHECK,
so there is no constraint to adjust.

**Downgrade.** Every `admin` becomes `owner` again. The old code already allowed
several owners, so two admins come back as two owners and nobody loses a right.
"""

from typing import Sequence, Union

from alembic import op

# revision identifiers, used by Alembic.
revision: str = "d4a7b2e9c813"
down_revision: Union[str, Sequence[str], None] = "a8f2c6d1e437"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    """Upgrade schema."""
    op.execute("UPDATE household_members SET role = 'admin' WHERE role = 'owner'")


def downgrade() -> None:
    """Downgrade schema."""
    op.execute("UPDATE household_members SET role = 'owner' WHERE role = 'admin'")
