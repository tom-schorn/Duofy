"""remove plan status and confirmation

Revision ID: 67c888645f74
Revises: bef5a98e9b2f
Create Date: 2026-08-12 05:38:23.194142

A plan had a state, `draft` or `confirmed`, and a `confirmed_at` to go with it.
Both are gone: they claimed there is a moment when a month is finished, and there
is none. See issue #8.

**The upgrade loses data.** Which months somebody had confirmed cannot be restored
afterwards — the downgrade brings the columns back, every plan then reads `draft`.
Nothing depended on the state, so nothing breaks; it is simply not recoverable.

There is no CHECK constraint to drop. `enum_column` builds these columns with
`native_enum=False` and SQLAlchemy does not add a constraint unless asked, so
`status` was a plain `varchar(20)` — verified against the schema the migrations
actually produce.
"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa

# Autogenerate emits fastapi_users_db_sqlalchemy.generics.GUID for user IDs but
# forgets to import it, so the import is wired into this template.
import fastapi_users_db_sqlalchemy
from sqlalchemy.dialects import postgresql

# revision identifiers, used by Alembic.
revision: str = '67c888645f74'
down_revision: Union[str, Sequence[str], None] = 'bef5a98e9b2f'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    """Upgrade schema."""
    op.drop_column('plans', 'confirmed_at')
    op.drop_column('plans', 'status')


def downgrade() -> None:
    """Downgrade schema."""
    # `status` is NOT NULL, so existing rows need a value. Autogenerate omits the
    # server_default and the downgrade then fails on any table that holds a plan.
    # The default is dropped again right after, because the model never had one —
    # the application always wrote the value itself.
    op.add_column(
        'plans',
        sa.Column(
            'status',
            sa.VARCHAR(length=20),
            autoincrement=False,
            nullable=False,
            server_default='draft',
        ),
    )
    op.alter_column('plans', 'status', server_default=None)
    op.add_column(
        'plans',
        sa.Column(
            'confirmed_at',
            postgresql.TIMESTAMP(timezone=True),
            autoincrement=False,
            nullable=True,
        ),
    )
