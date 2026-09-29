"""drop household from positions and commitments

Revision ID: c7e1f4a9b326
Revises: b6d0e3f8a215
Create Date: 2026-09-29 10:20:00.000000

Third of three steps that give every person exactly one household (#242).

A person has one household now, so the item does not need to name it: the
household view is the non-private items of every member.

**Upgrade.** `household_id` and its foreign key leave `plan_positions` and
`commitments`.

**Downgrade.** The column comes back, nullable, with a named foreign key
(`ON DELETE SET NULL`). It is filled with the one household of the owner wherever
the item is not private; private items stay NULL, as before.
"""
from typing import Sequence, Union

import sqlalchemy as sa
from alembic import op

# revision identifiers, used by Alembic.
revision: str = "c7e1f4a9b326"
down_revision: Union[str, Sequence[str], None] = "b6d0e3f8a215"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None

TABLES = ("plan_positions", "commitments")


def upgrade() -> None:
    """Upgrade schema."""
    # Dropping the column takes its foreign key along, whatever the key is called:
    # the one on `plan_positions` was created without a name.
    for table in TABLES:
        op.drop_column(table, "household_id")


def downgrade() -> None:
    """Downgrade schema."""
    for table in TABLES:
        op.add_column(table, sa.Column("household_id", sa.Uuid(), nullable=True))
        op.create_foreign_key(
            f"fk_{table}_household_id",
            table,
            "households",
            ["household_id"],
            ["id"],
            ondelete="SET NULL",
        )
    op.execute(
        """
        UPDATE plan_positions AS p SET household_id = m.household_id
        FROM plans, household_members m
        WHERE plans.id = p.plan_id AND m.user_id = plans.user_id AND NOT p.is_private
        """
    )
    op.execute(
        """
        UPDATE commitments AS c SET household_id = m.household_id
        FROM household_members m
        WHERE m.user_id = c.owner_id AND NOT c.is_private
        """
    )
