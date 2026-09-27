"""What each area shows at `view`, and what it refuses below it (#217).

`test_area_permissions.py` pins down what `granted_level()` answers.
`test_delegation.py` covers what `edit` unlocks, endpoint by endpoint.
This file is the missing third: whether the plain **list** of each area
actually reads the level and says the honest thing when it is not there —
`403 no_insight_granted`, not an empty list that looks the same as "nothing
here yet".
"""

from httpx import AsyncClient
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.enums import AccessLevel
from tests.test_area_permissions import add_member, make_household, make_user
from tests.test_delegation import grant_area, make_commitment, make_plan, sign_in


async def make_pair(session: AsyncSession):
    """Two people in one household, with nothing granted yet."""
    owner = await make_user(session, "Owner")
    helper = await make_user(session, "Helper")
    household = await make_household(session, "Shared")
    await add_member(session, household, owner)
    await add_member(session, household, helper)
    await session.commit()
    return owner, helper, household


async def test_commitments_hide_below_view_and_show_at_view(
    client: AsyncClient, session: AsyncSession
) -> None:
    owner, helper, household = await make_pair(session)
    await make_commitment(session, owner, "Owner contract")
    await session.commit()
    sign_in(helper)

    hidden = await client.get(f"/api/v1/commitments?owner={owner.id}")
    assert hidden.status_code == 403
    assert hidden.json()["detail"]["code"] == "no_insight_granted"

    await grant_area(session, household, owner, "commitments", AccessLevel.VIEW)
    shown = await client.get(f"/api/v1/commitments?owner={owner.id}")
    assert shown.status_code == 200
    assert [row["name"] for row in shown.json()] == ["Owner contract"]


async def test_accounts_hide_below_view_and_show_at_view(
    client: AsyncClient, session: AsyncSession
) -> None:
    owner, helper, household = await make_pair(session)
    sign_in(owner)
    created = await client.post(
        "/api/v1/accounts",
        json={
            "name": "Girokonto",
            "type": "checking",
            "openingBalance": "0.00",
            "openingDate": "2026-01-01",
        },
    )
    assert created.status_code == 201
    sign_in(helper)

    hidden = await client.get(f"/api/v1/accounts?owner={owner.id}")
    assert hidden.status_code == 403
    assert hidden.json()["detail"]["code"] == "no_insight_granted"

    await grant_area(session, household, owner, "accounts", AccessLevel.VIEW)
    shown = await client.get(f"/api/v1/accounts?owner={owner.id}")
    assert shown.status_code == 200
    assert [row["name"] for row in shown.json()] == ["Girokonto"]


async def test_the_book_hides_below_view_and_shows_at_view(
    client: AsyncClient, session: AsyncSession
) -> None:
    """The book hangs off `Area.ACCOUNTS`, same as accounts themselves."""
    owner, helper, household = await make_pair(session)
    sign_in(helper)

    hidden = await client.get(f"/api/v1/transactions?owner={owner.id}")
    assert hidden.status_code == 403
    assert hidden.json()["detail"]["code"] == "no_insight_granted"

    await grant_area(session, household, owner, "accounts", AccessLevel.VIEW)
    shown = await client.get(f"/api/v1/transactions?owner={owner.id}")
    assert shown.status_code == 200
    assert shown.json() == []


async def test_import_hides_below_view_and_shows_at_view(
    client: AsyncClient, session: AsyncSession
) -> None:
    """The parking area hangs off `Area.ACCOUNTS` too, like the book."""
    owner, helper, household = await make_pair(session)
    sign_in(helper)

    hidden = await client.get(f"/api/v1/imports?owner={owner.id}")
    assert hidden.status_code == 403
    assert hidden.json()["detail"]["code"] == "no_insight_granted"

    await grant_area(session, household, owner, "accounts", AccessLevel.VIEW)
    shown = await client.get(f"/api/v1/imports?owner={owner.id}")
    assert shown.status_code == 200
    assert shown.json() == []


async def test_plan_hides_below_view_and_shows_at_view(
    client: AsyncClient, session: AsyncSession
) -> None:
    owner, helper, household = await make_pair(session)
    plan = await make_plan(session, owner)
    sign_in(helper)

    hidden_month = await client.get(f"/api/v1/plans/{plan.year}/{plan.month}?owner={owner.id}")
    assert hidden_month.status_code == 403
    assert hidden_month.json()["detail"]["code"] == "no_insight_granted"

    hidden_list = await client.get(f"/api/v1/plans?owner={owner.id}")
    assert hidden_list.status_code == 403
    assert hidden_list.json()["detail"]["code"] == "no_insight_granted"

    await grant_area(session, household, owner, "plan", AccessLevel.VIEW)
    shown_month = await client.get(f"/api/v1/plans/{plan.year}/{plan.month}?owner={owner.id}")
    assert shown_month.status_code == 200

    shown_list = await client.get(f"/api/v1/plans?owner={owner.id}")
    assert shown_list.status_code == 200
    assert len(shown_list.json()) == 1
