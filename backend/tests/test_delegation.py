"""Acting on somebody else's data: what each level allows, area by area.

`tests/test_area_permissions.py` pins down what `granted_level()` answers.
This file goes one layer up and asks what the endpoints do with that answer —
which is where the gap was: reading and editing delegated, creating did not.

The half that mattered was silent. `POST /plans` took no owner at all, so
pressing "create month" while standing in for someone built **your own** month
out of **your own** commitments, and said nothing.

The second half of this file guards the ladder itself. `AccessLevel` grew a
fourth step, and three checks were written as `level is AccessLevel.EDIT` — an
exact match, which quietly stops being true as soon as a higher level exists.
Somebody trusted with `delete` would have lost the right to edit.
"""

import uuid
from datetime import date
from decimal import Decimal

import pytest
from httpx import AsyncClient
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.orm import selectinload

from app.core.auth import current_active_user
from app.main import app
from app.models.commitment import Commitment
from app.models.enums import AccessLevel, Block, Category, CommitmentType, Rhythm
from app.models.plan import Plan
from app.models.user import User
from tests.test_area_permissions import add_member, make_household, make_user


def sign_in(user: User) -> None:
    """Answer every request as `user`, without going through the login."""
    app.dependency_overrides[current_active_user] = lambda: user


async def make_commitment(session: AsyncSession, owner: User, name: str) -> Commitment:
    commitment = Commitment(
        owner_id=owner.id,
        type=CommitmentType.CONTRACT,
        name=name,
        amount=Decimal("50.00"),
        category=Category.LEISURE_SUBSCRIPTIONS,
        block=Block.WANTS,
        rhythm=Rhythm.MONTHLY,
        first_due_date=date(2026, 1, 1),
        due_day=1,
        active=True,
    )
    session.add(commitment)
    await session.flush()
    return commitment


@pytest.fixture
async def pair(session: AsyncSession):
    """Two people in one household, and one contract each.

    The contracts carry different names on purpose — that is how a month built
    from the wrong person's commitments becomes visible.
    """
    owner = await make_user(session, "Owner")
    helper = await make_user(session, "Helper")
    household = await make_household(session, "Shared")

    await add_member(session, household, owner)
    await add_member(session, household, helper)

    await make_commitment(session, owner, "Owner contract")
    await make_commitment(session, helper, "Helper contract")
    await session.commit()

    yield owner, helper, household
    app.dependency_overrides.pop(current_active_user, None)


async def grant_area(
    session: AsyncSession, household, user: User, area: str, level: AccessLevel
) -> None:
    """What `user` hands out about themselves in one area."""
    from app.models.household import HouseholdMember

    row = await session.execute(
        select(HouseholdMember).where(
            HouseholdMember.household_id == household.id,
            HouseholdMember.user_id == user.id,
        )
    )
    member = row.scalar_one()
    setattr(member, f"grants_{area}", level)
    await session.commit()


async def grant(session: AsyncSession, household, user: User, level: AccessLevel) -> None:
    """The plan area, which most of these tests are about."""
    await grant_area(session, household, user, "plan", level)


# --- Creating a month ------------------------------------------------------


async def test_create_month_for_another_member(
    client: AsyncClient, session: AsyncSession, pair
):
    """With `edit` the month lands on the owner — and grows from their contracts.

    Both halves are checked, because the old behaviour got both wrong at once
    and neither showed up as an error.
    """
    owner, helper, household = pair
    await grant(session, household, owner, AccessLevel.EDIT)
    sign_in(helper)

    response = await client.post(
        f"/api/v1/plans?owner={owner.id}", json={"year": 2026, "month": 9}
    )
    assert response.status_code == 201

    rows = await session.execute(
        select(Plan).where(Plan.year == 2026).options(selectinload(Plan.positions))
    )
    plans = rows.scalars().all()
    assert [plan.user_id for plan in plans] == [owner.id]

    labels = {position.label for position in plans[0].positions}
    assert labels == {"Owner contract"}


