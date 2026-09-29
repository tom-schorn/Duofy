"""Households, members and invitations.

A household owns nothing — it only says who plans together. That is why there are
no amounts here, only people and quotas.
"""

import uuid
from datetime import UTC, datetime

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.orm import selectinload

from app.core.auth import current_active_user
from app.core.permissions import is_household_admin, is_member, require
from app.db.session import get_session
from app.models.enums import AccessLevel, Area, InvitationStatus, Role
from app.models.household import Household, HouseholdInvitation, HouseholdMember
from app.models.user import User
from app.schemas.household import (
    HouseholdRead,
    HouseholdUpdate,
    InvitationCreate,
    InvitationPreview,
    InvitationRead,
    MemberRead,
    MyInvitationRead,
)
from app.services.grants import drop_all_of, levels_with, no_levels, set_levels
from app.services.households import create_own_household

router = APIRouter()


async def _load(session: AsyncSession, household_id: uuid.UUID) -> Household:
    household = await session.get(
        Household,
        household_id,
        options=[selectinload(Household.members)],
        # A membership may have just changed in this session; read the row again.
        populate_existing=True,
    )
    if household is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "household_not_found"})
    return household


async def _admin_count(session: AsyncSession, household_id: uuid.UUID) -> int:
    """How many admins a household has. At least one must always stay (decision 68)."""
    return await session.scalar(
        select(func.count())
        .select_from(HouseholdMember)
        .where(HouseholdMember.household_id == household_id, HouseholdMember.role == Role.ADMIN)
    )


async def _to_read(
    session: AsyncSession, household: Household, viewer_id: uuid.UUID
) -> HouseholdRead:
    """Enrich members with their names and the grants between them and the viewer.

    Only the grants that touch the viewer: what they gave each member and what each
    member gave them. How two other members trust each other is none of their
    business.
    """
    user_ids = [member.user_id for member in household.members]
    users = {}
    if user_ids:
        result = await session.execute(select(User).where(User.id.in_(user_ids)))
        users = {user.id: user for user in result.scalars()}
    given, received = await levels_with(session, viewer_id, user_ids)

    return HouseholdRead(
        id=household.id,
        name=household.name,
        target_needs=household.target_needs,
        target_wants=household.target_wants,
        target_savings=household.target_savings,
        members=[
            _member_read(member, users[member.user_id], given, received)
            for member in household.members
            if member.user_id in users
        ],
    )


def _member_read(
    member: HouseholdMember,
    user: User,
    given: dict[uuid.UUID, dict[Area, AccessLevel]],
    received: dict[uuid.UUID, dict[Area, AccessLevel]],
) -> MemberRead:
    return MemberRead(
        user_id=member.user_id,
        first_name=user.first_name,
        last_name=user.last_name,
        email=user.email,
        role=member.role,
        grants_to_me=received.get(member.user_id, no_levels()),
        my_grants=given.get(member.user_id, no_levels()),
    )


