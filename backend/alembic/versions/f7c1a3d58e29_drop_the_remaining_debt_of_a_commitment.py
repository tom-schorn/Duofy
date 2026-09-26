"""drop the remaining debt of a commitment

Revision ID: f7c1a3d58e29
Revises: e5b2c7a94d16
Create Date: 2026-09-26 17:00:00.000000

`commitments.remaining_debt` was maintained by nobody and computed with nothing:
no repayment reduced it and no report read it (#110). The column and its CHECK
`ck_commitment_remaining_debt_only_for_debt` go; with them the field, the
validator branch and the error code in the backend.

**Upgrade.** Whatever amounts were typed into `remaining_debt` are deleted with
the column. The commitments themselves, their amounts and their positions stay.

**Downgrade.** The column comes back empty (NULL) together with its CHECK. The
values **cannot be restored**; the issue accepts that.
"""
from typing import Sequence, Union

import sqlalchemy as sa
from alembic import op

# revision identifiers, used by Alembic.
revision: str = "f7c1a3d58e29"
down_revision: Union[str, Sequence[str], None] = "e5b2c7a94d16"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None

CHECK = "ck_commitment_remaining_debt_only_for_debt"


def upgrade() -> None:
    """Upgrade schema."""
    op.drop_constraint(CHECK, "commitments", type_="check")
    op.drop_column("commitments", "remaining_debt")


def downgrade() -> None:
    """Downgrade schema."""
    op.add_column(
        "commitments", sa.Column("remaining_debt", sa.Numeric(12, 2), nullable=True)
    )
    op.create_check_constraint(CHECK, "commitments", "type = 'debt' OR remaining_debt IS NULL")