async def test_create_month_for_another_member_needs_edit(
    client: AsyncClient, session: AsyncSession, pair
):
    """`view` is enough to look, never enough to create."""
    owner, helper, household = pair
    await grant(session, household, owner, AccessLevel.VIEW)
    sign_in(helper)

    response = await client.post(
        f"/api/v1/plans?owner={owner.id}", json={"year": 2026, "month": 9}
    )
    assert response.status_code == 403
    assert response.json()["detail"]["code"] == "no_edit_granted"

    assert (await session.execute(select(Plan))).scalars().first() is None


async def test_creating_without_owner_still_builds_your_own(
    client: AsyncClient, session: AsyncSession, pair
):
    """The parameter is optional, and leaving it out means yourself."""
    _owner, helper, _household = pair
    sign_in(helper)

    response = await client.post("/api/v1/plans", json={"year": 2026, "month": 9})
    assert response.status_code == 201

    rows = await session.execute(select(Plan).options(selectinload(Plan.positions)))
    plan = rows.scalars().one()
    assert plan.user_id == helper.id
    assert {position.label for position in plan.positions} == {"Helper contract"}


# --- Adding a position -----------------------------------------------------


async def make_plan(session: AsyncSession, owner: User) -> Plan:
    plan = Plan(user_id=owner.id, year=2026, month=9)
    session.add(plan)
    await session.commit()
    await session.refresh(plan)
    return plan


def position_payload() -> dict:
    return {
        "label": "Added by the helper",
        "amountPlanned": "12.00",
        "category": "leisure.dining",
        "block": "wants",
        "dueDay": 15,
    }


async def test_add_position_to_another_members_month(
    client: AsyncClient, session: AsyncSession, pair
):
    """Standing in means being able to add, not only to correct.

    A delegate who may change a position but not add one is stuck the moment
    something is missing — which is the usual reason for helping.
    """
    owner, helper, household = pair
    await grant(session, household, owner, AccessLevel.EDIT)
    plan = await make_plan(session, owner)
    sign_in(helper)

    response = await client.post(
        f"/api/v1/plans/{plan.id}/positions", json=position_payload()
    )
    assert response.status_code == 201

    await session.refresh(plan, ["positions"])
    assert [position.label for position in plan.positions] == ["Added by the helper"]


async def test_add_position_to_another_members_month_needs_edit(
    client: AsyncClient, session: AsyncSession, pair
):
    owner, helper, household = pair
    await grant(session, household, owner, AccessLevel.VIEW)
    plan = await make_plan(session, owner)
    sign_in(helper)

    response = await client.post(
        f"/api/v1/plans/{plan.id}/positions", json=position_payload()
    )
    assert response.status_code == 403
    assert response.json()["detail"]["code"] == "no_edit_granted"


async def test_a_stranger_gets_nothing(client: AsyncClient, session: AsyncSession, pair):
    """No shared household at all — not even the plan's existence leaks."""
    owner, _helper, _household = pair
    stranger = await make_user(session, "Stranger")
    plan = await make_plan(session, owner)
    await session.commit()
    sign_in(stranger)

    response = await client.post(
        f"/api/v1/plans/{plan.id}/positions", json=position_payload()
    )
    assert response.status_code == 403

    response = await client.post(
        f"/api/v1/plans?owner={owner.id}", json={"year": 2026, "month": 10}
    )
    assert response.status_code == 403


# --- The ladder ------------------------------------------------------------


async def test_delete_also_allows_editing(
    client: AsyncClient, session: AsyncSession, pair
):
    """The one that would have gone wrong silently.

    Levels build on each other, so the highest one has to contain every right
    below it. Written as an exact comparison — `level is AccessLevel.EDIT` — the
    check fails for `delete`, and the person trusted most loses the most.
    """
    owner, helper, household = pair
    await grant(session, household, owner, AccessLevel.DELETE)
    plan = await make_plan(session, owner)
    sign_in(helper)

    response = await client.post(
        f"/api/v1/plans/{plan.id}/positions", json=position_payload()
    )
    assert response.status_code == 201


