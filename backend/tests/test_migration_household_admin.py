# ruff: noqa: F811
"""The owner becomes the admin (#254 step 3, part A2), forwards and backwards.

Only the stored value of the role changes; members stay members. Several admins
need nothing in the schema — they are several rows with the role `admin`.
"""

import pytest
from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncConnection

from tests.test_migrations import (
    alembic,
    at_revision,  # noqa: F401  (a fixture, used by name below)
)

BEFORE = "a8f2c6d1e437"
ADMIN = "d4a7b2e9c813"

pytestmark = pytest.mark.parametrize("at_revision", [BEFORE], indirect=True)

ALICE = "cccccccc-0000-0000-0000-000000000001"
BEN = "cccccccc-0000-0000-0000-000000000002"
CARLA = "cccccccc-0000-0000-0000-000000000003"

SHARED = "dddddddd-0000-0000-0000-000000000001"
ALONE = "dddddddd-0000-0000-0000-000000000002"

#: (household, user, role before)
MEMBERS = [
    (SHARED, ALICE, "owner"),
    (SHARED, BEN, "member"),
    (ALONE, CARLA, "owner"),
]


async def seed(connection: AsyncConnection) -> None:
    for number, user_id in enumerate([ALICE, BEN, CARLA], start=1):
        await connection.execute(
            text(
                "INSERT INTO users (id, email, hashed_password, is_active, is_superuser,"
                " is_verified, first_name, last_name) VALUES"
                " (:id, :email, 'x', true, false, true, 'Test', 'Example')"
            ),
            {"id": user_id, "email": f"admin{number}@example.invalid"},
        )
    for household_id in (SHARED, ALONE):
        await connection.execute(
            text(
                "INSERT INTO households (id, name, target_needs, target_wants, target_savings)"
                " VALUES (:id, 'Home', 50, 30, 20)"
            ),
            {"id": household_id},
        )
    for household_id, user_id, role in MEMBERS:
        await connection.execute(
            text(
                "INSERT INTO household_members (id, household_id, user_id, role)"
                " VALUES (gen_random_uuid(), :household, :user, :role)"
            ),
            {"household": household_id, "user": user_id, "role": role},
        )


async def roles(connection: AsyncConnection) -> dict[str, str]:
    rows = await connection.execute(text("SELECT user_id, role FROM household_members"))
    return {str(user_id): role for user_id, role in rows}


async def test_every_owner_becomes_an_admin_and_members_stay_members(
    at_revision: AsyncConnection,
):
    await seed(at_revision)

    alembic("upgrade", ADMIN)

    assert await roles(at_revision) == {ALICE: "admin", BEN: "member", CARLA: "admin"}


async def test_the_downgrade_turns_every_admin_back_into_an_owner(
    at_revision: AsyncConnection,
):
    """Two admins after the upgrade come back as two owners; the old model allowed that."""
    await seed(at_revision)
    alembic("upgrade", ADMIN)
    await at_revision.execute(
        text("UPDATE household_members SET role = 'admin' WHERE user_id = :user"),
        {"user": BEN},
    )

    alembic("downgrade", BEFORE)

    assert await roles(at_revision) == {ALICE: "owner", BEN: "owner", CARLA: "owner"}
