import uuid
from datetime import datetime
from decimal import Decimal

from pydantic import EmailStr, Field

from app.models.enums import InvitationStatus, Role
from app.schemas.base import Schema


class MemberRead(Schema):
    """Ein Mitglied samt Namen — die Liste zeigt Personen, nicht IDs."""

    user_id: uuid.UUID
    first_name: str
    last_name: str
    email: EmailStr
    role: Role


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
    """Alles optional — was nicht mitkommt, bleibt wie es war."""

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

    #: Nur für die einladende Seite — daraus baut das Frontend den Link.
    token: str


class InvitationCreate(Schema):
    email: EmailStr


class MyInvitationRead(Schema):
    """Eine offene Einladung an die eigene Adresse — der Posteingang.

    Damit braucht es keinen Link und keine E-Mail: wer sich mit der
    eingeladenen Adresse anmeldet, findet die Einladung im Portal.
    """

    token: str
    household_id: uuid.UUID
    household_name: str
    invited_by: str
    expires_at: datetime


class InvitationPreview(Schema):
    """Was jemand sieht, der auf einen Einladungslink klickt.

    Bewusst sparsam: wer den Link hat, muss den Haushaltsnamen sehen dürfen,
    aber nicht die Mitgliederliste.
    """

    household_name: str
    invited_by: str
    expires_at: datetime
