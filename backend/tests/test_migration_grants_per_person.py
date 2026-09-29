# ruff: noqa: F811
"""The migration of #254 step 3, forwards and backwards on filled tables.

One level per member for everybody becomes one level per person, per other
member and per area. The world below has a household of three with mixed
levels and a person alone in a household of their own.
"""

import pytest
from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncConnection

from tests.test_migrations import (
    alembic,
    at_revision,  # noqa: F401  (a fixture, used by name below)
    columns,
)

BEFORE = "c7e1f4a9b326"
GRANTS = "a8f2c6d1e437"

pytestmark = pytest.mark.parametrize("at_revision", [BEFORE], indirect=True)

ALICE = "aaaaaaaa-0000-0000-0000-000000000001"
BEN = "aaaaaaaa-0000-0000-0000-000000000002"
CARLA = "aaaaaaaa-0000-0000-0000-000000000003"
DINO = "aaaaaaaa-0000-0000-0000-000000000004"

SHARED = "bbbbbbbb-0000-0000-0000-000000000001"
ALONE = "bbbbbbbb-0000-0000-0000-000000000002"

#: (household, user, role, plan, commitments, accounts)
MEMBERS = [
    (SHARED, ALICE, "owner", "edit", "plan", "view"),
    (SHARED, BEN, "member", "view", "delete", "plan"),
    (SHARED, CARLA, "member", "plan", "plan", "plan"),
    (ALONE, DINO, "owner", "view", "view", "view"),
]


async def seed(connection: AsyncConnection) -> None:
    for number, user_id in enumerate([ALICE, BEN, CARLA, DINO], start=1):
        await connection.execute(
            text(
                "INSERT INTO users (id, email, hashed_password, is_active, is_superuser,"
                " is_verified, first_name, last_name) VALUES"
                " (:id, :email, 'x', true, false, true, 'Test', 'Example')"
            ),
            {"id": user_id, "email": f"user{number}@example.invalid"},
        )
    for household_id in (SHARED, ALONE):
        await connection.execute(
            text(
                "INSERT INTO households (id, name, target_needs, target_wants, target_savings)"
                " VALUES (:id, 'Home', 50, 30, 20)"
            ),
            {"id": household_id},
        )
    for household_id, user_id, role, plan, commitments, accounts in MEMBERS:
        await connection.execute(
            text(
                "INSERT INTO household_members (id, household_id, user_id, role, grants_plan,"
                " grants_commitments, grants_accounts) VALUES"
                " (gen_random_uuid(), :household, :user, :role, :plan, :commitments, :accounts)"
            ),
            {
                "household": household_id,
                "user": user_id,
                "role": role,
                "plan": plan,
                "commitments": commitments,
                "accounts": accounts,
            },
        )


async def grants(connection: AsyncConnection) -> set[tuple[str, str, str, str]]:
    rows = await connection.execute(text("SELECT granter_id, grantee_id, area, level FROM grants"))
    return {(str(granter), str(grantee), area, level) for granter, grantee, area, level in rows}


async def old_levels(connection: AsyncConnection) -> dict[str, tuple[str, str, str]]:
    rows = await connection.execute(
        text(
            "SELECT user_id, grants_plan, grants_commitments, grants_accounts"
            " FROM household_members"
        )
    )
    return {
        str(user_id): (plan, commitments, accounts) for user_id, plan, commitments, accounts in rows
    }


async def test_every_member_grants_every_other_member_what_they_granted_everybody(
    at_revision: AsyncConnection,
):
    await seed(at_revision)

    alembic("upgrade", GRANTS)

    alice = {
        (ALICE, grantee, area, level)
        for grantee in (BEN, CARLA)
        for area, level in [
            ("plan", "edit"),
            ("accounts", "view"),
            ("book", "view"),
            ("import", "view"),
        ]
    }
    ben = {
        (BEN, grantee, area, level)
        for grantee in (ALICE, CARLA)
        for area, level in [("plan", "view"), ("commitments", "delete")]
    }
    assert await grants(at_revision) == alice | ben


async def test_the_old_step_plan_becomes_no_row_and_the_columns_are_gone(
    at_revision: AsyncConnection,
):
    """Carla shared nothing beyond the household view; Dino has nobody to grant to."""
    await seed(at_revision)

    alembic("upgrade", GRANTS)

    granters = {granter for granter, *_ in await grants(at_revision)}
    assert CARLA not in granters
    assert DINO not in granters
    assert await columns(at_revision, "grants_plan") == set()


async def test_the_database_refuses_a_grant_to_oneself_and_the_step_none(
    at_revision: AsyncConnection,
):
    await seed(at_revision)
    alembic("upgrade", GRANTS)

    for granter, grantee, level in [(ALICE, ALICE, "view"), (ALICE, BEN, "none")]:
        with pytest.raises(Exception, match="ck_grant"):
            await at_revision.execute(
                text(
                    "INSERT INTO grants (granter_id, grantee_id, area, level)"
                    " VALUES (:granter, :grantee, 'plan', :level)"
                ),
                {"granter": granter, "grantee": grantee, "level": level},
            )


async def test_the_downgrade_gives_the_same_levels_back_when_everybody_got_the_same(
    at_revision: AsyncConnection,
):
    await seed(at_revision)
    alembic("upgrade", GRANTS)

    alembic("downgrade", BEFORE)

    levels = await old_levels(at_revision)
    assert levels[ALICE] == ("edit", "plan", "view")
    assert levels[BEN] == ("view", "delete", "plan")
    assert levels[CARLA] == ("plan", "plan", "plan")
    # Alone in the household, Dino's level reached nobody and comes back as `plan`.
    assert levels[DINO] == ("plan", "plan", "plan")
    assert await columns(at_revision, "granter_id") == set()


async def test_the_downgrade_takes_the_lowest_level_and_never_grants_more(
    at_revision: AsyncConnection,
):
    """Alice gives Ben more than Carla after the upgrade; the old model has one level."""
    await seed(at_revision)
    alembic("upgrade", GRANTS)
    changes = [
        ("delete", BEN, "plan"),
        ("create", CARLA, "plan"),
        ("delete", BEN, "accounts"),
    ]
    for level, grantee, area in changes:
        await at_revision.execute(
            text(
                "UPDATE grants SET level = :level"
                " WHERE granter_id = :granter AND grantee_id = :grantee AND area = :area"
            ),
            {"level": level, "granter": ALICE, "grantee": grantee, "area": area},
        )
    await at_revision.execute(
        text(
            "DELETE FROM grants WHERE granter_id = :granter AND grantee_id = :grantee"
            " AND area = 'accounts'"
        ),
        {"granter": ALICE, "grantee": CARLA},
    )

    alembic("downgrade", BEFORE)

    plan, _, accounts = (await old_levels(at_revision))[ALICE]
    assert plan == "view"  # `create` for Carla counts as `view`
    assert accounts == "plan"  # Carla got nothing in accounts
