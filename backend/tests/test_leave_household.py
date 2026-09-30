"""Leaving a household hands the leaver a household of their own (#54, #242).

Everybody belongs to exactly one household, so leaving is really moving out: into a
new household with nobody else in it. Nothing is deleted. The leaver's plans,
positions and contracts stay theirs, but they hang on the person, so the household
they left stops showing them — in every month, past ones included.
"""

from decimal import Decimal

from httpx import AsyncClient
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.commitment import Commitment
from app.models.enums import Budget, Category, Role
from app.models.household import Household, HouseholdMember
from app.models.plan import Plan, PlanPosition
from tests.test_delegation import pair, sign_in  # noqa: F401  (pair is a fixture)


async def add_position(session: AsyncSession, user, month: int) -> PlanPosition:
    plan = Plan(user_id=user.id, year=2026, month=month)
    session.add(plan)
    await session.flush()
    position = PlanPosition(
        plan_id=plan.id,
        label=f"Position {month}",
        amount_planned=Decimal("10.00"),
        category=Category.LEISURE_SUBSCRIPTIONS,
        budget=Budget.WANTS,
        due_day=15,
    )
    session.add(position)
    await session.flush()
    return position


async def memberships_of(session: AsyncSession, user_id) -> list[HouseholdMember]:
    session.expire_all()
    return list(
        await session.scalars(select(HouseholdMember).where(HouseholdMember.user_id == user_id))
    )


async def test_leaving_gives_the_leaver_a_new_household_of_their_own(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
) -> None:
    _, helper, household = pair
    household_id, helper_id = household.id, helper.id
    sign_in(helper)

    response = await client.delete(f"/api/v1/households/{household_id}/members/me")

    assert response.status_code == 204
    [membership] = await memberships_of(session, helper_id)
    assert membership.household_id != household_id
    assert membership.role is Role.ADMIN
    own = await session.get(Household, membership.household_id)
    assert own.name == "Haushalt von Helper"


async def test_the_others_stay_in_the_household_they_were_in(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
) -> None:
    owner, helper, household = pair
    household_id, owner_id = household.id, owner.id
    sign_in(helper)

    await client.delete(f"/api/v1/households/{household_id}/members/me")

    [membership] = await memberships_of(session, owner_id)
    assert membership.household_id == household_id


async def test_leaving_keeps_the_positions_in_the_own_plan(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
) -> None:
    _, helper, household = pair
    household_id = household.id
    position = await add_position(session, helper, 9)
    position_id = position.id
    await session.commit()
    sign_in(helper)

    await client.delete(f"/api/v1/households/{household_id}/members/me")

    session.expire_all()
    kept = await session.get(PlanPosition, position_id)
    assert kept is not None
    assert kept.label == "Position 9"
    assert kept.is_private is False


async def test_the_household_no_longer_shows_the_leavers_positions_in_any_month(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
) -> None:
    owner, helper, household = pair
    household_id = household.id
    await add_position(session, owner, 9)
    helper_plan = Plan(user_id=helper.id, year=2026, month=9)
    session.add(helper_plan)
    await session.flush()
    session.add(
        PlanPosition(
            plan_id=helper_plan.id,
            label="Helper position",
            amount_planned=Decimal("10.00"),
            category=Category.LEISURE_SUBSCRIPTIONS,
            budget=Budget.WANTS,
            due_day=15,
        )
    )
    await session.commit()
    sign_in(helper)
    await client.delete(f"/api/v1/households/{household_id}/members/me")

    sign_in(owner)
    response = await client.get(f"/api/v1/plans/2026/9?household={household_id}")

    assert response.status_code == 200
    assert [p["label"] for p in response.json()["positions"]] == ["Position 9"]


async def test_the_only_owner_cannot_leave_and_stays_a_member(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
) -> None:
    owner, _, household = pair
    household_id, owner_id = household.id, owner.id
    member = await session.scalar(
        select(HouseholdMember).where(
            HouseholdMember.household_id == household_id,
            HouseholdMember.user_id == owner.id,
        )
    )
    member.role = Role.ADMIN
    await session.commit()
    sign_in(owner)

    response = await client.delete(f"/api/v1/households/{household_id}/members/me")

    assert response.status_code == 409
    assert response.json()["detail"]["code"] == "last_admin_cannot_leave"
    [membership] = await memberships_of(session, owner_id)
    assert membership.household_id == household_id


async def test_a_month_created_after_leaving_keeps_the_contracts_flag(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
) -> None:
    _, helper, household = pair
    household_id = household.id
    commitment = await session.scalar(select(Commitment).where(Commitment.owner_id == helper.id))
    commitment.is_private = True
    commitment_id = commitment.id
    await session.commit()
    sign_in(helper)

    await client.delete(f"/api/v1/households/{household_id}/members/me")
    created = await client.post("/api/v1/plans", json={"year": 2026, "month": 12})

    assert created.status_code == 201
    session.expire_all()
    positions = (
        await session.scalars(
            select(PlanPosition.is_private).where(PlanPosition.commitment_id == commitment_id)
        )
    ).all()
    assert positions and all(positions)
