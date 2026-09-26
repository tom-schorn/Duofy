"""make income its own commitment type

Revision ID: d5a1c73f2e08
Revises: c8b26f4e1a37
Create Date: 2026-08-17 10:12:44.318920

A salary used to be a `contract` whose block the user set to `income` by hand.
The arithmetic worked — `budget = income - buffer` reads the block, not the type —
but nothing stopped anyone from picking `needs` on their own wage and quietly
breaking every quota. `CommitmentType.INCOME` takes that choice away:
`resolve_block()` now settles it, the way it already settles saving and repaying.

**No schema change.** `enum_column()` stores its values as `varchar` without a
check constraint, so a new member costs nothing in the database. The three
constraints on `commitments` name `savings_goal` and `debt` only — a fourth type
walks past them untouched.

Only `commitments` carries a type. `plan_positions` and `transactions` store the
block, and their block is already `income` — nothing to rewrite there.

Both directions are one to one:

    contract + block income  ->  income     (upgrade)
    income                   ->  contract   (downgrade)

Rows of some **other** type that sit on block `income` are deliberately left
alone. Folding them in would make the downgrade guess which of them used to be a
budget and which a contract, and a migration that guesses is worse than one that
leaves a row slightly misnamed.
"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa

# Autogenerate emits fastapi_users_db_sqlalchemy.generics.GUID for user IDs but
# forgets to import it, so the import is wired into this template.
import fastapi_users_db_sqlalchemy
from sqlalchemy.dialects import postgresql

# revision identifiers, used by Alembic.
revision: str = 'd5a1c73f2e08'
down_revision: Union[str, Sequence[str], None] = 'c8b26f4e1a37'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    """Upgrade schema."""
    op.execute(
        sa.text(
            "UPDATE commitments SET type = 'income' "
            "WHERE type = 'contract' AND block = 'income'"
        )
    )


def downgrade() -> None:
    """Downgrade schema."""
    op.execute(sa.text("UPDATE commitments SET type = 'contract' WHERE type = 'income'"))
