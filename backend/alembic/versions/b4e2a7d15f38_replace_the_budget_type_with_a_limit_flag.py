"""replace the budget type with a limit flag

Revision ID: b4e2a7d15f38
Revises: a1d9f4c6b207
Create Date: 2026-09-25 22:40:00.000000

`CommitmentType.BUDGET` never had behaviour of its own — it only set a flag on
the position it generated. What the flag says is what matters: whether the
planned amount is a **single payment** that gets ticked off, or a **limit** that
fills up over the month. That is orthogonal to the kind of commitment, so it
becomes a field instead of a type. Point 2 of #106, and it frees the word
"budget" for the 50/30/20 dimension renamed in `a1d9f4c6b207`.

The flag moves onto `commitments` because groceries are planned every month, so
the property belongs to the commitment and is copied onto each position, the way
`category` and `payment_method` already are. On `plan_positions` it stays a
snapshot and is only renamed.

Existing rows: every commitment of type `budget` becomes a `contract` with
`is_limit = true` (decided 2026-09-25 — "one direct debit, one contract", and
nothing changes for the user).

`enum_column()` creates no CHECK constraint, so removing a member from the
Python enum needs no constraint change here.

**The downgrade is not exact**, and cannot be. Going back maps every
`contract` with `is_limit = true` to type `budget` — a contract that was a
contract before this migration and had the flag set by hand afterwards comes
back as a budget. Nothing is lost: `plan_positions.is_limit` survives under its
old name `is_budget`, which is where the behaviour is actually read.
"""
from typing import Sequence, Union

import sqlalchemy as sa
from alembic import op

# revision identifiers, used by Alembic.
revision: str = "b4e2a7d15f38"
down_revision: Union[str, Sequence[str], None] = "a1d9f4c6b207"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    """Upgrade schema."""
    # `server_default` because the table holds rows: without it the NOT NULL
    # column could not be added. Dropped again right after, so new rows get the
    # value from the model like every other flag.
    op.add_column(
        "commitments",
        sa.Column("is_limit", sa.Boolean(), nullable=False, server_default=sa.false()),
    )
    op.alter_column("commitments", "is_limit", server_default=None)

    op.execute(
        sa.text(
            "UPDATE commitments SET is_limit = true, type = 'contract' "
            "WHERE type = 'budget'"
        )
    )

    op.alter_column("plan_positions", "is_budget", new_column_name="is_limit")


def downgrade() -> None:
    """Downgrade schema."""
    op.alter_column("plan_positions", "is_limit", new_column_name="is_budget")

    # See the module docstring: this direction guesses, and says so.
    op.execute(
        sa.text(
            "UPDATE commitments SET type = 'budget' "
            "WHERE type = 'contract' AND is_limit = true"
        )
    )

    op.drop_column("commitments", "is_limit")
