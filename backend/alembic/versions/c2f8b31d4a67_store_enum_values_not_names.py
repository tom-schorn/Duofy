"""Store enum values instead of names

SQLAlchemy legte bisher den **Namen** des Enums ab (`MONTHLY`), obwohl das
Enum `monthly` als Wert trägt. Damit liefen zwei CHECK-Constraints ins Leere:

    rhythm = 'monthly' OR first_due_date IS NOT NULL
    type = 'savings_goal' OR (target_amount IS NULL AND ...)

Die linke Seite war nie wahr, weil dort `MONTHLY` bzw. `CONTRACT` stand. Der
erste Constraint blockierte deshalb **jeden** Vertrag, der zweite hätte jedes
Sparziel mit Zielbetrag blockiert.

`app/db/types.py:enum_column` speichert jetzt den Wert. Diese Migration zieht
bestehende Zeilen nach — sie schreibt schlicht alles klein.

Revision ID: c2f8b31d4a67
Revises: b1c4e7a90d32
Create Date: 2026-07-31 04:35:00.000000

"""

from typing import Sequence, Union

from alembic import op

# revision identifiers, used by Alembic.
revision: str = "c2f8b31d4a67"
down_revision: Union[str, Sequence[str], None] = "b1c4e7a90d32"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None

#: Tabelle → Spalten, die ein Enum halten.
ENUM_COLUMNS = {
    "commitments": ["type", "category", "block", "rhythm"],
    "plans": ["status"],
    "plan_positions": ["category", "block", "payment_method"],
    "household_members": ["role"],
    "household_invitations": ["status"],
}


def upgrade() -> None:
    """Upgrade schema."""
    # Alle Enum-Werte sind reines ASCII in snake_case — lower() reicht.
    for table, columns in ENUM_COLUMNS.items():
        for column in columns:
            op.execute(f"UPDATE {table} SET {column} = lower({column}) WHERE {column} IS NOT NULL")


def downgrade() -> None:
    """Downgrade schema."""
    for table, columns in ENUM_COLUMNS.items():
        for column in columns:
            op.execute(f"UPDATE {table} SET {column} = upper({column}) WHERE {column} IS NOT NULL")
