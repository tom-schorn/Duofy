"""Wer darf was.

Kein Rollen-Framework — vier Regeln, die von den Endpunkten aufgerufen werden.

    Commitment   nur der Eigentümer, sehen und ändern
    Plan         nur der Eigentümer, sehen und ändern
    Position     sehen:  Planbesitzer + Mitglieder des gesetzten Haushalts
                 ändern: dieselben, jede Änderung wird protokolliert
    Household    sehen:  Mitglieder
                 ändern: nur die Rolle owner

Die Prüffunktionen liefern `bool`. `require()` macht daraus einen
HTTP-Fehler mit **Code**, den das Frontend übersetzt.
"""

import uuid

from fastapi import HTTPException, status
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.commitment import Commitment
from app.models.enums import Role
from app.models.household import HouseholdMember
from app.models.plan import Plan
from app.models.user import User


def require(allowed: bool, code: str) -> None:
    """Wirft einen 403 mit Fehler-Code, wenn nicht erlaubt.

    Der Code ist maschinenlesbar, der Text kommt aus dem Frontend.
    """
    if not allowed:
        raise HTTPException(status.HTTP_403_FORBIDDEN, detail={"code": code})


async def is_member(session: AsyncSession, user_id: uuid.UUID, household_id: uuid.UUID) -> bool:
    result = await session.execute(
        select(HouseholdMember.id).where(
            HouseholdMember.user_id == user_id,
            HouseholdMember.household_id == household_id,
        )
    )
    return result.scalar_one_or_none() is not None


async def is_household_owner(
    session: AsyncSession, user_id: uuid.UUID, household_id: uuid.UUID
) -> bool:
    result = await session.execute(
        select(HouseholdMember.id).where(
            HouseholdMember.user_id == user_id,
            HouseholdMember.household_id == household_id,
            HouseholdMember.role == Role.OWNER,
        )
    )
    return result.scalar_one_or_none() is not None


def owns_commitment(user: User, commitment: Commitment) -> bool:
    """Verpflichtungen sind privat — auch im gemeinsamen Haushalt.

    Jasmin sieht Toms O2-Vertrag nicht. Sie sieht nur den Posten, den er
    erzeugt, und auch nur wenn Tom ihn in den Haushalt gehängt hat.
    """
    return commitment.owner_id == user.id


def owns_plan(user: User, plan: Plan) -> bool:
    return plan.user_id == user.id


async def can_assign_to_household(
    session: AsyncSession, plan_owner_id: uuid.UUID, household_id: uuid.UUID | None
) -> bool:
    """Ein Posten darf nur in einen Haushalt, in dem sein Besitzer Mitglied ist.

    Ohne diese Prüfung könnte man Posten in fremde Haushalte schieben.
    """
    if household_id is None:
        return True
    return await is_member(session, plan_owner_id, household_id)
