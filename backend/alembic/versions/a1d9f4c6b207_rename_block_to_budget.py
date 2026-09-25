"""rename block to budget

Revision ID: a1d9f4c6b207
Revises: f3b71d5a92c4
Create Date: 2026-09-25 22:10:00.000000

The code said `block`, the interface has always said "Budget". One word for one
thing: the column is renamed on all four tables that carry it, and the Python
enum `Block` becomes `Budget`. The values (`income`, `needs`, `wants`,
`savings`) are untouched, so **no data moves** — this is a pure rename.

`enum_column()` builds `sa.Enum(..., native_enum=False)` without
`create_constraint`, which defaults to `False` in SQLAlchemy 2. The columns are
therefore plain `varchar(20)` with no CHECK constraint of their own, and a
rename needs nothing more than `ALTER TABLE ... RENAME COLUMN`.

One constraint does name the column in its expression:
`ck_transaction_purpose_unless_transfer` on `transactions`. Postgres rewrites
such an expression by itself on a rename, but the constraint is also declared in
`app/models/transaction.py` — so it is dropped and recreated here with the new
text, and database and model stay in step.
"""
from typing import Sequence, Union

from alembic import op

# revision identifiers, used by Alembic.
revision: str = "a1d9f4c6b207"
down_revision: Union[str, Sequence[str], None] = "f3b71d5a92c4"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None

#: Every table that carries the 50/30/20 dimension.
TABLES = ("commitments", "plan_positions", "transactions", "imported_entries")

PURPOSE_CONSTRAINT = "ck_transaction_purpose_unless_transfer"


def upgrade() -> None:
    """Upgrade schema."""
    op.drop_constraint(PURPOSE_CONSTRAINT, "transactions", type_="check")

    for table in TABLES:
        op.alter_column(table, "block", new_column_name="budget")

    op.create_check_constraint(
        PURPOSE_CONSTRAINT,
        "transactions",
        "counter_account_id IS NOT NULL OR (category IS NOT NULL AND budget IS NOT NULL)",
    )


def downgrade() -> None:
    """Downgrade schema."""
    op.drop_constraint(PURPOSE_CONSTRAINT, "transactions", type_="check")

    for table in TABLES:
        op.alter_column(table, "budget", new_column_name="block")

    op.create_check_constraint(
        PURPOSE_CONSTRAINT,
        "transactions",
        "counter_account_id IS NOT NULL OR (category IS NOT NULL AND block IS NOT NULL)",
    )
