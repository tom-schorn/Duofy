"""keep the change log a deleted user left on other people's positions

Revision ID: a2c6e94b1d70
Revises: f7c1a3d58e29
Create Date: 2026-09-27 01:00:00.000000

`plan_position_changes.changed_by_id` was NOT NULL with ON DELETE CASCADE, so
deleting a user also deleted the entries they had left on **other** members'
positions — history that belongs to those members (#66).

**Upgrade.** The column becomes nullable and the foreign key ON DELETE SET NULL.
Existing rows are untouched. Entries on the deleted user's own positions still go
with those positions (the cascade from `plan_positions`).

**Downgrade.** Entries with `changed_by_id IS NULL` cannot satisfy NOT NULL again
and have no author to point at, so they are **deleted**; then the column is NOT NULL
with CASCADE again.
"""
from typing import Sequence, Union

import sqlalchemy as sa
from alembic import op

# revision identifiers, used by Alembic.
revision: str = "a2c6e94b1d70"
down_revision: Union[str, Sequence[str], None] = "f7c1a3d58e29"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None

TABLE = "plan_position_changes"
#: Postgres' default name for the unnamed foreign key created with the table.
FK = "plan_position_changes_changed_by_id_fkey"


def upgrade() -> None:
    """Upgrade schema."""
    op.drop_constraint(FK, TABLE, type_="foreignkey")
    op.alter_column(TABLE, "changed_by_id", nullable=True)
    op.create_foreign_key(FK, TABLE, "users", ["changed_by_id"], ["id"], ondelete="SET NULL")


def downgrade() -> None:
    """Downgrade schema."""
    op.drop_constraint(FK, TABLE, type_="foreignkey")
    op.execute(sa.text(f"DELETE FROM {TABLE} WHERE changed_by_id IS NULL"))
    op.alter_column(TABLE, "changed_by_id", nullable=False)
    op.create_foreign_key(FK, TABLE, "users", ["changed_by_id"], ["id"], ondelete="CASCADE")
