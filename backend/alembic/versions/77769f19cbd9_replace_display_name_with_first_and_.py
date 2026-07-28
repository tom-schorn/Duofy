"""Replace display name with first and last name

Revision ID: 77769f19cbd9
Revises: da3bea01ebf0
Create Date: 2026-07-28 09:20:05.691614

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa

# Autogenerate erzeugt fastapi_users_db_sqlalchemy.generics.GUID für User-IDs,
# vergisst aber den Import. Deshalb fest im Template.
import fastapi_users_db_sqlalchemy


# revision identifiers, used by Alembic.
revision: str = '77769f19cbd9'
down_revision: Union[str, Sequence[str], None] = 'da3bea01ebf0'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    """Upgrade schema."""
    # Spalten mit Default anlegen, damit vorhandene Zeilen nicht scheitern.
    op.add_column("users", sa.Column("first_name", sa.String(100), nullable=False, server_default=""))
    op.add_column("users", sa.Column("last_name", sa.String(100), nullable=False, server_default=""))

    # Bestandsdaten übernehmen: der bisherige Anzeigename wird zum Vornamen.
    op.execute("UPDATE users SET first_name = display_name")

    # Default wieder entfernen — neue Zeilen müssen die Namen mitliefern.
    op.alter_column("users", "first_name", server_default=None)
    op.alter_column("users", "last_name", server_default=None)

    op.drop_column("users", "display_name")


def downgrade() -> None:
    """Downgrade schema."""
    op.add_column("users", sa.Column("display_name", sa.VARCHAR(100), nullable=False, server_default=""))
    op.execute("UPDATE users SET display_name = first_name")
    op.alter_column("users", "display_name", server_default=None)

    op.drop_column("users", "last_name")
    op.drop_column("users", "first_name")
