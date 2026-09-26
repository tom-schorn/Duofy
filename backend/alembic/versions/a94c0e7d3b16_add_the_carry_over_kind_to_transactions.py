"""add the carry-over kind to transactions

Revision ID: a94c0e7d3b16
Revises: f7c1a3d58e29
Create Date: 2026-09-26 22:40:00.000000

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa

# Autogenerate emits fastapi_users_db_sqlalchemy.generics.GUID for user IDs but
# forgets to import it, so the import is wired into this template.
import fastapi_users_db_sqlalchemy


# revision identifiers, used by Alembic.
revision: str = 'a94c0e7d3b16'
down_revision: Union[str, Sequence[str], None] = 'f7c1a3d58e29'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    """Upgrade schema.

    `kind` is NOT NULL on a filled table, so it gets a server default: every row
    that exists is a booking. The purpose check learns the exception for a
    carry-over, and the new checks and the unique index only ever concern rows
    of that kind.
    """
    op.add_column(
        'transactions',
        sa.Column('kind', sa.String(length=20), server_default='booking', nullable=False),
    )
    op.drop_constraint('ck_transaction_purpose_unless_transfer', 'transactions', type_='check')
    op.create_check_constraint(
        'ck_transaction_purpose_unless_transfer',
        'transactions',
        "kind = 'carry_over' OR counter_account_id IS NOT NULL"
        " OR (category IS NOT NULL AND budget IS NOT NULL)",
    )
    op.create_check_constraint(
        'ck_transaction_amount_not_negative_unless_carry_over',
        'transactions',
        "kind = 'carry_over' OR amount >= 0",
    )
    op.create_check_constraint(
        'ck_transaction_carry_over_is_bare',
        'transactions',
        "kind <> 'carry_over' OR (category IS NULL AND budget IS NULL"
        " AND position_id IS NULL AND counter_account_id IS NULL)",
    )
    op.create_check_constraint(
        'ck_transaction_carry_over_on_first_of_month',
        'transactions',
        "kind <> 'carry_over' OR EXTRACT(DAY FROM occurred_on) = 1",
    )
    op.create_index(
        'uq_transaction_one_carry_over_per_account_and_month',
        'transactions',
        ['account_id', 'occurred_on'],
        unique=True,
        postgresql_where=sa.text("kind = 'carry_over'"),
    )


def downgrade() -> None:
    """Downgrade schema.

    Carry-overs are deleted first: without `kind` they would turn into bookings
    with no purpose and a sign, and the restored constraints would reject them.
    They only state a balance, so no booking and no balance is lost.
    """
    op.execute("DELETE FROM transactions WHERE kind = 'carry_over'")
    op.drop_index(
        'uq_transaction_one_carry_over_per_account_and_month',
        table_name='transactions',
        postgresql_where=sa.text("kind = 'carry_over'"),
    )
    op.drop_constraint('ck_transaction_carry_over_on_first_of_month', 'transactions', type_='check')
    op.drop_constraint('ck_transaction_carry_over_is_bare', 'transactions', type_='check')
    op.drop_constraint(
        'ck_transaction_amount_not_negative_unless_carry_over', 'transactions', type_='check'
    )
    op.drop_constraint('ck_transaction_purpose_unless_transfer', 'transactions', type_='check')
    op.create_check_constraint(
        'ck_transaction_purpose_unless_transfer',
        'transactions',
        "counter_account_id IS NOT NULL OR (category IS NOT NULL AND budget IS NOT NULL)",
    )
    op.drop_column('transactions', 'kind')
