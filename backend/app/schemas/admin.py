import uuid
from datetime import datetime

from pydantic import EmailStr

from app.schemas.base import Schema


class InstanceInvitationCreate(Schema):
    #: Optional. When set, only this address can redeem the invitation.
    email: EmailStr | None = None


class InstanceInvitationRead(Schema):
    """An invitation as the admin sees it.

    The token is in here because the admin needs it to build the link. It never
    appears anywhere else in the API.
    """

    id: uuid.UUID
    token: str
    email: str | None
    created_at: datetime
    expires_at: datetime
    used_at: datetime | None
    used_by_id: uuid.UUID | None
