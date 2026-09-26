"""Leaving a household detaches the leaver's positions from it (#54).

The household plan is a lens over the positions that carry `household_id`. Leaving
used to delete only the membership, so the positions kept pointing at a household
their owner no longer belonged to — and came back on re-joining.
"""

from decimal import Decimal

from httpx import AsyncClient
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.enums import Budget, Category, Role
from app.models.household import HouseholdMember
from app.models.plan import Plan, PlanPosition
from tests.test_area_permissions import make_household
from tests.test_delegation import pair, sign_in  # noqa: F401  (pair is a fixture)


async def add_position(session: AsyncSession, user, month: int, household_id) -> PlanPosition:
    plan = Plan(user_id=user.id, year=2026, month=month)
    session.add(plan)
    await session.flush()
    position = PlanPosition(
        plan_id=plan.id,
        household_id=household_id,
        label=f"Position {month}",
        amount_planned=Decimal("10.00"),
        category=Category.LEISURE_SUBSCRIPTIONS,
        budget=Budget.WANTS,
        due_day=15,
    )
    session.add(position)
    await session.flush()
    return position


async def household_of(session: AsyncSession, position_id):
    session.expire_all()
    return await session.scalar(
        select(PlanPosition.household_id).where(PlanPosition.id == position_id)
    )


async def test_leaving_detaches_the_positions_of_every_month(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
) -> None:
    owner, helper, household = pair
    household_id = household.id
    past = await add_position(session, helper, 3, household_id)
    future = await add_position(session, helper, 11, household_id)
    past_id, future_id = past.id, future.id
    await session.commit()
    sign_in(helper)

    response = await client.delete(f"/api/v1/households/{household_id}/members/me")

    assert response.status_code == 204
    assert await household_of(session, past_id) is None
    assert await household_of(session, future_id) is None


async def test_leaving_keeps_the_positions_in_the_own_plan(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
) -> None:
    _, helper, household = pair
    household_id = household.id
    position = await add_position(session, helper, 9, household_id)
    position_id = position.id
    await session.commit()
    sign_in(helper)

    await client.delete(f"/api/v1/households/{household_id}/members/me")

    session.expire_all()
    kept = await session.get(PlanPosition, position_id)
    assert kept is not None
    assert kept.label == "Position 9"


async def test_positions_in_another_household_stay_where_they_are(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
) -> None:
    _, helper, household = pair
    household_id = household.id
    other = await make_household(session, "Other")
    other_id = other.id
    mine = await add_position(session, helper, 9, household_id)
    elsewhere = await add_position(session, helper, 10, other_id)
    mine_id, elsewhere_id = mine.id, elsewhere.id
    await session.commit()
    sign_in(helper)

    await client.delete(f"/api/v1/households/{household_id}/members/me")

    assert await household_of(session, mine_id) is None
    assert await household_of(session, elsewhere_id) == other_id


async def test_the_positions_of_the_others_stay_in_the_household(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
) -> None:
    owner, helper, household = pair
    household_id = household.id
    theirs = await add_position(session, owner, 9, household_id)
    theirs_id = theirs.id
    await session.commit()
    sign_in(helper)

    await client.delete(f"/api/v1/households/{household_id}/members/me")

    assert await household_of(session, theirs_id) == household_id


async def test_the_only_owner_cannot_leave_and_keeps_the_positions(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
) -> None:
    owner, _, household = pair
    household_id = household.id
    position = await add_position(session, owner, 9, household_id)
    position_id = position.id
    member = await session.scalar(
        select(HouseholdMember).where(
            HouseholdMember.household_id == household_id,
            HouseholdMember.user_id == owner.id,
        )
    )
    member.role = Role.OWNER
    await session.commit()
    sign_in(owner)

    response = await client.delete(f"/api/v1/households/{household_id}/members/me")

    assert response.status_code == 409
    assert response.json()["detail"]["code"] == "last_owner_cannot_leave"
    assert await household_of(session, position_id) == household_id
