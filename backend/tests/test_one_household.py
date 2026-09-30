"""Everybody belongs to exactly one household (#242).

The rules worth pinning down: registering gives you one, an invitation moves you
only when nobody would be left behind, the household plan shows what is not private
whatever the grants say, and the personal pages of the others still ask for a grant.
"""

from decimal import Decimal

import pytest
from httpx import AsyncClient
from sqlalchemy import select
from sqlalchemy.exc import IntegrityError
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.enums import Budget, Category, Role
from app.models.household import Household, HouseholdInvitation, HouseholdMember
from app.models.plan import Plan, PlanPosition
from tests.test_area_permissions import add_member, make_household, make_user
from tests.test_delegation import sign_in
from tests.test_instance_registration import register


async def households_of(session: AsyncSession, user_id) -> list[Household]:
    session.expire_all()
    return list(
        await session.scalars(
            select(Household)
            .join(HouseholdMember, HouseholdMember.household_id == Household.id)
            .where(HouseholdMember.user_id == user_id)
        )
    )


async def invite(session: AsyncSession, household, inviter, invitee) -> str:
    invitation = HouseholdInvitation(
        household_id=household.id, invited_by_id=inviter.id, email=invitee.email.lower()
    )
    session.add(invitation)
    await session.commit()
    return invitation.token


async def add_position(session: AsyncSession, user, label: str, **kwargs) -> PlanPosition:
    plan = await session.scalar(select(Plan).where(Plan.user_id == user.id))
    if plan is None:
        plan = Plan(user_id=user.id, year=2026, month=9)
        session.add(plan)
        await session.flush()
    kwargs.setdefault("amount_planned", Decimal("10.00"))
    position = PlanPosition(
        plan_id=plan.id,
        label=label,
        category=Category.LEISURE_SUBSCRIPTIONS,
        budget=Budget.WANTS,
        due_day=15,
        **kwargs,
    )
    session.add(position)
    await session.flush()
    return position


async def test_registering_gives_the_new_person_a_household_of_their_own(
    client: AsyncClient, session: AsyncSession
) -> None:
    response = await register(client, "anna@example.org")
    assert response.status_code == 201

    session.expire_all()
    [membership] = (await session.scalars(select(HouseholdMember))).all()
    assert membership.role is Role.ADMIN
    household = await session.get(Household, membership.household_id)
    assert household.name.startswith("Haushalt von ")


async def test_nobody_can_be_a_member_of_two_households(session: AsyncSession) -> None:
    person = await make_user(session, "Person")
    first = await make_household(session, "First")
    second = await make_household(session, "Second")
    await add_member(session, first, person)

    with pytest.raises(IntegrityError):
        await add_member(session, second, person)


async def test_accepting_an_invitation_moves_a_person_out_of_their_empty_household(
    client: AsyncClient, session: AsyncSession
) -> None:
    host = await make_user(session, "Host")
    guest = await make_user(session, "Guest")
    host_household = await make_household(session, "Host home")
    await add_member(session, host_household, host)
    guest_household = await make_household(session, "Guest home")
    await add_member(session, guest_household, guest)
    guest_household_id, host_household_id, guest_id = (
        guest_household.id,
        host_household.id,
        guest.id,
    )
    token = await invite(session, host_household, host, guest)
    sign_in(guest)

    response = await client.post(f"/api/v1/households/invitations/{token}/accept")

    assert response.status_code == 200
    assert [h.id for h in await households_of(session, guest_id)] == [host_household_id]
    assert await session.get(Household, guest_household_id) is None


async def test_accepting_is_refused_while_others_live_in_the_own_household(
    client: AsyncClient, session: AsyncSession
) -> None:
    host = await make_user(session, "Host")
    guest = await make_user(session, "Guest")
    flatmate = await make_user(session, "Flatmate")
    host_household = await make_household(session, "Host home")
    await add_member(session, host_household, host)
    guest_household = await make_household(session, "Guest home")
    await add_member(session, guest_household, guest)
    await add_member(session, guest_household, flatmate)
    guest_household_id, guest_id = guest_household.id, guest.id
    token = await invite(session, host_household, host, guest)
    sign_in(guest)

    response = await client.post(f"/api/v1/households/invitations/{token}/accept")

    assert response.status_code == 409
    assert response.json()["detail"]["code"] == "household_not_empty"
    assert [h.id for h in await households_of(session, guest_id)] == [guest_household_id]


