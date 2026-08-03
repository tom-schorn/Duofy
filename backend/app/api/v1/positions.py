"""Posten eines Monatsplans.

Eigener Router, damit sich `/positions/{id}` und `/plans/{id}` nicht in die
Quere kommen — sonst versucht FastAPI, „positions" als Jahreszahl zu lesen.

Im gemeinsamen Haushalt dürfen beide Mitglieder Posten ändern, auch die des
anderen. Jede Änderung wird protokolliert.
"""

import uuid
from datetime import UTC, date, datetime

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.auth import current_active_user
from app.core.permissions import (
    can_assign_to_household,
    granted_level,
    owns_plan,
    require,
)
from app.db.session import get_session
from app.models.account import Account
from app.models.enums import AccessLevel
from app.models.plan import Plan, PlanPosition, PlanPositionChange
from app.models.transaction import Transaction
from app.models.user import User
from app.schemas.plan import PositionPaid, PositionRead, PositionUpdate

router = APIRouter()


async def _load(
    session: AsyncSession,
    position_id: uuid.UUID,
    user: User,
    *,
    allow_delegate: bool = True,
) -> tuple[PlanPosition, Plan]:
    position = await session.get(PlanPosition, position_id)
    if position is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "position_not_found"})

    plan = await session.get(Plan, position.plan_id)
    if plan is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "plan_not_found"})

    # Der eigene Posten ist immer erlaubt.
    if owns_plan(user, plan):
        return position, plan

    # Fremde Posten nur mit Stufe `edit` — und die gibt der Besitzer selbst.
    # Bis dahin gilt weiter: jeder trägt seinen Teil und bucht auf seine
    # eigenen Posten. Kauft der Partner mit ein, wird der Betrag überwiesen
    # und beim Besitzer verbucht.
    #
    # Nachvollziehbar bleibt es über `plan_position_changes`: dort steht, wer
    # welches Feld wann geändert hat. Deshalb braucht es keine Sperre, sondern
    # ein Protokoll.
    #
    # `allow_delegate=False` beim Löschen: ändern ist umkehrbar und
    # protokolliert, löschen ist beides nicht.
    require(allow_delegate, "not_allowed")
    level = await granted_level(session, plan.user_id, user.id)
    require(level is AccessLevel.EDIT, "no_edit_granted")
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
    position, _ = await _load(session, position_id, user, allow_delegate=False)
    await session.delete(position)
    await session.commit()


@router.post("/{position_id}/paid", response_model=PositionRead)
async def mark_paid(
    position_id: uuid.UUID,
    payload: PositionPaid | None = None,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> PlanPosition:
    """Abhaken — und dabei ins Haushaltsbuch buchen.

    Getrennt vom Betrag: „abgehakt" und „Betrag eingetragen" sind zwei Dinge.
    Die Miete kann bezahlt sein und exakt dem geplanten Betrag entsprechen.

    **Gebucht wird nur, wenn dem Posten noch keine Buchung zugeordnet ist.**
    Bei der Miete gibt es keine, der Haken bucht also die 850 €. Beim
    Lebensmittel-Budget sind über den Monat schon Einkäufe erfasst — dort
    heißt der Haken nur „Monat durch", und das Buch bleibt die Wahrheit.

    Ohne diese Regel würden 600 € Einkäufe plus Haken 1.200 € ergeben.

    `payload` erlaubt beim Abhaken ein anderes Datum und einen anderen Betrag.
    Gebraucht wird das ständig: die Zahlung liegt ein paar Tage zurück, oder
    der Abschlag kam anders als geplant. Ohne die beiden Felder müsste man
    danach ins Buch gehen und die eben erzeugte Buchung korrigieren.
    """
    position, plan = await _load(session, position_id, user)
    position.paid_at = datetime.now(UTC)
    payload = payload or PositionPaid()

    already = await session.scalar(
        select(func.count())
        .select_from(Transaction)
        .where(Transaction.position_id == position.id)
    )

    if not already:
        # Kontoherkunft: erst der Posten, dann das Standardkonto. Ist keins da,
        # wird nichts gebucht — das Abhaken selbst darf daran nicht scheitern.
        # **Auf das Konto des Besitzers**, nicht des Abhakenden. Hakt Jasmin
        # Toms Miete ab, geht das Geld von Toms Konto — sonst entstünde eine
        # Buchung in ihrem Buch für eine Zahlung, die sie nie geleistet hat.
        account_id = position.account_id
        if account_id is None:
            account_id = await session.scalar(
                select(Account.id).where(Account.owner_id == plan.user_id, Account.is_default)
            )

        if account_id is not None:
            session.add(
                Transaction(
                    owner_id=plan.user_id,
                    account_id=account_id,
                    occurred_on=payload.occurred_on or date.today(),
                    amount=payload.amount or position.amount_planned,
                    note=position.label,
                    category=position.category,
                    block=position.block,
                    position_id=position.id,
                    auto_booked=True,
                )
            )
            await session.flush()
            position.amount_actual = payload.amount or position.amount_planned

    await session.commit()
    await session.refresh(position)
    return position


@router.delete("/{position_id}/paid", response_model=PositionRead)
async def unmark_paid(
    position_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> PlanPosition:
    """Haken wegnehmen — und die davon erzeugte Buchung mitnehmen.

    Nur die **selbst erzeugte** Buchung, erkennbar an `auto_booked`. Von Hand
    erfasste Einkäufe am selben Posten bleiben stehen; sie hat der Haken nie
    angelegt, also nimmt er sie auch nicht mit.

    Das Frontend fragt vorher nach und nennt den Betrag — falls er nachher von
    Hand korrigiert wurde, sieht man, was verloren geht.
    """
    position, _ = await _load(session, position_id, user)
    position.paid_at = None

    booked = await session.execute(
        select(Transaction).where(
            Transaction.position_id == position.id,
            Transaction.auto_booked,
        )
    )
    for transaction in booked.scalars():
        await session.delete(transaction)

    await session.flush()

    total = await session.scalar(
        select(func.sum(Transaction.amount)).where(Transaction.position_id == position.id)
    )
    position.amount_actual = total

    await session.commit()
    await session.refresh(position)
    return position
