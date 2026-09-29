"""mark private positions and commitments

Revision ID: a5c9d2e7f104
Revises: c5a8e2f7d941
Create Date: 2026-09-29 10:00:00.000000

First of three steps that give every person exactly one household (#242).

Until now a position or commitment either named a household (`household_id`) or
named none, and "none" meant private. That meaning gets a column of its own,
`is_private`, because the household is about to become a property of the person
instead of the item. Marking it first keeps the two changes apart.

**Upgrade.** `is_private` is added to `plan_positions` and `commitments`, NOT NULL
with `false` as the default. Everything whose `household_id` is NULL becomes
private, so nothing that was private turns visible.

**Downgrade.** The column is dropped; `household_id` still carries the meaning.
"""
from typing import Sequence, Union

import sqlalchemy as sa
from alembic import op

# revision identifiers, used by Alembic.
revision: str = "a5c9d2e7f104"
down_revision: Union[str, Sequence[str], None] = "c5a8e2f7d941"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None

TABLES = ("plan_positions", "commitments")


def upgrade() -> None:
    """Upgrade schema."""
    for table in TABLES:
        op.add_column(
            table,
            sa.Column("is_private", sa.Boolean(), nullable=False, server_default=sa.false()),
        )
        op.execute(f"UPDATE {table} SET is_private = true WHERE household_id IS NULL")


def downgrade() -> None:
    """Downgrade schema."""
    for table in TABLES:
        op.drop_column(table, "is_private")
