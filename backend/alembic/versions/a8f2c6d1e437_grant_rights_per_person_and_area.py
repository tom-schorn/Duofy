"""grant rights per person and area

Revision ID: a8f2c6d1e437
Revises: c7e1f4a9b326
Create Date: 2026-09-29 18:00:00.000000

Until now each person had one level per area on their membership, and it counted
for every other member at once. From here on each person grants each other member
separately, in five areas instead of three, on a ladder of five steps (#254,
decisions 57 and 63).

**Upgrade.** A new table `grants`: "A grants B in area X level Y". Every member's
old setting is copied to every other member of the same household, so nobody can
see or do more or less on the day of the migration:

    grants_plan         -> plan
    grants_commitments  -> commitments
    grants_accounts     -> accounts, book, import

The steps carry over one to one, except `plan`: it only ever meant "the shared
positions", and the household view shows those to everybody anyway (decision 48).
It becomes `none`, which is stored as no row at all. Nobody gets the new step
`create` from here.

Then the three columns leave `household_members`.

**Downgrade.** The columns come back with `plan` as default. Each person's column
gets the *lowest* level they granted to any other member in that area, so the
old model never hands out more than was granted. `create` becomes `view` (old
`edit` would be more than was given), a missing row `plan`. The book and import
rows are dropped; the accounts rows fill `grants_accounts`. Differences between
members are lost — the old model has one level for everybody.
"""
from typing import Sequence, Union

import sqlalchemy as sa
from alembic import op

# revision identifiers, used by Alembic.
revision: str = "a8f2c6d1e437"
down_revision: Union[str, Sequence[str], None] = "c7e1f4a9b326"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None

#: New area <- old column.
SOURCE = {
    "plan": "grants_plan",
    "book": "grants_accounts",
    "accounts": "grants_accounts",
    "commitments": "grants_commitments",
    "import": "grants_accounts",
}

#: Old column <- new area, for the way back.
BACK = {
    "grants_plan": "plan",
    "grants_commitments": "commitments",
    "grants_accounts": "accounts",
}


def _level_column(name: str) -> sa.Column:
    return sa.Column(
        name,
        sa.Enum("plan", "view", "edit", "delete", name="accesslevel", native_enum=False, length=20),
        nullable=False,
        server_default="plan",
    )


def upgrade() -> None:
    """Upgrade schema."""
    op.create_table(
        "grants",
        sa.Column("granter_id", sa.Uuid(), nullable=False),
        sa.Column("grantee_id", sa.Uuid(), nullable=False),
        sa.Column("area", sa.String(length=20), nullable=False),
        sa.Column("level", sa.String(length=20), nullable=False),
        sa.Column(
            "created_at",
            sa.DateTime(timezone=True),
            server_default=sa.text("now()"),
            nullable=False,
        ),
        sa.CheckConstraint("granter_id <> grantee_id", name="ck_grant_not_self"),
        sa.CheckConstraint(
            "area IN ('plan', 'book', 'accounts', 'commitments', 'import')",
            name="ck_grant_area",
        ),
        sa.CheckConstraint(
            "level IN ('view', 'create', 'edit', 'delete')", name="ck_grant_level"
        ),
        sa.ForeignKeyConstraint(
            ["granter_id"], ["users.id"], name="fk_grants_granter_id_users", ondelete="CASCADE"
        ),
        sa.ForeignKeyConstraint(
            ["grantee_id"], ["users.id"], name="fk_grants_grantee_id_users", ondelete="CASCADE"
        ),
        sa.PrimaryKeyConstraint("granter_id", "grantee_id", "area"),
    )
    op.create_index("ix_grants_grantee_id", "grants", ["grantee_id"])

    # Every member to every other member of the same household, per area. `plan`
    # means nothing beyond the household view, so it becomes no row.
    for area, column in SOURCE.items():
        op.execute(
            sa.text(
                "INSERT INTO grants (granter_id, grantee_id, area, level) "
                f"SELECT granter.user_id, grantee.user_id, :area, granter.{column} "
                "FROM household_members granter "
                "JOIN household_members grantee "
                "  ON grantee.household_id = granter.household_id "
                " AND grantee.user_id <> granter.user_id "
                f"WHERE granter.{column} <> 'plan'"
            ).bindparams(area=area)
        )

    for column in BACK:
        op.drop_column("household_members", column)


def downgrade() -> None:
    """Downgrade schema."""
    for column in BACK:
        op.add_column("household_members", _level_column(column))

    # The lowest level given to any other member; `create` counts as `view`, a
    # missing row as `plan`. MIN over nobody is NULL, which the CASE turns into
    # `plan` as well — a person alone in their household shared nothing.
    for column, area in BACK.items():
        op.execute(
            sa.text(
                f"UPDATE household_members member SET {column} = ("
                "  SELECT CASE MIN("
                "    CASE grant_row.level"
                "      WHEN 'view' THEN 1 WHEN 'create' THEN 1"
                "      WHEN 'edit' THEN 2 WHEN 'delete' THEN 3 ELSE 0 END)"
                "    WHEN 1 THEN 'view' WHEN 2 THEN 'edit' WHEN 3 THEN 'delete'"
                "    ELSE 'plan' END"
                "  FROM household_members other"
                "  LEFT JOIN grants grant_row"
                "    ON grant_row.granter_id = member.user_id"
                "   AND grant_row.grantee_id = other.user_id"
                "   AND grant_row.area = :area"
                "  WHERE other.household_id = member.household_id"
                "    AND other.user_id <> member.user_id"
                ")"
            ).bindparams(area=area)
        )

    op.drop_index("ix_grants_grantee_id", table_name="grants")
    op.drop_table("grants")
