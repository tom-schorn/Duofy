"""let a parked entry survive the importer leaving

Revision ID: 48618ba806e8
Revises: b4d7e19c3a58
Create Date: 2026-09-28 02:00:00.000000

`imported_entries.imported_by_id` was `ON DELETE CASCADE`. Somebody with
`Area.ACCOUNTS` at `edit` may import for another household member (see
`ImportedEntry`'s docstring); deleting *their own* account then cascaded from
`imported_by_id` and wiped the *other* member's parked rows, even though those
rows belong to `owner_id`, not to the importer.

**Upgrade.** The column becomes nullable and its foreign key `SET NULL`: the
row stays, only the memory of who uploaded it is lost.

**Downgrade.** A `NULL` cannot be turned back into whoever the importer was —
that fact is gone. The rows are **reassigned to their `owner_id`** instead of
being deleted, because the entry itself is worth more than the (already
approximate) uploader attribution, and because deleting would again raise the
CASCADE risk this migration exists to remove.
"""
from typing import Sequence, Union

from alembic import op


# revision identifiers, used by Alembic.
revision: str = '48618ba806e8'
down_revision: Union[str, Sequence[str], None] = 'b4d7e19c3a58'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None

OLD_FK = 'imported_entries_imported_by_id_fkey'
NEW_FK = 'fk_imported_entries_imported_by_id_users'


def upgrade() -> None:
    """Upgrade schema."""
    op.drop_constraint(OLD_FK, 'imported_entries', type_='foreignkey')
    op.alter_column('imported_entries', 'imported_by_id', nullable=True)
    op.create_foreign_key(
        NEW_FK,
        'imported_entries',
        'users',
        ['imported_by_id'],
        ['id'],
        ondelete='SET NULL',
    )


def downgrade() -> None:
    """Downgrade schema."""
    op.execute(
        "UPDATE imported_entries SET imported_by_id = owner_id "
        "WHERE imported_by_id IS NULL"
    )
    op.drop_constraint(NEW_FK, 'imported_entries', type_='foreignkey')
    op.alter_column('imported_entries', 'imported_by_id', nullable=False)
    op.create_foreign_key(
        OLD_FK,
        'imported_entries',
        'users',
        ['imported_by_id'],
        ['id'],
        ondelete='CASCADE',
    )
