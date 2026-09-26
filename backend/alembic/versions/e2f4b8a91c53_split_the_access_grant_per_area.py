"""split the access grant per area

Revision ID: e2f4b8a91c53
Revises: d5a1c73f2e08
Create Date: 2026-08-17 11:24:08.551903

One column decided what another member could see about you: plan, accounts and
book at once. Contracts were not in it at all — they were owner-only and could not
be shared even on purpose. Both are now settled by three columns of the same
`AccessLevel` ladder.

The values carry over so that nothing changes for anybody on the day of the
migration:

    grants_plan         <- grants_access   behaves exactly as before
    grants_accounts     <- grants_access   accounts already hung on that level
    grants_commitments  <- 'plan'          nobody has ever shared a contract

The last line is the point of the whole split: a contract becomes visible only
after its owner says so, never because a migration ran. The book has no column of
its own — it follows `grants_accounts`, since an account you may look at comes
with its bookings.

`server_default` is not decoration. The table has rows, and a `NOT NULL` column
without one fails the moment there is a single member — the same trap that broke
an earlier downgrade.
"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa

# Autogenerate emits fastapi_users_db_sqlalchemy.generics.GUID for user IDs but
# forgets to import it, so the import is wired into this template.
import fastapi_users_db_sqlalchemy
from sqlalchemy.dialects import postgresql

# revision identifiers, used by Alembic.
revision: str = 'e2f4b8a91c53'
down_revision: Union[str, Sequence[str], None] = 'd5a1c73f2e08'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


#: The three new columns, in the order they are added.
AREAS = ('grants_plan', 'grants_commitments', 'grants_accounts')


def _level_column(name: str) -> sa.Column:
    return sa.Column(
        name,
        sa.Enum('plan', 'view', 'edit', name='accesslevel', native_enum=False, length=20),
        nullable=False,
        server_default='plan',
    )


def upgrade() -> None:
    """Upgrade schema."""
    for name in AREAS:
        op.add_column('household_members', _level_column(name))

    # Plan and accounts inherit what the single column said. Commitments stay at
    # `plan` — the server_default already put them there.
    op.execute(
        sa.text(
            'UPDATE household_members '
            'SET grants_plan = grants_access, grants_accounts = grants_access'
        )
    )

    op.drop_column('household_members', 'grants_access')


def downgrade() -> None:
    """Downgrade schema."""
    op.add_column('household_members', _level_column('grants_access'))

    # The plan level is the one the old column used to hold. Whatever was granted
    # on contracts is lost here — there is no column left to keep it in.
    op.execute(sa.text('UPDATE household_members SET grants_access = grants_plan'))

    for name in reversed(AREAS):
        op.drop_column('household_members', name)
