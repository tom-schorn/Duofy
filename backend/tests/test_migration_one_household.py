# ruff: noqa: F811
"""The three migrations of #242, forwards and backwards on filled tables.

Every person belongs to exactly one household, and a position or commitment no
longer names a household of its own: it is private, or it belongs to the one
household of its owner.

The migrations run in three steps so that each one is a single change:

1. `is_private` appears and is filled from the old `household_id IS NULL`.
2. Memberships are cleaned up: one per person, and everybody has one.
3. `household_id` leaves positions and commitments.

The data below is a small world that hits every branch of step two: a person in
three households (the oldest membership stays), a household that ends up empty,
a household that loses its owner, and a person without any household.
"""

from datetime import datetime

import pytest
from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncConnection

from tests.test_migrations import (
    alembic,
    at_revision,  # noqa: F401  (a fixture, used by name below)
    columns,
)

BEFORE = "c5a8e2f7d941"
PRIVATE_FLAG = "a5c9d2e7f104"
MEMBERSHIPS = "b6d0e3f8a215"
DROP_HOUSEHOLD = "c7e1f4a9b326"

pytestmark = pytest.mark.parametrize("at_revision", [BEFORE], indirect=True)

ALICE = "aaaaaaaa-0000-0000-0000-000000000001"
BEN = "aaaaaaaa-0000-0000-0000-000000000002"
CARLA = "aaaaaaaa-0000-0000-0000-000000000003"
DINO = "aaaaaaaa-0000-0000-0000-000000000004"

#: The oldest household, Alice owns it and Ben is a member.
OLD = "bbbbbbbb-0000-0000-0000-000000000001"
#: Alice's second membership; Carla is a member and stays.
SECOND = "bbbbbbbb-0000-0000-0000-000000000002"
#: Alice's third membership, and nobody else: empty once Alice is removed.
THIRD = "bbbbbbbb-0000-0000-0000-000000000003"


async def seed_world(connection: AsyncConnection) -> None:
    for number, (user_id, first_name) in enumerate(
        [(ALICE, "Alice"), (BEN, "Ben"), (CARLA, "Carla"), (DINO, "Dino")], start=1
    ):
        await connection.execute(
            text(
                "INSERT INTO users (id, email, hashed_password, is_active, is_superuser,"
                " is_verified, first_name, last_name) VALUES"
                " (:id, :email, 'x', true, false, true, :first_name, 'Example')"
            ),
            {"id": user_id, "email": f"user{number}@example.invalid", "first_name": first_name},
        )
    for household_id, name in [(OLD, "Old"), (SECOND, "Second"), (THIRD, "Third")]:
        await connection.execute(
            text(
                "INSERT INTO households (id, name, target_needs, target_wants, target_savings)"
                " VALUES (:id, :name, 50, 30, 20)"
            ),
            {"id": household_id, "name": name},
        )
    memberships = [
        (OLD, ALICE, "owner", "2026-01-01"),
        (SECOND, ALICE, "owner", "2026-02-01"),
        (THIRD, ALICE, "owner", "2026-03-01"),
        (OLD, BEN, "member", "2026-01-05"),
        (SECOND, CARLA, "member", "2026-02-05"),
    ]
    for household_id, user_id, role, created in memberships:
        await connection.execute(
            text(
                "INSERT INTO household_members (id, household_id, user_id, role, grants_plan,"
                " grants_commitments, grants_accounts, created_at) VALUES"
                " (gen_random_uuid(), :household, :user, :role, 'plan', 'plan', 'plan',"
                " :created)"
            ),
            {
                "household": household_id,
                "user": user_id,
                "role": role,
                "created": datetime.fromisoformat(f"{created}T12:00:00+00:00"),
            },
        )
    await connection.execute(
        text(
            "INSERT INTO plans (id, user_id, year, month, target_needs, target_wants,"
            " target_savings) VALUES (:id, :user, 2026, 9, 50, 30, 20)"
        ),
        {"id": PLAN, "user": ALICE},
    )
    await connection.execute(
        text(
            "INSERT INTO plans (id, user_id, year, month, target_needs, target_wants,"
            " target_savings) VALUES (:id, :user, 2026, 9, 50, 30, 20)"
        ),
        {"id": PLAN_CARLA, "user": CARLA},
    )
    # (id suffix, owner, plan, household): shared in the oldest household, shared in a
    # household Alice is removed from, private (NULL), and Carla's, which stays shared.
    for suffix, owner, plan, household in POSITIONS:
        await connection.execute(
            text(
                "INSERT INTO commitments (id, owner_id, type, name, amount, category, budget,"
                " interval_months, first_due_date, pass_through, is_limit, household_id)"
                " VALUES (:id, :owner, 'contract', :name, 10.00, 'leisure.subscriptions',"
                " 'wants', 1, '2026-01-01', false, false, :household)"
            ),
            {"id": commitment_id(suffix), "owner": owner, "name": suffix, "household": household},
        )
        await connection.execute(
            text(
                "INSERT INTO plan_positions (id, plan_id, commitment_id, label, amount_planned,"
                " category, budget, due_day, manually_changed, is_limit, pass_through,"
                " household_id) VALUES (:id, :plan, :commitment, :name, 10.00,"
                " 'leisure.subscriptions', 'wants', 1, false, false, false, :household)"
            ),
            {
                "id": position_id(suffix),
                "plan": plan,
                "commitment": commitment_id(suffix),
                "name": suffix,
                "household": household,
            },
        )


