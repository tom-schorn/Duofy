"""What only a household admin may do (#254 step 3, decisions 58, 60 and 68).

Admins invite and revoke, rename the household, set its quotas, remove members and
hand the admin role on or take it away. A member gets `not_household_admin` for
every one of these. Several admins are equal; only the last one is protected.
"""

from decimal import Decimal

import pytest
from httpx import AsyncClient
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.enums import AccessLevel, Area, InvitationStatus, Role
from app.models.grant import Grant
from app.models.household import Household, HouseholdInvitation, HouseholdMember
from app.models.user import User
from tests.test_area_permissions import add_member, grant_to_all, make_household, make_user
from tests.test_delegation import sign_in

QUOTAS = {"targetNeeds": "65", "targetWants": "20", "targetSavings": "15"}


async def make_admin(session: AsyncSession, household: Household, user: User) -> None:
    member = await session.scalar(
        select(HouseholdMember).where(
            HouseholdMember.household_id == household.id, HouseholdMember.user_id == user.id
        )
    )
    member.role = Role.ADMIN


@pytest.fixture
async def home(session: AsyncSession):
    """A household of three: one admin and two members."""
    admin = await make_user(session, "Admin")
    member = await make_user(session, "Member")
    other = await make_user(session, "Other")
    household = await make_household(session, "Shared")
    for user in (admin, member, other):
        await add_member(session, household, user)
    await make_admin(session, household, admin)
    await session.commit()
    return admin, member, other, household


async def invitation_in(
    session: AsyncSession, household: Household, by: User
) -> HouseholdInvitation:
    invitation = HouseholdInvitation(
        household_id=household.id, invited_by_id=by.id, email="new@example.org"
    )
    session.add(invitation)
    await session.commit()
    return invitation


def refused(response) -> bool:
    return response.status_code == 403 and response.json()["detail"] == {
        "code": "not_household_admin"
    }


# --- rename, quotas, invitations -------------------------------------------


async def test_an_admin_renames_and_sets_the_household_quotas(client: AsyncClient, home):
    admin, _, _, household = home
    sign_in(admin)

    response = await client.patch(
        f"/api/v1/households/{household.id}", json={"name": "New", **QUOTAS}
    )

    assert response.status_code == 200
    assert response.json()["name"] == "New"
    assert Decimal(response.json()["targetNeeds"]) == 65


@pytest.mark.parametrize("body", [{"name": "New"}, QUOTAS], ids=["name", "quotas"])
async def test_a_member_may_neither_rename_nor_set_the_household_quotas(
    client: AsyncClient, session: AsyncSession, home, body
):
    _, member, _, household = home
    household_id = household.id
    sign_in(member)

    response = await client.patch(f"/api/v1/households/{household.id}", json=body)

    assert refused(response)
    session.expire_all()
    unchanged = await session.get(Household, household_id)
    assert unchanged.name == "Shared"
    assert unchanged.target_needs == 50


async def test_an_admin_invites_and_revokes(client: AsyncClient, session: AsyncSession, home):
    admin, _, _, household = home
    sign_in(admin)

    invited = await client.post(
        f"/api/v1/households/{household.id}/invitations", json={"email": "new@example.org"}
    )
    assert invited.status_code == 201

    revoked = await client.delete(
        f"/api/v1/households/{household.id}/invitations/{invited.json()['id']}"
    )
    assert revoked.status_code == 204


async def test_a_member_may_neither_invite_nor_revoke(
    client: AsyncClient, session: AsyncSession, home
):
    admin, member, _, household = home
    invitation = await invitation_in(session, household, admin)
    invitation_id = invitation.id
    sign_in(member)

    invited = await client.post(
        f"/api/v1/households/{household.id}/invitations", json={"email": "else@example.org"}
    )
    revoked = await client.delete(f"/api/v1/households/{household.id}/invitations/{invitation.id}")

    assert refused(invited)
    assert refused(revoked)
    session.expire_all()
    assert (await session.get(HouseholdInvitation, invitation_id)).status is (
        InvitationStatus.PENDING
    )


async def test_an_admin_of_another_household_is_a_stranger_here(
    client: AsyncClient, session: AsyncSession, home
):
    _, _, _, household = home
    stranger = await make_user(session, "Stranger")
    theirs = await make_household(session, "Theirs")
    await add_member(session, theirs, stranger)
    await make_admin(session, theirs, stranger)
    await session.commit()
    sign_in(stranger)

    response = await client.patch(f"/api/v1/households/{household.id}", json=QUOTAS)

    assert refused(response)


# --- remove a member ---------------------------------------------------------


async def membership_of(session: AsyncSession, user_id) -> HouseholdMember:
    session.expire_all()
    return await session.scalar(select(HouseholdMember).where(HouseholdMember.user_id == user_id))


async def test_an_admin_removes_a_member_who_gets_a_household_of_their_own(
    client: AsyncClient, session: AsyncSession, home
):
    admin, member, _, household = home
    household_id, member_id = household.id, member.id
    await grant_to_all(session, household, member, {Area.PLAN: AccessLevel.VIEW})
    await grant_to_all(session, household, admin, {Area.PLAN: AccessLevel.VIEW})
    await session.commit()
    sign_in(admin)

    response = await client.delete(f"/api/v1/households/{household_id}/members/{member_id}")

    assert response.status_code == 204
    moved = await membership_of(session, member_id)
    assert moved.household_id != household_id
    assert moved.role is Role.ADMIN
    left = await session.scalars(
        select(Grant).where((Grant.granter_id == member_id) | (Grant.grantee_id == member_id))
    )
    assert list(left) == []


