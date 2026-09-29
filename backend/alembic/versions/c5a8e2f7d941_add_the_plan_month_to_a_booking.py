"""add the plan month to a booking

Revision ID: c5a8e2f7d941
Revises: 48618ba806e8
Create Date: 2026-09-29 11:20:00.000000

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa

# revision identifiers, used by Alembic.
revision: str = 'c5a8e2f7d941'
down_revision: Union[str, Sequence[str], None] = '48618ba806e8'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    """Upgrade schema.

    `plan_year` and `plan_month` are NOT NULL, so on a filled table they go in with
    a placeholder default, are filled with the real value and lose the default
    again. A default that stays would let a forgotten value pass as "2000-01".

    The real value, in the order the rule has always applied: the plan of the
    position when there is one — so a booking already counts where the book
    listed it before — otherwise the month of the booking date.
    """
    op.add_column(
        'transactions',
        sa.Column('plan_year', sa.Integer(), server_default='2000', nullable=False),
    )
    op.add_column(
        'transactions',
        sa.Column('plan_month', sa.Integer(), server_default='1', nullable=False),
    )
    op.execute(
        """
        UPDATE transactions AS t
        SET plan_year = p.year, plan_month = p.month
        FROM plan_positions AS pp
        JOIN plans AS p ON p.id = pp.plan_id
        WHERE pp.id = t.position_id
        """
    )
    op.execute(
        """
        UPDATE transactions
        SET plan_year = EXTRACT(YEAR FROM occurred_on)::int,
            plan_month = EXTRACT(MONTH FROM occurred_on)::int
        WHERE position_id IS NULL
        """
    )
    op.alter_column('transactions', 'plan_year', server_default=None)
    op.alter_column('transactions', 'plan_month', server_default=None)
    op.create_check_constraint(
        'ck_transaction_plan_month', 'transactions', 'plan_month BETWEEN 1 AND 12'
    )
    op.create_index(
        'ix_transactions_plan_year_plan_month',
        'transactions',
        ['plan_year', 'plan_month'],
    )


def downgrade() -> None:
    """Downgrade schema.

    Only the choice is lost: which month a booking counted in. The booking, its
    date and its position stay, and the book falls back to listing by those.
    """
    op.drop_index('ix_transactions_plan_year_plan_month', table_name='transactions')
    op.drop_constraint('ck_transaction_plan_month', 'transactions', type_='check')
    op.drop_column('transactions', 'plan_month')
    op.drop_column('transactions', 'plan_year')
