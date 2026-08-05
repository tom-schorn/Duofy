"""Verträge, Sparziele und Schulden.

Eine Tabelle für alle drei — `type` sagt nur, ob das Ding ein Ende hat. Ein
Vertrag kann in jedem Budget liegen: Miete in Fixkosten, Streaming in Wünsche,
Sparplan in Sparen.

Verpflichtungen sind **privat**, auch im gemeinsamen Haushalt. Jasmin sieht
Toms Handyvertrag nicht — nur den Posten, den er erzeugt, und auch nur wenn
Tom ihn in den Haushalt gehängt hat.
"""

import uuid

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.auth import current_active_user
from app.core.permissions import can_assign_to_household, owns_commitment, require
from app.db.session import get_session
from app.models.commitment import Commitment
from app.models.enums import resolve_block
from app.models.user import User
from app.schemas.commitment import CommitmentCreate, CommitmentRead, CommitmentUpdate

router = APIRouter()


async def _load(session: AsyncSession, commitment_id: uuid.UUID, user: User) -> Commitment:
    commitment = await session.get(Commitment, commitment_id)
    if commitment is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "commitment_not_found"})
    require(owns_commitment(user, commitment), "not_commitment_owner")
    return commitment


@router.get("", response_model=list[CommitmentRead])
async def list_commitments(
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> list[Commitment]:
    """Alle eigenen Verpflichtungen, aktive wie stillgelegte."""
    result = await session.execute(
        select(Commitment)
        .where(Commitment.owner_id == user.id)
        .order_by(Commitment.block, Commitment.amount.desc())
    )
    return list(result.scalars())


@router.post("", response_model=CommitmentRead, status_code=status.HTTP_201_CREATED)
async def create_commitment(
    payload: CommitmentCreate,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> Commitment:
    require(
        await can_assign_to_household(session, user.id, payload.household_id),
        "not_household_member",
    )

    data = payload.model_dump()
    # Bei Sparziel und Schuld steht der Block fest — die Wahl des Nutzers wird
    # überstimmt, damit Tilgung nicht als Wunsch durchgeht.
    data["block"] = resolve_block(payload.block, payload.type)

    commitment = Commitment(owner_id=user.id, **data)
    session.add(commitment)
    await session.commit()
    await session.refresh(commitment)
    return commitment


@router.patch("/{commitment_id}", response_model=CommitmentRead)
async def update_commitment(
    commitment_id: uuid.UUID,
    payload: CommitmentUpdate,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> Commitment:
    commitment = await _load(session, commitment_id, user)
    changes = payload.model_dump(exclude_unset=True)

    if "household_id" in changes:
        require(
            await can_assign_to_household(session, user.id, changes["household_id"]),
            "not_household_member",
        )

    for field, value in changes.items():
        setattr(commitment, field, value)

    # Nach jeder Änderung neu festzurren — der Typ kann den Block überstimmen.
    commitment.block = resolve_block(commitment.block, commitment.type)

    # Was die Datenbank ohnehin prüft, hier mit lesbarem Code abfangen.
    if commitment.rhythm.value != "monthly" and commitment.first_due_date is None:
        raise HTTPException(
            status.HTTP_422_UNPROCESSABLE_ENTITY, detail={"code": "first_due_date_required"}
        )

    await session.commit()
    await session.refresh(commitment)
    return commitment


@router.delete("/{commitment_id}", status_code=status.HTTP_204_NO_CONTENT)
async def delete_commitment(
    commitment_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> None:
    """Löschen.

    Bereits erzeugte Posten bleiben stehen — `commitment_id` steht auf
    ON DELETE SET NULL. Für künftige Monate entsteht nichts mehr.
    """
    commitment = await _load(session, commitment_id, user)
    await session.delete(commitment)
    await session.commit()
