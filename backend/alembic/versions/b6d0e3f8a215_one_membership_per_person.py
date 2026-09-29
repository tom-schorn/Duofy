"""one membership per person

Revision ID: b6d0e3f8a215
Revises: a5c9d2e7f104
Create Date: 2026-09-29 10:10:00.000000

Second of three steps that give every person exactly one household (#242).

**Upgrade.** Per person the oldest membership stays (`created_at`, ties broken by
`id`); the others are removed. What a person had shared into a household they
lose becomes private. Households left without members are deleted, together with
their invitations. A household that lost its owner gets the oldest remaining
member as owner. Every person without a household gets one of their own
("Haushalt von <first name>"), so that "at least one" holds as well as "at most
one". Then `UNIQUE (user_id)` replaces `uq_household_member`.

**Downgrade.** The unique rule on `user_id` goes and `uq_household_member` comes
back. The removed memberships, the deleted households and the households created
for people without one are **not** restored — that is lossy, and on purpose: the
data is not recoverable, and an extra empty household does no harm.
"""
from typing import Sequence, Union

import sqlalchemy as sa
from alembic import op

# revision identifiers, used by Alembic.
revision: str = "b6d0e3f8a215"
down_revision: Union[str, Sequence[str], None] = "a5c9d2e7f104"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None

#: The membership of each person that stays: the oldest one.
KEPT = """
    SELECT id FROM (
        SELECT id, row_number() OVER (
            PARTITION BY user_id ORDER BY created_at, id
        ) AS rank
        FROM household_members
    ) AS ranked
    WHERE rank = 1
"""


def upgrade() -> None:
    """Upgrade schema."""
    # What was shared into a household the owner is about to leave (or was never a
    # member of) turns private, before the memberships that say so are gone.
    op.execute(
        f"""
        UPDATE plan_positions AS p SET is_private = true
        FROM plans
        WHERE plans.id = p.plan_id AND p.household_id IS NOT NULL AND NOT EXISTS (
            SELECT 1 FROM household_members m
            WHERE m.household_id = p.household_id AND m.user_id = plans.user_id
              AND m.id IN ({KEPT})
        )
        """
    )
    op.execute(
        f"""
        UPDATE commitments AS c SET is_private = true
        WHERE c.household_id IS NOT NULL AND NOT EXISTS (
            SELECT 1 FROM household_members m
            WHERE m.household_id = c.household_id AND m.user_id = c.owner_id
              AND m.id IN ({KEPT})
        )
        """
    )

    op.execute(f"DELETE FROM household_members WHERE id NOT IN ({KEPT})")
    op.execute(
        "DELETE FROM households WHERE NOT EXISTS ("
        " SELECT 1 FROM household_members m WHERE m.household_id = households.id)"
    )
    # A household must keep somebody who can invite and rename.
    op.execute(
        """
        UPDATE household_members SET role = 'owner' WHERE id IN (
            SELECT DISTINCT ON (household_id) id FROM household_members h
            WHERE NOT EXISTS (
                SELECT 1 FROM household_members o
                WHERE o.household_id = h.household_id AND o.role = 'owner'
            )
            ORDER BY household_id, created_at, id
        )
        """
    )

    op.drop_constraint("uq_household_member", "household_members", type_="unique")
    op.create_unique_constraint("uq_household_member_user", "household_members", ["user_id"])

    # Everybody has one: a household of their own for whoever has none yet.
    connection = op.get_bind()
    lonely = connection.execute(
        sa.text(
            "SELECT id, first_name FROM users WHERE NOT EXISTS ("
            " SELECT 1 FROM household_members m WHERE m.user_id = users.id)"
        )
    ).all()
    for user_id, first_name in lonely:
        household_id = connection.execute(
            sa.text(
                "INSERT INTO households (id, name, target_needs, target_wants, target_savings)"
                " VALUES (gen_random_uuid(), :name, 50, 30, 20) RETURNING id"
            ),
            {"name": f"Haushalt von {first_name}"[:100]},
        ).scalar_one()
        connection.execute(
            sa.text(
                "INSERT INTO household_members (id, household_id, user_id, role, grants_plan,"
                " grants_commitments, grants_accounts) VALUES"
                " (gen_random_uuid(), :household, :user, 'owner', 'plan', 'plan', 'plan')"
            ),
            {"household": household_id, "user": user_id},
        )


def downgrade() -> None:
    """Downgrade schema."""
    op.drop_constraint("uq_household_member_user", "household_members", type_="unique")
    op.create_unique_constraint(
        "uq_household_member", "household_members", ["household_id", "user_id"]
    )
