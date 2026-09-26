"""Nobody sets the grants of somebody else — each person grants their own data."""

from httpx import AsyncClient
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.enums import AccessLevel
from app.models.household import HouseholdMember
from tests.test_delegation import pair, sign_in  # noqa: F401  (pair is a fixture)


async def grants_of(session: AsyncSession, household_id, user_id) -> tuple[AccessLevel, ...]:
    session.expire_all()
    member = await session.scalar(
        select(HouseholdMember).where(
            HouseholdMember.household_id == household_id,
            HouseholdMember.user_id == user_id,
        )
    )
    return member.grants_plan, member.grants_commitments, member.grants_accounts


async def test_setting_grants_changes_only_my_own_membership(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
) -> None:
    owner, helper, household = pair
    owner_id, helper_id, household_id = owner.id, helper.id, household.id
    sign_in(helper)

    response = await client.patch(
        f"/api/v1/households/{household_id}/members/me",
        json={"grants_plan": "edit", "grants_commitments": "edit", "grants_accounts": "edit"},
    )

    assert response.status_code == 200
    assert await grants_of(session, household_id, helper_id) == (AccessLevel.EDIT,) * 3
    assert await grants_of(session, household_id, owner_id) == (AccessLevel.PLAN,) * 3


async def test_no_endpoint_sets_the_grants_of_another_member(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
) -> None:
    owner, helper, household = pair
    owner_id, household_id = owner.id, household.id
    sign_in(helper)

    for method in ("patch", "put", "post"):
        response = await getattr(client, method)(
            f"/api/v1/households/{household_id}/members/{owner_id}",
            json={"grants_plan": "edit"},
        )
        assert response.status_code in (404, 405)

    assert await grants_of(session, household_id, owner_id) == (AccessLevel.PLAN,) * 3
