"""Add block and household to commitments and legal category

Revision ID: 9f5a7df19ea2
Revises: da1f8e62e34f
Create Date: 2026-07-28 13:25:46.686038

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa

# Autogenerate erzeugt fastapi_users_db_sqlalchemy.generics.GUID für User-IDs,
# vergisst aber den Import. Deshalb fest im Template.
import fastapi_users_db_sqlalchemy


# revision identifiers, used by Alembic.
revision: str = '9f5a7df19ea2'
down_revision: Union[str, Sequence[str], None] = 'da1f8e62e34f'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    """Upgrade schema."""
    block_enum = sa.Enum(
        "INCOME", "NEEDS", "WANTS", "INVESTMENT", "SAVINGS",
        name="block", native_enum=False, length=20,
    )

    # Mit Default anlegen, damit vorhandene Zeilen nicht scheitern —
    # danach entfernen, neue Zeilen müssen den Block mitliefern.
    op.add_column("commitments", sa.Column("block", block_enum, nullable=False, server_default="needs"))
    op.alter_column("commitments", "block", server_default=None)

    op.add_column("commitments", sa.Column("household_id", sa.Uuid(), nullable=True))
    op.create_foreign_key(
        "fk_commitments_household_id",
        "commitments", "households", ["household_id"], ["id"], ondelete="SET NULL",
    )


def downgrade() -> None:
    """Downgrade schema."""
    op.drop_constraint("fk_commitments_household_id", "commitments", type_="foreignkey")
    op.drop_column("commitments", "household_id")
    op.drop_column("commitments", "block")
