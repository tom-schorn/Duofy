"""Posten eines Monatsplans.

Eigener Router, damit sich `/positions/{id}` und `/plans/{id}` nicht in die
Quere kommen — sonst versucht FastAPI, „positions" als Jahreszahl zu lesen.

Im gemeinsamen Haushalt dürfen beide Mitglieder Posten ändern, auch die des
anderen. Jede Änderung wird protokolliert.
"""

import uuid
from datetime import UTC, datetime

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.auth import current_active_user
from app.core.permissions import can_assign_to_household, owns_plan, require
from app.db.session import get_session
from app.models.plan import Plan, PlanPosition, PlanPositionChange
from app.models.user import User
from app.schemas.plan import PositionRead, PositionUpdate

router = APIRouter()


async def _load(
    session: AsyncSession, position_id: uuid.UUID, user: User
) -> tuple[PlanPosition, Plan]:
    position = await session.get(PlanPosition, position_id)
    if position is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "position_not_found"})

    plan = await session.get(Plan, position.plan_id)
    if plan is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "plan_not_found"})

    # **Nur eigene Posten.** Ein Haushaltsposten ist zwar für alle Mitglieder
    # sichtbar, gehört aber weiterhin einer Person — geändert wird er nur von
    # ihr. Jeder trägt seinen Teil und bucht auf seine eigenen Posten; kauft
    # der Partner mit ein, wird der Betrag überwiesen und beim Besitzer
    # verbucht. Ein Zugriff auf fremde Posten wird dafür nicht gebraucht, und
    # ein Recht, das niemand braucht, ist bei Finanzdaten eins zu viel.
    #
    # Gelesen wird der gemeinsame Plan weiterhin von allen — der prüft über
    # `is_member`, nicht hierüber.
    require(owns_plan(user, plan), "not_allowed")
    return position, plan


@router.patch("/{position_id}", response_model=PositionRead)
async def update_position(
    position_id: uuid.UUID,
    payload: PositionUpdate,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> PlanPosition:
    """Einen Posten ändern — jede Änderung landet im Protokoll."""
    position, plan = await _load(session, position_id, user)

    changes = payload.model_dump(exclude_unset=True)
    if "household_id" in changes:
        require(
            await can_assign_to_household(session, plan.user_id, changes["household_id"]),
            "not_household_member",
        )

    touched = False
    for field, value in changes.items():
        old = getattr(position, field)
        if old == value:
            continue
        session.add(
            PlanPositionChange(
                position_id=position.id,
                changed_by_id=user.id,
                field=field,
                old_value=None if old is None else str(old),
                new_value=None if value is None else str(value),
            )
        )
        setattr(position, field, value)
        touched = True

    # Manuelle Korrekturen dürfen beim nächsten Erzeugen nicht überschrieben
    # werden — das merkt sich der Posten selbst.
    if touched and position.commitment_id is not None:
        position.manually_changed = True

    await session.commit()
    await session.refresh(position)
    return position


@router.delete("/{position_id}", status_code=status.HTTP_204_NO_CONTENT)
async def delete_position(
    position_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> None:
    position, _ = await _load(session, position_id, user)
    await session.delete(position)
    await session.commit()


@router.post("/{position_id}/paid", response_model=PositionRead)
async def mark_paid(
    position_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> PlanPosition:
    """Abhaken.

    Getrennt vom Betrag: „abgehakt" und „Betrag eingetragen" sind zwei Dinge.
    Die Miete kann bezahlt sein und exakt dem geplanten Betrag entsprechen.
    """
    position, _ = await _load(session, position_id, user)
    position.paid_at = datetime.now(UTC)
    await session.commit()
    await session.refresh(position)
    return position


@router.delete("/{position_id}/paid", response_model=PositionRead)
async def unmark_paid(
    position_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> PlanPosition:
    position, _ = await _load(session, position_id, user)
    position.paid_at = None
    await session.commit()
    await session.refresh(position)
    return position
