"""The system level: invitations onto the instance.

Every route here needs the admin role (`require_admin`). What an admin may do is
**run the instance**. What they may not do is look into it: nothing in this module,
or anywhere else, gives them plans, books or accounts of other people. That only
happens through household grants (#91).
"""

import logging
import uuid
from datetime import UTC, datetime

from fastapi import APIRouter, Depends, HTTPException, Response, status
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.auth import require_admin
from app.db.session import get_session
from app.models.instance_invitation import INSTANCE_INVITATION_LIFETIME, InstanceInvitation
from app.models.user import User
from app.schemas.admin import InstanceInvitationCreate, InstanceInvitationRead

logger = logging.getLogger(__name__)

router = APIRouter(dependencies=[Depends(require_admin)])


@router.get("/invitations", response_model=list[InstanceInvitationRead])
async def list_invitations(session: AsyncSession = Depends(get_session)) -> list:
    """Open invitations: not used, not expired. Newest first."""
    result = await session.scalars(
        select(InstanceInvitation)
        .where(InstanceInvitation.used_at.is_(None))
        .where(InstanceInvitation.expires_at > datetime.now(UTC))
        .order_by(InstanceInvitation.created_at.desc())
    )
    return list(result)


@router.post(
    "/invitations", response_model=InstanceInvitationRead, status_code=status.HTTP_201_CREATED
)
async def create_invitation(
    body: InstanceInvitationCreate,
    admin: User = Depends(require_admin),
    session: AsyncSession = Depends(get_session),
) -> InstanceInvitation:
    """Make an invitation. Valid for seven days, usable once."""
    # Both stamps from one clock reading, so that the seven days are exact.
    now = datetime.now(UTC)
    invitation = InstanceInvitation(
        created_by_id=admin.id,
        email=body.email.lower() if body.email else None,
        created_at=now,
        expires_at=now + INSTANCE_INVITATION_LIFETIME,
    )
    session.add(invitation)
    await session.commit()
    await session.refresh(invitation)
    logger.info("Instance invitation created")
    return invitation


@router.delete("/invitations/{invitation_id}", status_code=status.HTTP_204_NO_CONTENT)
async def revoke_invitation(
    invitation_id: uuid.UUID, session: AsyncSession = Depends(get_session)
) -> Response:
    """Revoke: the row is deleted, so the link stops working at once."""
    invitation = await session.get(InstanceInvitation, invitation_id)
    if invitation is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "invitation_not_found"})
    await session.delete(invitation)
    await session.commit()
    return Response(status_code=status.HTTP_204_NO_CONTENT)