@router.get("", response_model=list[HouseholdRead])
async def list_households(
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> list[HouseholdRead]:
    """The household the user is a member of.

    A list for the frontend's sake; it holds exactly one household.
    """
    result = await session.execute(
        select(Household)
        .join(HouseholdMember)
        .where(HouseholdMember.user_id == user.id)
        .options(selectinload(Household.members))
        .order_by(Household.created_at)
    )
    return [await _to_read(session, household, user.id) for household in result.scalars().unique()]


@router.patch("/{household_id}", response_model=HouseholdRead)
async def update_household(
    household_id: uuid.UUID,
    payload: HouseholdUpdate,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> HouseholdRead:
    """Rename and change the household quotas — admins only (decisions 58, 60).

    Every person keeps their own quotas; these are the household's.
    """
    household = await _load(session, household_id)
    changes = payload.model_dump(exclude_unset=True)
    require(await is_household_admin(session, user.id, household_id), "not_household_admin")

    for field, value in changes.items():
        setattr(household, field, value)

    await session.commit()
    await session.refresh(household, ["members"])
    return await _to_read(session, household, user.id)


@router.put("/{household_id}/grants/{grantee_id}", response_model=MemberRead)
async def set_grants(
    household_id: uuid.UUID,
    grantee_id: uuid.UUID,
    payload: dict[Area, AccessLevel],
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> MemberRead:
    """Set what `grantee` may do with **your** data, per area (decision 57).

    The granter is always the caller — there is no parameter for it, so nobody can
    set rights on somebody else's data, not even an admin (decision 58). Only the
    areas sent change; `none` takes a right away.
    """
    require(await is_member(session, user.id, household_id), "not_household_member")
    if grantee_id == user.id:
        raise HTTPException(
            status.HTTP_422_UNPROCESSABLE_CONTENT, detail={"code": "cannot_grant_self"}
        )
    member = await session.scalar(
        select(HouseholdMember).where(
            HouseholdMember.household_id == household_id,
            HouseholdMember.user_id == grantee_id,
        )
    )
    if member is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "not_a_member"})

    await set_levels(session, user.id, grantee_id, payload)
    await session.commit()

    grantee = await session.get(User, grantee_id)
    given, received = await levels_with(session, user.id, [grantee_id])
    return _member_read(member, grantee, given, received)


@router.delete("/{household_id}/members/me", status_code=status.HTTP_204_NO_CONTENT)
async def leave_household(
    household_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> None:
    """Austreten.

    Wer austritt, sieht die gemeinsamen Pläne des Haushalts nicht mehr, und die
    anderen sehen seine Posten dort in keinem Monat mehr, auch nicht in vergangenen:
    die gemeinsame Sicht wird aus den Plänen der *aktuellen* Mitglieder
    zusammengesetzt. Gelöscht wird nichts. Die eigenen Pläne, Konten und Verträge
    bleiben bei der Person.

    Wer austritt, bekommt sofort einen neuen, eigenen Haushalt: jede Person gehört zu
    genau einem. Posten und Verträge bleiben unverändert; weil sie am Haushalt der
    Person hängen, sehen die bisherigen Mitglieder sie nicht mehr.
    """
    result = await session.execute(
        select(HouseholdMember).where(
            HouseholdMember.household_id == household_id,
            HouseholdMember.user_id == user.id,
        )
    )
    member = result.scalar_one_or_none()
    if member is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "not_a_member"})

    # The last admin may not leave — nobody could invite or remove anymore.
    if member.role is Role.ADMIN and await _admin_count(session, household_id) == 1:
        raise HTTPException(status.HTTP_409_CONFLICT, detail={"code": "last_admin_cannot_leave"})

    await session.delete(member)
    await drop_all_of(session, user.id)
    await session.flush()
    await create_own_household(session, user)
    await session.commit()


# --- Einladungen ----------------------------------------------------------