PLAN = "cccccccc-0000-0000-0000-000000000001"
PLAN_CARLA = "cccccccc-0000-0000-0000-000000000002"

POSITIONS = [
    ("oldest", ALICE, PLAN, OLD),
    ("removed", ALICE, PLAN, SECOND),
    ("empty", ALICE, PLAN, THIRD),
    ("nobody", ALICE, PLAN, None),
    ("carla", CARLA, PLAN_CARLA, SECOND),
]


def commitment_id(suffix: str) -> str:
    return f"dddddddd-0000-0000-0000-{POSITIONS_INDEX[suffix]:012d}"


def position_id(suffix: str) -> str:
    return f"eeeeeeee-0000-0000-0000-{POSITIONS_INDEX[suffix]:012d}"


POSITIONS_INDEX = {suffix: number for number, (suffix, *_rest) in enumerate(POSITIONS, start=1)}


async def private_flags(connection: AsyncConnection, table: str) -> dict[str, bool]:
    """`is_private` per item, keyed by the name the seed gave it."""
    name_column = "label" if table == "plan_positions" else "name"
    rows = await connection.execute(text(f"SELECT {name_column}, is_private FROM {table}"))
    return {row[0]: row[1] for row in rows}


async def members_by_household(connection: AsyncConnection) -> dict[str, set[str]]:
    rows = await connection.execute(text("SELECT household_id, user_id FROM household_members"))
    result: dict[str, set[str]] = {}
    for household_id, user_id in rows:
        result.setdefault(str(household_id), set()).add(str(user_id))
    return result


async def test_the_first_step_marks_what_belonged_to_no_household_as_private(
    at_revision: AsyncConnection,
):
    await seed_world(at_revision)

    alembic("upgrade", PRIVATE_FLAG)

    for table in ("plan_positions", "commitments"):
        flags = await private_flags(at_revision, table)
        assert flags == {
            "oldest": False,
            "removed": False,
            "empty": False,
            "nobody": True,
            "carla": False,
        }


async def test_the_first_step_downgrade_takes_the_column_away_again(at_revision: AsyncConnection):
    await seed_world(at_revision)
    alembic("upgrade", PRIVATE_FLAG)

    alembic("downgrade", BEFORE)

    assert await columns(at_revision, "is_private") == set()


async def test_the_second_step_keeps_the_oldest_membership_of_a_person(
    at_revision: AsyncConnection,
):
    await seed_world(at_revision)

    alembic("upgrade", MEMBERSHIPS)

    members = await members_by_household(at_revision)
    assert members[OLD] == {ALICE, BEN}
    assert members[SECOND] == {CARLA}
    assert THIRD not in members


async def test_the_second_step_deletes_a_household_left_empty(at_revision: AsyncConnection):
    await seed_world(at_revision)

    alembic("upgrade", MEMBERSHIPS)

    rows = await at_revision.execute(
        text("SELECT id FROM households WHERE id = :id"), {"id": THIRD}
    )
    assert rows.first() is None


