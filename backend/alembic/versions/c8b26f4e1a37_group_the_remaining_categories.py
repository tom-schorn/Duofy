"""group the remaining categories

Revision ID: c8b26f4e1a37
Revises: a4d7e9c31b06
Create Date: 2026-08-14 09:41:02.775614

The previous migration grouped what the five expense headings covered and left
twelve values standing on their own. After five groups with a title, a tail
without one reads like a leftover — and three of the twelve were not leftovers at
all but headings in their own right:

    Personal   what hangs on the person, not on a flat or a car: health and
               liability insurance, phone, work expenses, a lawyer
    Income     the app never had an income category worth the name, although
               `budget = income - buffer` is the basis of every quota
    Finance    not consumption. Saving, repaying, settling up — the money is not
               gone, it sits somewhere else or a debt got smaller

`subscriptions` moves into the existing Leisure group, next to `memberships`. The
two were telling the same story from different ends.

All twelve rewrites are one to one, so **nothing is lost in either direction**.
The two genuinely new entries — `income.benefits` and `income.other` — have no
ancestor in the old list; on a downgrade they fall back to plain `income`.

No column changes here: the previous migration already widened all three to
`varchar(40)`, and the longest value is still `household.personal_care` at 23.
"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa

# Autogenerate emits fastapi_users_db_sqlalchemy.generics.GUID for user IDs but
# forgets to import it, so the import is wired into this template.
import fastapi_users_db_sqlalchemy
from sqlalchemy.dialects import postgresql

# revision identifiers, used by Alembic.
revision: str = 'c8b26f4e1a37'
down_revision: Union[str, Sequence[str], None] = 'a4d7e9c31b06'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


#: Every table that stores a category.
TABLES = ('commitments', 'plan_positions', 'transactions')

#: Old value -> new value. One to one, every single one of them.
FORWARD = {
    'income': 'income.earned',
    'interest': 'income.interest',
    'communication': 'personal.communication',
    'insurance': 'personal.insurance',
    'legal': 'personal.legal',
    'work': 'personal.work',
    'subscriptions': 'leisure.subscriptions',
    'reserves': 'finance.savings',
    'debt_repayment': 'finance.debt',
    'investment': 'finance.investment',
    'fees': 'finance.fees',
    'settlement': 'finance.settlement',
}

#: New value -> old value. The inverse of FORWARD, plus the two entries the old
#: list never had: a benefit and a one-off both become plain `income` again.
BACKWARD = {new: old for old, new in FORWARD.items()} | {
    'income.benefits': 'income',
    'income.other': 'income',
}


def _rewrite(mapping: dict[str, str]) -> None:
    """Apply a value mapping to the category column of every table."""
    for table in TABLES:
        for old, new in mapping.items():
            op.execute(
                sa.text(f'UPDATE {table} SET category = :new WHERE category = :old').bindparams(
                    new=new, old=old
                )
            )


def upgrade() -> None:
    """Upgrade schema."""
    _rewrite(FORWARD)


def downgrade() -> None:
    """Downgrade schema."""
    _rewrite(BACKWARD)
