import uuid
from datetime import datetime
from decimal import Decimal

from pydantic import EmailStr, Field

from app.models.enums import AccessLevel, InvitationStatus, Role
from app.schemas.base import Schema


class MemberRead(Schema):
    """A member including their name — the list shows people, not IDs."""

    user_id: uuid.UUID
    first_name: str
    last_name: str
    email: EmailStr
    role: Role
    #: What this person allows the others to see about themselves.
    grants_access: AccessLevel


class HouseholdRead(Schema):
    id: uuid.UUID
    name: str
    target_needs: Decimal
    target_wants: Decimal
    target_savings: Decimal
    buffer_percent: Decimal
    members: list[MemberRead]


class HouseholdCreate(Schema):
    name: str = Field(min_length=1, max_length=100)


class HouseholdUpdate(Schema):
    """Everything optional — whatever is not sent stays as it was."""

    name: str | None = Field(default=None, min_length=1, max_length=100)
    target_needs: Decimal | None = Field(default=None, ge=0, le=100)
    target_wants: Decimal | None = Field(default=None, ge=0, le=100)
    target_savings: Decimal | None = Field(default=None, ge=0, le=100)
    buffer_percent: Decimal | None = Field(default=None, ge=0, le=100)


class InvitationRead(Schema):
    id: uuid.UUID
    household_id: uuid.UUID
    email: EmailStr
    status: InvitationStatus
    expires_at: datetime

    #: For the inviting side only — the frontend builds the link from it.
    token: str


class InvitationCreate(Schema):
    email: EmailStr


class MyInvitationRead(Schema):
    """A pending invitation to your own address — the inbox.

    This is why no link and no email are needed: whoever signs in with the invited
    address finds the invitation waiting in the app.
    """

    token: str
    household_id: uuid.UUID
    household_name: str
    invited_by: str
    expires_at: datetime


class InvitationPreview(Schema):
    """What someone sees who opens an invitation link.

    Deliberately sparse: holding the link is enough to be shown the household name,
    but not the member list.
    """

    household_name: str
    invited_by: str
    expires_at: datetime


class AccessUpdate(Schema):
    """Changes your own access level. Only your own."""

    grants_access: AccessLevel