@router.get("/{household_id}/invitations", response_model=list[InvitationRead])
async def list_invitations(
    household_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> list[HouseholdInvitation]:
    require(await is_member(session, user.id, household_id), "not_household_member")

    result = await session.execute(
        select(HouseholdInvitation)
        .where(
            HouseholdInvitation.household_id == household_id,
            HouseholdInvitation.status == InvitationStatus.PENDING,
        )
        .order_by(HouseholdInvitation.created_at.desc())
    )
    return list(result.scalars())


@router.post(
    "/{household_id}/invitations",
    response_model=InvitationRead,
    status_code=status.HTTP_201_CREATED,
)
async def invite(
    household_id: uuid.UUID,
    payload: InvitationCreate,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> HouseholdInvitation:
    """Invite somebody, even if they have no account yet.

    The invitation targets an email address, not a user. Whoever signs in with that
    address finds it waiting for them.

    TODO: actually send an email. Until then the response carries the token so the
    link can be passed on by hand.
    """
    await _load(session, household_id)
    require(await is_household_admin(session, user.id, household_id), "not_household_admin")

    email = payload.email.lower()

    # Somebody who is already a member does not need an invitation.
    existing_member = await session.execute(
        select(HouseholdMember)
        .join(User, User.id == HouseholdMember.user_id)
        .where(HouseholdMember.household_id == household_id, User.email == email)
    )
    if existing_member.scalar_one_or_none() is not None:
        raise HTTPException(status.HTTP_409_CONFLICT, detail={"code": "already_a_member"})

    open_invite = await session.execute(
        select(HouseholdInvitation).where(
            HouseholdInvitation.household_id == household_id,
            HouseholdInvitation.email == email,
            HouseholdInvitation.status == InvitationStatus.PENDING,
        )
    )
    if open_invite.scalar_one_or_none() is not None:
        raise HTTPException(status.HTTP_409_CONFLICT, detail={"code": "invitation_already_open"})

    invitation = HouseholdInvitation(
        household_id=household_id,
        invited_by_id=user.id,
        email=email,
    )
    session.add(invitation)
    await session.commit()
    await session.refresh(invitation)
    return invitation


@router.delete(
    "/{household_id}/invitations/{invitation_id}", status_code=status.HTTP_204_NO_CONTENT
)
async def revoke_invitation(
    household_id: uuid.UUID,
    invitation_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> None:
    require(await is_household_admin(session, user.id, household_id), "not_household_admin")

    invitation = await session.get(HouseholdInvitation, invitation_id)
    if invitation is None or invitation.household_id != household_id:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "invitation_not_found"})

    invitation.status = InvitationStatus.REVOKED
    await session.commit()


@router.get("/invitations", response_model=list[MyInvitationRead])
async def my_invitations(
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> list[MyInvitationRead]:
    """Pending invitations addressed to you.

    The inbox: whoever signs in with the invited address finds the invitation in
    the app — no link, no email.

    Must be declared **before** `/invitations/{token}`, otherwise the path parameter
    swallows any literal segment declared after it.
    """
    result = await session.execute(
        select(HouseholdInvitation, Household.name, User.first_name)
        .join(Household, Household.id == HouseholdInvitation.household_id)
        .join(User, User.id == HouseholdInvitation.invited_by_id)
        .where(
            HouseholdInvitation.email == user.email.lower(),
            HouseholdInvitation.status == InvitationStatus.PENDING,
            HouseholdInvitation.expires_at > datetime.now(UTC),
        )
        .order_by(HouseholdInvitation.created_at.desc())
    )

    return [
        MyInvitationRead(
            token=invitation.token,
            household_id=invitation.household_id,
            household_name=household_name,
            invited_by=invited_by,
            expires_at=invitation.expires_at,
        )
        for invitation, household_name, invited_by in result.all()
    ]


@router.get("/invitations/{token}", response_model=InvitationPreview)
async def preview_invitation(
    token: str,
    session: AsyncSession = Depends(get_session),
) -> InvitationPreview:
    """What is behind an invitation link — readable without signing in.

    Deliberately sparse: the household name and who invited, nothing else. Holding
    the link must not reveal the member list.
    """
    invitation = await _open_invitation(session, token)

    household = await session.get(Household, invitation.household_id)
    inviter = await session.get(User, invitation.invited_by_id)

    return InvitationPreview(
        household_name=household.name if household else "",
        invited_by=f"{inviter.first_name} {inviter.last_name}" if inviter else "",
        expires_at=invitation.expires_at,
    )


@router.post("/invitations/{token}/accept", response_model=HouseholdRead)
async def accept_invitation(
    token: str,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> HouseholdRead:
    """Accept an invitation — this is what creates the membership."""
    invitation = await _open_invitation(session, token)

    # The invitation belongs to the address, not to whoever holds the link —
    # otherwise anyone who picked it up somewhere could join.
    if user.email.lower() != invitation.email:
        raise HTTPException(status.HTTP_403_FORBIDDEN, detail={"code": "invitation_email_mismatch"})

    if not await is_member(session, user.id, invitation.household_id):
        # Everybody is in exactly one household. Moving is fine while the own one
        # is empty apart from oneself — it goes away. With other people in it
        # somebody would be left behind, so that is a refusal, not a merge.
        own = await session.scalar(
            select(HouseholdMember).where(HouseholdMember.user_id == user.id)
        )
        if own is not None:
            others = await session.scalar(
                select(func.count())
                .select_from(HouseholdMember)
                .where(
                    HouseholdMember.household_id == own.household_id,
                    HouseholdMember.user_id != user.id,
                )
            )
            if others:
                raise HTTPException(
                    status.HTTP_409_CONFLICT, detail={"code": "household_not_empty"}
                )
            # Members and invitations are loaded up front: the ORM cascade cannot
            # lazy-load them inside an async session.
            own_household = await session.get(
                Household,
                own.household_id,
                options=[selectinload(Household.members), selectinload(Household.invitations)],
            )
            await session.delete(own_household)
            await session.flush()
        session.add(
            HouseholdMember(
                household_id=invitation.household_id,
                user_id=user.id,
                role=Role.MEMBER,
            )
        )

    invitation.status = InvitationStatus.ACCEPTED
    await session.commit()

    household = await _load(session, invitation.household_id)
    return await _to_read(session, household, user.id)


@router.post("/invitations/{token}/decline", status_code=status.HTTP_204_NO_CONTENT)
async def decline_invitation(
    token: str,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> None:
    invitation = await _open_invitation(session, token)
    if user.email.lower() != invitation.email:
        raise HTTPException(status.HTTP_403_FORBIDDEN, detail={"code": "invitation_email_mismatch"})

    invitation.status = InvitationStatus.DECLINED
    await session.commit()


async def _open_invitation(session: AsyncSession, token: str) -> HouseholdInvitation:
    """Fetch an invitation that can still be accepted."""
    result = await session.execute(
        select(HouseholdInvitation).where(HouseholdInvitation.token == token)
    )
    invitation = result.scalar_one_or_none()

    if invitation is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "invitation_not_found"})

    if invitation.status is not InvitationStatus.PENDING:
        raise HTTPException(status.HTTP_409_CONFLICT, detail={"code": "invitation_not_open"})

    if invitation.expires_at <= datetime.now(UTC):
        raise HTTPException(status.HTTP_409_CONFLICT, detail={"code": "invitation_expired"})

    return invitation
