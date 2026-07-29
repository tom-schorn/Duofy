"""Haushalte, Mitglieder und Einladungen.

Der Haushalt besitzt nichts — er sagt nur, wer zusammen plant. Deshalb gibt es
hier keine Beträge, nur Personen und Quoten.
"""

import uuid
from datetime import UTC, datetime

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.orm import selectinload

from app.core.auth import current_active_user
from app.core.permissions import is_household_owner, is_member, require
from app.db.session import get_session
from app.models.enums import InvitationStatus, Role
from app.models.household import Household, HouseholdInvitation, HouseholdMember
from app.models.user import User
from app.schemas.household import (
    HouseholdCreate,
    HouseholdRead,
    HouseholdUpdate,
    InvitationCreate,
    InvitationPreview,
    InvitationRead,
    MemberRead,
)

router = APIRouter()


async def _load(session: AsyncSession, household_id: uuid.UUID) -> Household:
    household = await session.get(
        Household,
        household_id,
        options=[selectinload(Household.members)],
    )
    if household is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "household_not_found"})
    return household


async def _to_read(session: AsyncSession, household: Household) -> HouseholdRead:
    """Mitglieder mit Namen anreichern — die Liste zeigt Personen, nicht IDs."""
    user_ids = [member.user_id for member in household.members]
    users = {}
    if user_ids:
        result = await session.execute(select(User).where(User.id.in_(user_ids)))
        users = {user.id: user for user in result.scalars()}

    return HouseholdRead(
        id=household.id,
        name=household.name,
        target_needs=household.target_needs,
        target_wants=household.target_wants,
        target_savings=household.target_savings,
        buffer_percent=household.buffer_percent,
        members=[
            MemberRead(
                user_id=member.user_id,
                first_name=users[member.user_id].first_name,
                last_name=users[member.user_id].last_name,
                email=users[member.user_id].email,
                role=member.role,
            )
            for member in household.members
            if member.user_id in users
        ],
    )


@router.get("", response_model=list[HouseholdRead])
async def list_households(
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> list[HouseholdRead]:
    """Alle Haushalte, in denen der Nutzer Mitglied ist.

    Mehrere sind vorgesehen — WG und Partnerin gleichzeitig.
    """
    result = await session.execute(
        select(Household)
        .join(HouseholdMember)
        .where(HouseholdMember.user_id == user.id)
        .options(selectinload(Household.members))
        .order_by(Household.created_at)
    )
    return [await _to_read(session, household) for household in result.scalars().unique()]


@router.post("", response_model=HouseholdRead, status_code=status.HTTP_201_CREATED)
async def create_household(
    payload: HouseholdCreate,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> HouseholdRead:
    """Wer anlegt, wird Besitzer."""
    household = Household(name=payload.name)
    household.members.append(HouseholdMember(user_id=user.id, role=Role.OWNER))

    session.add(household)
    await session.commit()
    await session.refresh(household, ["members"])

    return await _to_read(session, household)


@router.patch("/{household_id}", response_model=HouseholdRead)
async def update_household(
    household_id: uuid.UUID,
    payload: HouseholdUpdate,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> HouseholdRead:
    """Umbenennen und Quoten ändern — nur der Besitzer."""
    household = await _load(session, household_id)
    require(await is_household_owner(session, user.id, household_id), "not_household_owner")

    for field, value in payload.model_dump(exclude_unset=True).items():
        setattr(household, field, value)

    await session.commit()
    await session.refresh(household, ["members"])
    return await _to_read(session, household)


@router.delete("/{household_id}/members/me", status_code=status.HTTP_204_NO_CONTENT)
async def leave_household(
    household_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> None:
    """Austreten.

    Die eingebrachten Posten bleiben bestehen und werden wieder privat —
    `household_id` steht auf ON DELETE SET NULL. Das Frontend sagt das im
    Bestätigungsdialog.
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

    # Der letzte Besitzer darf nicht gehen — sonst verwaist der Haushalt.
    if member.role is Role.OWNER:
        owners = await session.execute(
            select(HouseholdMember).where(
                HouseholdMember.household_id == household_id,
                HouseholdMember.role == Role.OWNER,
            )
        )
        if len(owners.scalars().all()) == 1:
            raise HTTPException(
                status.HTTP_409_CONFLICT, detail={"code": "last_owner_cannot_leave"}
            )

    await session.delete(member)
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
    """Jemanden einladen — auch wenn er noch kein Konto hat.

    Die Einladung geht an eine E-Mail, nicht an einen Nutzer. Wer den Link
    öffnet, registriert sich und tritt damit bei.

    TODO: E-Mail tatsächlich verschicken. Bis dahin liefert die Antwort den
    Token, damit der Link von Hand weitergegeben werden kann.
    """
    await _load(session, household_id)
    require(await is_household_owner(session, user.id, household_id), "not_household_owner")

    email = payload.email.lower()

    # Wer schon Mitglied ist, braucht keine Einladung.
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
    require(await is_household_owner(session, user.id, household_id), "not_household_owner")

    invitation = await session.get(HouseholdInvitation, invitation_id)
    if invitation is None or invitation.household_id != household_id:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "invitation_not_found"})

    invitation.status = InvitationStatus.REVOKED
    await session.commit()


@router.get("/invitations/{token}", response_model=InvitationPreview)
async def preview_invitation(
    token: str,
    session: AsyncSession = Depends(get_session),
) -> InvitationPreview:
    """Was hinter dem Einladungslink steckt — ohne Anmeldung abrufbar.

    Bewusst sparsam: Haushaltsname und wer eingeladen hat, mehr nicht. Wer den
    Link hat, soll nicht die Mitgliederliste sehen.
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
    """Einladung annehmen — daraus entsteht die Mitgliedschaft."""
    invitation = await _open_invitation(session, token)

    # Die Einladung gilt der Adresse, nicht dem Konto. Sonst könnte jeder mit
    # dem Link beitreten, der ihn irgendwo aufschnappt.
    if user.email.lower() != invitation.email:
        raise HTTPException(status.HTTP_403_FORBIDDEN, detail={"code": "invitation_email_mismatch"})

    if not await is_member(session, user.id, invitation.household_id):
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
    return await _to_read(session, household)


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
    """Holt eine Einladung, die noch angenommen werden kann."""
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
