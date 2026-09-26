"""add instance invitations

Revision ID: a1d4e7c92b30
Revises: f7c1a3d58e29
Create Date: 2026-09-26 22:30:00.000000

`instance_invitations` (#168): the tickets an admin hands out so that somebody can
register on the instance while `REGISTRATION_MODE=invite`. Not to be confused with
`household_invitations`, which invite into a household.

**Upgrade.** A new, empty table. Nothing existing changes.

**Downgrade.** The table and its invitations go. Registered users are unaffected.
"""
from typing import Sequence, Union

import sqlalchemy as sa
from alembic import op

# revision identifiers, used by Alembic.
revision: str = "a1d4e7c92b30"
down_revision: Union[str, Sequence[str], None] = "f7c1a3d58e29"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    """Upgrade schema."""
    op.create_table(
        "instance_invitations",
        sa.Column("id", sa.Uuid(), nullable=False),
        sa.Column("token", sa.String(length=64), nullable=False),
        sa.Column("created_by_id", sa.Uuid(), nullable=False),
        sa.Column("email", sa.String(length=320), nullable=True),
        sa.Column("expires_at", sa.DateTime(timezone=True), nullable=False),
        sa.Column("used_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("used_by_id", sa.Uuid(), nullable=True),
        sa.Column(
            "created_at",
            sa.DateTime(timezone=True),
            server_default=sa.text("now()"),
            nullable=False,
        ),
        sa.ForeignKeyConstraint(
            ["created_by_id"],
            ["users.id"],
            name="fk_instance_invitations_created_by_id",
            ondelete="CASCADE",
        ),
        sa.ForeignKeyConstraint(
            ["used_by_id"],
            ["users.id"],
            name="fk_instance_invitations_used_by_id",
            ondelete="SET NULL",
        ),
        sa.PrimaryKeyConstraint("id"),
    )
    op.create_index(
        op.f("ix_instance_invitations_token"), "instance_invitations", ["token"], unique=True
    )
    op.create_index(
        op.f("ix_instance_invitations_created_by_id"),
        "instance_invitations",
        ["created_by_id"],
        unique=False,
    )


def downgrade() -> None:
    """Downgrade schema."""
    op.drop_index(op.f("ix_instance_invitations_created_by_id"), table_name="instance_invitations")
    op.drop_index(op.f("ix_instance_invitations_token"), table_name="instance_invitations")
    op.drop_table("instance_invitations")