async def test_edit_is_not_enough_to_delete(
    client: AsyncClient, session: AsyncSession, pair
):
    """Deleting is its own step, not a bonus that comes with editing.

    A change lands in `plan_position_changes` and can be undone. A deletion is
    in no log and cannot.
    """
    owner, helper, household = pair
    await grant(session, household, owner, AccessLevel.EDIT)
    plan = await make_plan(session, owner)
    sign_in(helper)

    created = await client.post(
        f"/api/v1/plans/{plan.id}/positions", json=position_payload()
    )
    position_id = created.json()["id"]

    response = await client.delete(f"/api/v1/positions/{position_id}")
    assert response.status_code == 403
    assert response.json()["detail"]["code"] == "no_delete_granted"


async def test_delete_level_may_delete(client: AsyncClient, session: AsyncSession, pair):
    owner, helper, household = pair
    await grant(session, household, owner, AccessLevel.DELETE)
    plan = await make_plan(session, owner)
    sign_in(helper)

    created = await client.post(
        f"/api/v1/plans/{plan.id}/positions", json=position_payload()
    )
    response = await client.delete(f"/api/v1/positions/{created.json()['id']}")
    assert response.status_code == 204


# --- Accounts: the area that never delegated -------------------------------


def account_payload() -> dict:
    return {
        "name": "Helper made this",
        "type": "checking",
        "openingBalance": "0.00",
        "openingDate": "2026-08-01",
    }


async def test_create_and_change_an_account_for_another_member(
    client: AsyncClient, session: AsyncSession, pair
):
    """Accounts were the one area where `edit` did nothing.

    A delegate could book on somebody else's account through `transactions.py`
    but could not rename the account itself — the same area, two answers.
    """
    from app.models.account import Account

    owner, helper, household = pair
    await grant_area(session, household, owner, "accounts", AccessLevel.EDIT)
    sign_in(helper)

    created = await client.post(
        f"/api/v1/accounts?owner={owner.id}", json=account_payload()
    )
    assert created.status_code == 201

    account = await session.get(Account, uuid.UUID(created.json()["id"]))
    assert account is not None
    assert account.owner_id == owner.id

    changed = await client.patch(
        f"/api/v1/accounts/{account.id}", json={"name": "Renamed"}
    )
    assert changed.status_code == 200

    deleted = await client.delete(f"/api/v1/accounts/{account.id}")
    assert deleted.status_code == 403
    assert deleted.json()["detail"]["code"] == "no_delete_granted"


async def test_create_a_commitment_for_another_member(
    client: AsyncClient, session: AsyncSession, pair
):
    """Filling somebody's contracts in for them — the support case."""
    owner, helper, household = pair
    await grant_area(session, household, owner, "commitments", AccessLevel.EDIT)
    sign_in(helper)

    response = await client.post(
        f"/api/v1/commitments?owner={owner.id}",
        json={
            "name": "Entered by the helper",
            "amount": "19.99",
            "category": "leisure.subscriptions",
            "block": "wants",
            "type": "contract",
            "rhythm": "monthly",
            "firstDueDate": "2026-01-01",
            "dueDay": 1,
        },
    )
    assert response.status_code == 201
    assert response.json()["ownerId"] == str(owner.id)


async def test_areas_do_not_leak_into_one_another(
    client: AsyncClient, session: AsyncSession, pair
):
    """`edit` on the plan says nothing about the contracts."""
    owner, helper, household = pair
    await grant(session, household, owner, AccessLevel.DELETE)
    sign_in(helper)

    response = await client.post(
        f"/api/v1/commitments?owner={owner.id}",
        json={
            "name": "Should not appear",
            "amount": "1.00",
            "category": "leisure.hobbies",
            "block": "wants",
            "type": "contract",
            "rhythm": "monthly",
            "firstDueDate": "2026-01-01",
            "dueDay": 1,
        },
    )
    assert response.status_code == 403
