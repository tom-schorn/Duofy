"""Each person grants their own data, to each other member separately (decision 57)."""

from httpx import AsyncClient
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.permissions import Area, granted_level
from app.models.enums import AccessLevel
from app.models.grant import Grant
from tests.test_area_permissions import add_member, make_household, make_user
from tests.test_delegation import pair, sign_in  # noqa: F401  (pair is a fixture)


async def rows(session: AsyncSession) -> set[tuple]:
    session.expire_all()
    result = await session.execute(select(Grant))
    return {(g.granter_id, g.grantee_id, g.area, g.level) for g in result.scalars()}


async def test_setting_grants_writes_only_rows_where_i_am_the_granter(
    client: AsyncClient,
    session: AsyncSession,
    pair,  # noqa: F811
) -> None:
    owner, helper, household = pair
    owner_id, helper_id, household_id = owner.id, helper.id, household.id
    sign_in(helper)

    response = await client.put(
        f"/api/v1/households/{household_id}/grants/{owner_id}",
        json={"plan": "edit", "book": "create"},
    )

    assert response.status_code == 200
    assert response.json()["myGrants"] == {
        "plan": "edit",
        "book": "create",
        "accounts": "none",
        "commitments": "none",
        "import": "none",
    }
    assert await rows(session) == {
        (helper_id, owner_id, Area.PLAN, AccessLevel.EDIT),
        (helper_id, owner_id, Area.BOOK, AccessLevel.CREATE),
    }


async def test_only_the_areas_sent_change_and_none_removes_the_row(
    client: AsyncClient,
    session: AsyncSession,
    pair,  # noqa: F811
) -> None:
    owner, helper, household = pair
    owner_id, helper_id, household_id = owner.id, helper.id, household.id
    sign_in(helper)
    url = f"/api/v1/households/{household_id}/grants/{owner_id}"
    await client.put(url, json={"plan": "edit", "book": "create"})

    await client.put(url, json={"plan": "none"})

    assert await rows(session) == {(helper_id, owner_id, Area.BOOK, AccessLevel.CREATE)}


async def test_there_is_no_way_to_set_the_grants_of_another_member(
    client: AsyncClient,
    session: AsyncSession,
    pair,  # noqa: F811
) -> None:
    """The granter is always whoever is signed in; the old `/members/me` is gone."""
    owner, helper, household = pair
    owner_id, household_id = owner.id, household.id
    sign_in(helper)

    for url in (
        f"/api/v1/households/{household_id}/members/me",
        f"/api/v1/households/{household_id}/members/{owner_id}",
    ):
        for method in ("patch", "put", "post"):
            response = await getattr(client, method)(url, json={"plan": "edit"})
            assert response.status_code in (404, 405)

    assert await rows(session) == set()


async def test_nobody_grants_themselves(
    client: AsyncClient,
    session: AsyncSession,
    pair,  # noqa: F811
) -> None:
    _, helper, household = pair
    sign_in(helper)

    response = await client.put(
        f"/api/v1/households/{household.id}/grants/{helper.id}", json={"plan": "edit"}
    )

    assert response.status_code == 422
    assert response.json()["detail"] == {"code": "cannot_grant_self"}


async def test_the_grantee_has_to_be_in_the_household(
    client: AsyncClient,
    session: AsyncSession,
    pair,  # noqa: F811
) -> None:
    _, helper, household = pair
    stranger = await make_user(session, "Stranger")
    await session.commit()
    sign_in(helper)

    response = await client.put(
        f"/api/v1/households/{household.id}/grants/{stranger.id}", json={"plan": "edit"}
    )

    assert response.status_code == 404
    assert response.json()["detail"] == {"code": "not_a_member"}


async def test_only_members_set_grants_in_a_household(
    client: AsyncClient,
    session: AsyncSession,
    pair,  # noqa: F811
) -> None:
    owner, _, household = pair
    stranger = await make_user(session, "Stranger")
    await session.commit()
    sign_in(stranger)

    response = await client.put(
        f"/api/v1/households/{household.id}/grants/{owner.id}", json={"plan": "edit"}
    )

    assert response.status_code == 403
    assert response.json()["detail"] == {"code": "not_household_member"}


async def test_an_unknown_area_or_level_is_refused(
    client: AsyncClient,
    session: AsyncSession,
    pair,  # noqa: F811
) -> None:
    owner, helper, household = pair
    sign_in(helper)
    url = f"/api/v1/households/{household.id}/grants/{owner.id}"

    assert (await client.put(url, json={"garden": "edit"})).status_code == 422
    assert (await client.put(url, json={"plan": "plan"})).status_code == 422


async def test_the_household_shows_what_i_gave_and_what_i_got_per_member(
    client: AsyncClient, session: AsyncSession
) -> None:
    """Only the grants that touch the viewer — not how two others trust each other."""
    me = await make_user(session, "Me")
    partner = await make_user(session, "Partner")
    kid = await make_user(session, "Kid")
    household = await make_household(session, "Home")
    for person in (me, partner, kid):
        await add_member(session, household, person)
    session.add_all(
        [
            Grant(
                granter_id=me.id, grantee_id=partner.id, area=Area.PLAN, level=AccessLevel.DELETE
            ),
            Grant(granter_id=partner.id, grantee_id=me.id, area=Area.BOOK, level=AccessLevel.VIEW),
            Grant(granter_id=partner.id, grantee_id=kid.id, area=Area.PLAN, level=AccessLevel.EDIT),
        ]
    )
    await session.commit()
    sign_in(me)

    members = (await client.get("/api/v1/households")).json()[0]["members"]

    by_id = {member["userId"]: member for member in members}
    assert by_id[str(partner.id)]["myGrants"]["plan"] == "delete"
    assert by_id[str(partner.id)]["grantsToMe"]["book"] == "view"
    assert set(by_id[str(kid.id)]["grantsToMe"].values()) == {"none"}
    assert set(by_id[str(me.id)]["myGrants"].values()) == {"none"}


async def test_a_grant_counts_only_while_both_share_the_household(
    client: AsyncClient,
    session: AsyncSession,
    pair,  # noqa: F811
) -> None:
    """A row left behind must not open anything once the grantee is elsewhere."""
    owner, helper, household = pair
    outside = await make_user(session, "Outside")
    session.add(
        Grant(granter_id=owner.id, grantee_id=outside.id, area=Area.PLAN, level=AccessLevel.EDIT)
    )
    await session.commit()

    assert await granted_level(session, owner.id, outside.id, Area.PLAN) is AccessLevel.NONE


async def test_leaving_removes_every_grant_from_and_to_the_person(
    client: AsyncClient,
    session: AsyncSession,
    pair,  # noqa: F811
) -> None:
    owner, helper, household = pair
    helper_id, household_id = helper.id, household.id
    session.add_all(
        [
            Grant(
                granter_id=owner.id, grantee_id=helper.id, area=Area.PLAN, level=AccessLevel.EDIT
            ),
            Grant(
                granter_id=helper.id, grantee_id=owner.id, area=Area.BOOK, level=AccessLevel.VIEW
            ),
        ]
    )
    await session.commit()
    sign_in(helper)

    response = await client.delete(f"/api/v1/households/{household_id}/members/me")

    assert response.status_code == 204
    assert not {row for row in await rows(session) if helper_id in row[:2]}