async def test_an_admin_removes_another_admin(client: AsyncClient, session: AsyncSession, home):
    admin, member, _, household = home
    household_id, member_id = household.id, member.id
    await make_admin(session, household, member)
    await session.commit()
    sign_in(admin)

    response = await client.delete(f"/api/v1/households/{household_id}/members/{member_id}")

    assert response.status_code == 204
    assert (await membership_of(session, member_id)).household_id != household_id


async def test_a_member_may_not_remove_anybody(client: AsyncClient, session: AsyncSession, home):
    _, member, other, household = home
    household_id, other_id = household.id, other.id
    sign_in(member)

    response = await client.delete(f"/api/v1/households/{household_id}/members/{other_id}")

    assert refused(response)
    assert (await membership_of(session, other_id)).household_id == household_id


async def test_an_admin_leaves_instead_of_removing_themselves(
    client: AsyncClient, session: AsyncSession, home
):
    admin, _, _, household = home
    sign_in(admin)

    response = await client.delete(f"/api/v1/households/{household.id}/members/{admin.id}")

    assert response.status_code == 422
    assert response.json()["detail"] == {"code": "cannot_remove_self"}


async def test_removing_somebody_outside_the_household_is_not_found(
    client: AsyncClient, session: AsyncSession, home
):
    admin, _, _, household = home
    stranger = await make_user(session, "Stranger")
    await session.commit()
    sign_in(admin)

    response = await client.delete(f"/api/v1/households/{household.id}/members/{stranger.id}")

    assert response.status_code == 404
    assert response.json()["detail"] == {"code": "member_not_found"}


async def test_changing_the_role_of_somebody_outside_the_household_is_not_found(
    client: AsyncClient, session: AsyncSession, home
):
    admin, _, _, household = home
    stranger = await make_user(session, "Stranger")
    await session.commit()
    sign_in(admin)

    response = await client.patch(
        f"/api/v1/households/{household.id}/members/{stranger.id}", json={"role": "admin"}
    )

    assert response.status_code == 404
    assert response.json()["detail"] == {"code": "member_not_found"}


# --- hand the admin role on or take it away ------------------------------


async def test_an_admin_makes_a_member_admin_and_takes_it_away_again(
    client: AsyncClient, session: AsyncSession, home
):
    admin, member, _, household = home
    url = f"/api/v1/households/{household.id}/members/{member.id}"
    sign_in(admin)

    promoted = await client.patch(url, json={"role": "admin"})
    assert promoted.status_code == 200
    assert promoted.json()["role"] == "admin"

    demoted = await client.patch(url, json={"role": "member"})
    assert demoted.status_code == 200
    assert demoted.json()["role"] == "member"


async def test_a_member_may_not_make_anybody_admin(
    client: AsyncClient, session: AsyncSession, home
):
    _, member, _, household = home
    member_id = member.id
    sign_in(member)

    response = await client.patch(
        f"/api/v1/households/{household.id}/members/{member_id}", json={"role": "admin"}
    )

    assert refused(response)
    assert (await membership_of(session, member_id)).role is Role.MEMBER


async def test_an_admin_takes_the_role_from_another_admin(
    client: AsyncClient, session: AsyncSession, home
):
    admin, member, _, household = home
    await make_admin(session, household, member)
    await session.commit()
    sign_in(member)

    response = await client.patch(
        f"/api/v1/households/{household.id}/members/{admin.id}", json={"role": "member"}
    )

    assert response.status_code == 200
    assert response.json()["role"] == "member"


async def test_the_last_admin_cannot_give_up_the_role(
    client: AsyncClient, session: AsyncSession, home
):
    admin, _, _, household = home
    admin_id = admin.id
    sign_in(admin)

    response = await client.patch(
        f"/api/v1/households/{household.id}/members/{admin_id}", json={"role": "member"}
    )

    assert response.status_code == 409
    assert response.json()["detail"] == {"code": "last_admin_required"}
    assert (await membership_of(session, admin_id)).role is Role.ADMIN


async def test_passing_the_household_on_is_two_steps_and_then_leaving_works(
    client: AsyncClient, session: AsyncSession, home
):
    """#249: make somebody else admin, then step down or leave."""
    admin, member, _, household = home
    household_id, admin_id = household.id, admin.id
    sign_in(admin)

    await client.patch(
        f"/api/v1/households/{household_id}/members/{member.id}", json={"role": "admin"}
    )
    stepped_down = await client.patch(
        f"/api/v1/households/{household_id}/members/{admin_id}", json={"role": "member"}
    )
    left = await client.delete(f"/api/v1/households/{household_id}/members/me")

    assert stepped_down.status_code == 200
    assert left.status_code == 204
    assert (await membership_of(session, admin_id)).household_id != household_id


async def test_the_last_admin_cannot_leave_but_one_of_two_can(
    client: AsyncClient, session: AsyncSession, home
):
    admin, member, _, household = home
    household_id = household.id
    sign_in(admin)

    alone = await client.delete(f"/api/v1/households/{household_id}/members/me")
    assert alone.status_code == 409
    assert alone.json()["detail"] == {"code": "last_admin_cannot_leave"}

    await make_admin(session, household, member)
    await session.commit()
    with_another = await client.delete(f"/api/v1/households/{household_id}/members/me")
    assert with_another.status_code == 204


async def test_an_unknown_role_is_rejected(client: AsyncClient, session: AsyncSession, home):
    admin, member, _, household = home
    sign_in(admin)

    response = await client.patch(
        f"/api/v1/households/{household.id}/members/{member.id}", json={"role": "owner"}
    )

    assert response.status_code == 422
