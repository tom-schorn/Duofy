"""Add first_due_date, paid_at, household quotas and invitations

Vier Lücken, die beim Bauen der Oberfläche aufgefallen sind:

1. `commitments.first_month` kannte nur den Monat, nicht das Jahr — die
   Generierung hätte rückwirkend Posten erzeugt. Ersetzt durch
   `first_due_date`, aus dem sich Monat und Tag ableiten lassen.
2. `plan_positions` hatte kein Feld fürs Abhaken. `amount_actual IS NULL` war
   ein Behelf und falsch: ein Posten kann bezahlt sein und trotzdem exakt dem
   geplanten Betrag entsprechen.
3. `households` hatte keine eigenen Quoten — der Haushaltsplan konnte kein
   eigenes 50/30/20 haben.
4. Es gab keinen Weg, jemanden einzuladen, der noch kein Konto hat.

Revision ID: b1c4e7a90d32
Revises: 9f5a7df19ea2
Create Date: 2026-07-31 04:05:00.000000

"""

from typing import Sequence, Union

import sqlalchemy as sa
from alembic import op

# Autogenerate erzeugt fastapi_users_db_sqlalchemy.generics.GUID für User-IDs,
# vergisst aber den Import. Deshalb fest im Template.
import fastapi_users_db_sqlalchemy  # noqa: F401

# revision identifiers, used by Alembic.
revision: str = "b1c4e7a90d32"
down_revision: Union[str, Sequence[str], None] = "9f5a7df19ea2"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    """Upgrade schema."""

    # --- 1. Commitment: erste Fälligkeit statt bloßem Monat ----------------
    op.add_column("commitments", sa.Column("first_due_date", sa.Date(), nullable=True))

    # Bestehende Zeilen übernehmen: aus first_month und due_day ein Datum im
    # laufenden Jahr bauen. Der Tag wird auf den letzten Tag des Monats
    # abgeklemmt, damit der 31. im Februar nicht zum Fehler wird.
    # `date + integer` ergibt in Postgres wieder ein Datum. Der Tag wird auf
    # den letzten Tag des Monats abgeklemmt — sonst wäre der 31. im Februar
    # ein Fehler.
    op.execute(
        """
        UPDATE commitments
        SET first_due_date =
            make_date(EXTRACT(YEAR FROM CURRENT_DATE)::int, first_month, 1)
            + (
                LEAST(
                    due_day,
                    EXTRACT(DAY FROM (
                        make_date(EXTRACT(YEAR FROM CURRENT_DATE)::int, first_month, 1)
                        + INTERVAL '1 month'
                        - INTERVAL '1 day'
                    ))::int
                ) - 1
            )
        WHERE first_month IS NOT NULL
        """
    )

    op.drop_constraint("ck_commitment_first_month_required", "commitments", type_="check")
    op.drop_constraint("ck_commitment_first_month", "commitments", type_="check")
    op.drop_column("commitments", "first_month")

    op.create_check_constraint(
        "ck_commitment_first_due_date_required",
        "commitments",
        "rhythm = 'monthly' OR first_due_date IS NOT NULL",
    )

    # --- 2. Position: abgehakt ---------------------------------------------
    op.add_column(
        "plan_positions",
        sa.Column("paid_at", sa.DateTime(timezone=True), nullable=True),
    )

    # Was bereits einen Ist-Betrag trägt, gilt als erledigt — das war bisher
    # die einzige verfügbare Aussage.
    op.execute("UPDATE plan_positions SET paid_at = created_at WHERE amount_actual IS NOT NULL")

    # --- 3. Haushalt: eigene Quoten ----------------------------------------
    op.add_column(
        "households",
        sa.Column("target_needs", sa.Numeric(5, 2), nullable=False, server_default="50.00"),
    )
    op.add_column(
        "households",
        sa.Column("target_wants", sa.Numeric(5, 2), nullable=False, server_default="30.00"),
    )
    op.add_column(
        "households",
        sa.Column("target_savings", sa.Numeric(5, 2), nullable=False, server_default="20.00"),
    )
    op.add_column(
        "households",
        sa.Column("buffer_percent", sa.Numeric(5, 2), nullable=False, server_default="0.00"),
    )

    # --- 4. Einladungen -----------------------------------------------------
    op.create_table(
        "household_invitations",
        sa.Column("household_id", sa.Uuid(), nullable=False),
        sa.Column("invited_by_id", fastapi_users_db_sqlalchemy.generics.GUID(), nullable=False),
        sa.Column("email", sa.String(length=320), nullable=False),
        sa.Column("token", sa.String(length=64), nullable=False),
        sa.Column("status", sa.String(length=20), nullable=False),
        sa.Column("expires_at", sa.DateTime(timezone=True), nullable=False),
        sa.Column("id", sa.Uuid(), nullable=False),
        sa.Column(
            "created_at",
            sa.DateTime(timezone=True),
            server_default=sa.text("now()"),
            nullable=False,
        ),
        sa.ForeignKeyConstraint(["household_id"], ["households.id"], ondelete="CASCADE"),
        sa.ForeignKeyConstraint(["invited_by_id"], ["users.id"], ondelete="CASCADE"),
        sa.PrimaryKeyConstraint("id"),
        sa.UniqueConstraint(
            "household_id", "email", "status", name="uq_invitation_household_email_status"
        ),
    )
    op.create_index(
        "ix_household_invitations_email", "household_invitations", ["email"], unique=False
    )
    op.create_index(
        "ix_household_invitations_token", "household_invitations", ["token"], unique=True
    )


def downgrade() -> None:
    """Downgrade schema."""

    op.drop_index("ix_household_invitations_token", table_name="household_invitations")
    op.drop_index("ix_household_invitations_email", table_name="household_invitations")
    op.drop_table("household_invitations")

    op.drop_column("households", "buffer_percent")
    op.drop_column("households", "target_savings")
    op.drop_column("households", "target_wants")
    op.drop_column("households", "target_needs")

    op.drop_column("plan_positions", "paid_at")

    op.add_column("commitments", sa.Column("first_month", sa.Integer(), nullable=True))
    op.execute(
        "UPDATE commitments SET first_month = EXTRACT(MONTH FROM first_due_date)::int "
        "WHERE first_due_date IS NOT NULL"
    )
    op.drop_constraint("ck_commitment_first_due_date_required", "commitments", type_="check")
    op.drop_column("commitments", "first_due_date")

    op.create_check_constraint(
        "ck_commitment_first_month",
        "commitments",
        "first_month IS NULL OR first_month BETWEEN 1 AND 12",
    )
    op.create_check_constraint(
        "ck_commitment_first_month_required",
        "commitments",
        "rhythm = 'monthly' OR first_month IS NOT NULL",
    )
