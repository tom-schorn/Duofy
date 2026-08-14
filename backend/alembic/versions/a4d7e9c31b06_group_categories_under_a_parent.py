"""group categories under a parent

Revision ID: a4d7e9c31b06
Revises: 67c888645f74
Create Date: 2026-08-14 09:12:44.108233

Categories used to be a flat list of twenty. They now carry their group in front
of a dot — `housing.rent`, `leisure.hobbies` — which makes the hierarchy readable
in SQL (`LIKE 'housing.%'`) without a second column.

Two things happen here, and the order matters:

1. the three category columns grow from `varchar(20)` to `varchar(40)`, because
   `household.personal_care` is 23 characters
2. the eight superseded values are rewritten in every existing row

Twelve values are untouched — `income`, `insurance`, `communication` and the rest
of the ungrouped ones fit under none of the new headings and keep their spelling.

**The downgrade loses detail.** The new taxonomy is finer than the old one, so
several values collapse back onto the same ancestor: `household.clothing` and
`household.cleaning` both become `groceries`, every `transport.*` becomes
`mobility`. Rolling forward again cannot tell them apart any more.

There is no CHECK constraint to adjust. `enum_column` builds these columns with
`native_enum=False` and does not pass `create_constraint=True`, so they are plain
`varchar` in the database — see the note in the previous migration.
"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa

# Autogenerate emits fastapi_users_db_sqlalchemy.generics.GUID for user IDs but
# forgets to import it, so the import is wired into this template.
import fastapi_users_db_sqlalchemy
from sqlalchemy.dialects import postgresql

# revision identifiers, used by Alembic.
revision: str = 'a4d7e9c31b06'
down_revision: Union[str, Sequence[str], None] = '67c888645f74'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


#: Every table that stores a category, with whether the column may be NULL.
TABLES = ('commitments', 'plan_positions', 'transactions')

#: Old value -> new value. Only the eight that the new grouping replaces; the
#: ungrouped twelve keep their spelling and are deliberately absent.
#:
#: `mobility` and `leisure` were broad enough to cover several of the new entries.
#: They land on the most common one — fuel and hobbies — which is a guess, not a
#: fact. Anything mapped wrongly is a single dropdown correction per row.
FORWARD = {
    'groceries': 'household.groceries',
    'health': 'household.healthcare',
    'housing': 'housing.rent',
    'mobility': 'transport.fuel',
    'children': 'children.care',
    'leisure': 'leisure.hobbies',
    'vacation': 'leisure.vacation',
    'pocket_money': 'children.allowance',
}

#: New value -> nearest old value. Not the inverse of FORWARD: the new list is
#: finer, so entries the old taxonomy never had fall back onto their closest
#: ancestor.
BACKWARD = {
    'household.groceries': 'groceries',
    'household.clothing': 'groceries',
    'household.healthcare': 'health',
    'household.personal_care': 'health',
    'household.cleaning': 'groceries',
    'housing.rent': 'housing',
    'housing.utilities': 'housing',
    'housing.repairs': 'housing',
    'housing.interior': 'housing',
    'housing.outdoor': 'housing',
    'housing.insurance': 'insurance',
    'transport.public': 'mobility',
    'transport.fuel': 'mobility',
    'transport.repairs': 'mobility',
    'transport.fines': 'mobility',
    'transport.purchase': 'mobility',
    'transport.insurance': 'insurance',
    'children.care': 'children',
    'children.school': 'children',
    'children.allowance': 'pocket_money',
    'leisure.vacation': 'vacation',
    'leisure.hobbies': 'leisure',
    'leisure.entertainment': 'leisure',
    'leisure.memberships': 'leisure',
    'leisure.dining': 'leisure',
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


def _resize(from_length: int, to_length: int) -> None:
    """Change the width of the category column in every table.

    `transactions.category` is the only one that may be NULL — a pure transfer has
    no purpose. `existing_nullable` has to say so, otherwise the column would come
    back NOT NULL and every transfer already booked would break it.
    """
    for table in TABLES:
        op.alter_column(
            table,
            'category',
            existing_type=sa.VARCHAR(length=from_length),
            type_=sa.VARCHAR(length=to_length),
            existing_nullable=table == 'transactions',
        )


def upgrade() -> None:
    """Upgrade schema."""
    # Widen first — the new values do not fit into varchar(20).
    _resize(20, 40)
    _rewrite(FORWARD)


def downgrade() -> None:
    """Downgrade schema."""
    # Shorten the values first, otherwise the column cannot shrink back.
    _rewrite(BACKWARD)
    _resize(40, 20)
