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

from app.models.enums import InvitationStatus, Role
from app.models.household import Household, HouseholdInvitation, HouseholdMember
from app.models.user import User
from tests.test_area_permissions import add_member, make_household, make_user
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