async def test_the_second_step_makes_the_oldest_remaining_member_the_owner(
    at_revision: AsyncConnection,
):
    """Alice owned the second household and leaves it; Carla is all that remains."""
    await seed_world(at_revision)

    alembic("upgrade", MEMBERSHIPS)

    rows = await at_revision.execute(
        text("SELECT role FROM household_members WHERE household_id = :id AND user_id = :user"),
        {"id": SECOND, "user": CARLA},
    )
    assert rows.scalar_one() == "owner"


async def test_the_second_step_marks_what_was_shared_into_a_lost_household_as_private(
    at_revision: AsyncConnection,
):
    await seed_world(at_revision)

    alembic("upgrade", MEMBERSHIPS)

    for table in ("plan_positions", "commitments"):
        flags = await private_flags(at_revision, table)
        assert flags == {
            "oldest": False,
            "removed": True,
            "empty": True,
            "nobody": True,
            "carla": False,
        }


async def test_the_second_step_gives_a_person_without_a_household_one_of_their_own(
    at_revision: AsyncConnection,
):
    await seed_world(at_revision)

    alembic("upgrade", MEMBERSHIPS)

    rows = await at_revision.execute(
        text(
            "SELECT h.name, m.role FROM household_members m JOIN households h"
            " ON h.id = m.household_id WHERE m.user_id = :user"
        ),
        {"user": DINO},
    )
    assert [tuple(row) for row in rows] == [("Haushalt von Dino", "owner")]


async def test_after_the_second_step_nobody_can_be_in_two_households(
    at_revision: AsyncConnection,
):
    await seed_world(at_revision)
    alembic("upgrade", MEMBERSHIPS)

    with pytest.raises(Exception, match="uq_household_member_user"):
        await at_revision.execute(
            text(
                "INSERT INTO household_members (id, household_id, user_id, role, grants_plan,"
                " grants_commitments, grants_accounts) VALUES (gen_random_uuid(), :household,"
                " :user, 'member', 'plan', 'plan', 'plan')"
            ),
            {"household": SECOND, "user": BEN},
        )


async def test_the_second_step_downgrade_lets_a_person_join_a_second_household_again(
    at_revision: AsyncConnection,
):
    """What was removed stays removed, but the rule that forbade it is gone."""
    await seed_world(at_revision)
    alembic("upgrade", MEMBERSHIPS)

    alembic("downgrade", PRIVATE_FLAG)

    await at_revision.execute(
        text(
            "INSERT INTO household_members (id, household_id, user_id, role, grants_plan,"
            " grants_commitments, grants_accounts) VALUES (gen_random_uuid(), :household,"
            " :user, 'member', 'plan', 'plan', 'plan')"
        ),
        {"household": SECOND, "user": BEN},
    )
    members = await members_by_household(at_revision)
    assert BEN in members[SECOND]
    assert BEN in members[OLD]


async def test_the_third_step_drops_the_household_from_positions_and_commitments(
    at_revision: AsyncConnection,
):
    await seed_world(at_revision)

    alembic("upgrade", DROP_HOUSEHOLD)

    tables = await columns(at_revision, "household_id")
    assert "plan_positions" not in tables
    assert "commitments" not in tables
    assert "household_members" in tables


async def test_the_third_step_downgrade_gives_shared_items_their_household_back(
    at_revision: AsyncConnection,
):
    await seed_world(at_revision)
    alembic("upgrade", DROP_HOUSEHOLD)

    alembic("downgrade", MEMBERSHIPS)

    for table, column in (("plan_positions", "label"), ("commitments", "name")):
        rows = await at_revision.execute(text(f"SELECT {column}, household_id FROM {table}"))
        households = {row[0]: str(row[1]) if row[1] else None for row in rows}
        assert households == {
            "oldest": OLD,
            "removed": None,
            "empty": None,
            "nobody": None,
            "carla": SECOND,
        }


async def test_the_whole_chain_runs_up_down_and_up_again(at_revision: AsyncConnection):
    await seed_world(at_revision)

    alembic("upgrade", DROP_HOUSEHOLD)
    alembic("downgrade", BEFORE)
    alembic("upgrade", DROP_HOUSEHOLD)

    members = await members_by_household(at_revision)
    assert all(len(user_ids) >= 1 for user_ids in members.values())
    assert len({user for user_ids in members.values() for user in user_ids}) == 4