async def test_the_household_plan_shows_every_shared_position_whatever_the_grants(
    client: AsyncClient, session: AsyncSession
) -> None:
    """Nobody granted anything: the defaults share the plan and nothing else, yet the
    household view lists the shared positions of both, and skips the private one."""
    ada = await make_user(session, "Ada")
    bob = await make_user(session, "Bob")
    household = await make_household(session, "Together")
    await add_member(session, household, ada)
    await add_member(session, household, bob)
    await add_position(session, ada, "Ada shared")
    await add_position(session, ada, "Ada private", is_private=True)
    await add_position(session, bob, "Bob shared")
    await session.commit()
    sign_in(bob)

    response = await client.get(f"/api/v1/plans/2026/9?household={household.id}")

    assert response.status_code == 200
    labels = sorted(p["label"] for p in response.json()["positions"])
    assert labels == ["Ada shared", "Bob shared"]


async def test_the_personal_pages_of_the_other_still_need_their_grant(
    client: AsyncClient, session: AsyncSession
) -> None:
    ada = await make_user(session, "Ada")
    bob = await make_user(session, "Bob")
    household = await make_household(session, "Together")
    await add_member(session, household, ada)
    await add_member(session, household, bob)
    await add_position(session, ada, "Ada private", is_private=True)
    await session.commit()
    sign_in(bob)

    response = await client.get(f"/api/v1/plans/2026/9?owner={ada.id}")

    assert response.status_code == 403
    assert response.json()["detail"]["code"] == "no_insight_granted"


async def test_a_month_built_from_contracts_copies_the_private_flag(
    client: AsyncClient, session: AsyncSession
) -> None:
    from tests.test_delegation import make_commitment

    ada = await make_user(session, "Ada")
    household = await make_household(session, "Alone")
    await add_member(session, household, ada)
    shared = await make_commitment(session, ada, "Shared contract")
    hidden = await make_commitment(session, ada, "Hidden contract")
    hidden.is_private = True
    await session.commit()
    sign_in(ada)

    created = await client.post("/api/v1/plans", json={"year": 2026, "month": 9})

    assert created.status_code == 201
    private_by_label = {p["label"]: p["isPrivate"] for p in created.json()["positions"]}
    assert private_by_label == {shared.name: False, hidden.name: True}


async def test_the_household_flow_leaves_out_private_positions(
    client: AsyncClient, session: AsyncSession
) -> None:
    ada = await make_user(session, "Ada")
    household = await make_household(session, "Together")
    await add_member(session, household, ada)
    await add_position(session, ada, "Ada shared")
    await add_position(
        session, ada, "Ada private", is_private=True, amount_planned=Decimal("77.00")
    )
    await session.commit()
    sign_in(ada)

    response = await client.get(f"/api/v1/plans/2026/9/flow?household={household.id}")

    assert response.status_code == 200
    assert [entry["amount"] for entry in response.json()["entries"]] == ["-10.00"]


async def test_the_household_month_list_leaves_out_private_positions(
    client: AsyncClient, session: AsyncSession
) -> None:
    ada = await make_user(session, "Ada")
    household = await make_household(session, "Together")
    await add_member(session, household, ada)
    await add_position(session, ada, "Ada shared")
    await add_position(
        session, ada, "Ada private", is_private=True, amount_planned=Decimal("77.00")
    )
    await session.commit()
    sign_in(ada)

    response = await client.get(f"/api/v1/plans?household={household.id}")

    assert response.status_code == 200
    (month,) = response.json()
    assert Decimal(month["spent"]["wants"]) == Decimal("10.00")
