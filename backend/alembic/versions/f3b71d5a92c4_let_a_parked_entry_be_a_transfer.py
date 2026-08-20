"""let a parked entry be a transfer

Revision ID: f3b71d5a92c4
Revises: 006c7dae1f2e
Create Date: 2026-08-20 11:02:41.118395

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa

# Autogenerate emits fastapi_users_db_sqlalchemy.generics.GUID for user IDs but
# forgets to import it, so the import is wired into this template.
import fastapi_users_db_sqlalchemy


# revision identifiers, used by Alembic.
revision: str = 'f3b71d5a92c4'
down_revision: Union[str, Sequence[str], None] = '006c7dae1f2e'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    """Upgrade schema.

    One nullable column and its foreign key. Nullable is not a compromise here —
    most parked entries are not transfers, and the column being empty is what
    says so.
    """
    op.add_column(
        'imported_entries',
        sa.Column('counter_account_id', sa.Uuid(), nullable=True),
    )
    op.create_foreign_key(
        'fk_imported_entries_counter_account_id_accounts',
        'imported_entries',
        'accounts',
        ['counter_account_id'],
        ['id'],
        ondelete='RESTRICT',
    )


def downgrade() -> None:
    """Downgrade schema.

    Dropping the column loses the interpretation "this was a transfer", not the
    entry. The bank half of the row is untouched, so the recognition works it
    out again on the next read.
    """
    op.drop_constraint(
        'fk_imported_entries_counter_account_id_accounts',
        'imported_entries',
        type_='foreignkey',
    )
    op.drop_column('imported_entries', 'counter_account_id')
