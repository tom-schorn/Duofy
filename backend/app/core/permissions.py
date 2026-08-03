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
from app.models.enums import AccessLevel, Role
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


async def granted_level(
    session: AsyncSession, owner_id: uuid.UUID, viewer_id: uuid.UUID
) -> AccessLevel:
    """Was `viewer` bei `owner` darf — über alle gemeinsamen Haushalte hinweg.

    Die Stufe hängt an **`owner`s** Mitgliedschaft: er gibt sie, nicht der, der
    sie nutzen will. Sind beide in mehreren Haushalten zusammen, gilt die
    höchste — sonst hinge das Recht davon ab, über welchen Haushalt man gerade
    schaut, und dieselbe Person sähe je nach Weg etwas anderes.

    Sich selbst gegenüber gibt es keine Beschränkung.
    """
    if owner_id == viewer_id:
        return AccessLevel.EDIT

    gemeinsam = select(HouseholdMember.household_id).where(
        HouseholdMember.user_id == viewer_id
    )
    result = await session.execute(
        select(HouseholdMember.grants_access).where(
            HouseholdMember.user_id == owner_id,
            HouseholdMember.household_id.in_(gemeinsam),
        )
    )
    stufen = [AccessLevel(x) for x in result.scalars()]
    return max(stufen, key=lambda s: s.rank) if stufen else AccessLevel.PLAN


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
