"""Das Haushaltsbuch — was tatsächlich geflossen ist.

Der Plan sagt, wie der Monat gedacht war. Das Buch sagt, wie er lief. Beides
hängt nur an einer Stelle zusammen: eine Buchung **kann** einem Posten
zugeordnet werden, muss aber nicht. Der Kiosk ohne Planung steht trotzdem
drin.

Buchungen sind **privat**, auch im gemeinsamen Haushalt. Im Haushaltsplan sieht
der Partner `127,50 von 600` — nicht, dass davon 82,40 bei Rewe waren.
"""

import uuid
from decimal import Decimal

from fastapi import APIRouter, Depends, HTTPException, Query, status
from sqlalchemy import and_, extract, func, or_, select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.auth import current_active_user
from app.core.permissions import require
from app.db.session import get_session
from app.models.account import Account
from app.models.plan import Plan, PlanPosition
from app.models.transaction import Transaction
from app.models.user import User
from app.schemas.transaction import (
    TransactionCreate,
    TransactionRead,
    TransactionUpdate,
)

router = APIRouter()

ZERO = Decimal("0.00")


async def _own_account(session: AsyncSession, account_id: uuid.UUID, user: User) -> None:
    account = await session.get(Account, account_id)
    if account is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "account_not_found"})
    require(account.owner_id == user.id, "not_account_owner")


async def _own_position(session: AsyncSession, position_id: uuid.UUID, user: User) -> None:
    """Ein Posten gehört dem Besitzer seines Plans — auch ein Haushaltsposten.

    Ohne diese Prüfung könnte man eigene Buchungen an fremde Posten hängen und
    damit deren Ist-Betrag verändern.
    """
    position = await session.get(PlanPosition, position_id)
    if position is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "position_not_found"})

    plan = await session.get(Plan, position.plan_id)
    require(plan is not None and plan.user_id == user.id, "not_plan_owner")


async def _recalc_position(session: AsyncSession, position_id: uuid.UUID | None) -> None:
    """Schreibt `amount_actual` als Summe der zugeordneten Buchungen fort.

    Mitgeschrieben statt bei jedem Lesen berechnet: `_summarize`, die
    Planübersicht und das Frontend lesen die Spalte längst. Eine Unterabfrage
    an all diesen Stellen wäre teurer und invasiver als eine Zeile hier.
    """
    if position_id is None:
        return

    position = await session.get(PlanPosition, position_id)
    if position is None:
        return

    total = await session.scalar(
        select(func.sum(Transaction.amount)).where(Transaction.position_id == position_id)
    )
    # Keine Buchungen mehr: zurück auf NULL, nicht auf 0. „Nichts erfasst" und
    # „null Euro ausgegeben" sind verschiedene Aussagen.
    position.amount_actual = total if total is not None else None


async def _load(session: AsyncSession, transaction_id: uuid.UUID, user: User) -> Transaction:
    transaction = await session.get(Transaction, transaction_id)
    if transaction is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "transaction_not_found"})
    require(transaction.owner_id == user.id, "not_allowed")
    return transaction


@router.get("", response_model=list[TransactionRead])
async def list_transactions(
    year: int | None = Query(default=None, ge=2000, le=2100),
    month: int | None = Query(default=None, ge=1, le=12),
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> list[Transaction]:
    """Eigene Buchungen, neueste zuerst. Ohne Zeitraum alle.

    Welcher Monat, entscheidet der **Posten** — nicht das Datum:

    * mit Posten  → der Monat des Plans, zu dem der Posten gehört
    * ohne Posten → der Monat, in dem das Geld floss

    Wohngeld für August wird am 31. Juli überwiesen, ALG1 ebenso. Sie gehören
    in den August und tauchen dort auf, mit ihrem echten Juli-Datum. Genau das
    machen die meisten Haushaltsbücher falsch: sie legen eine Buchung nach
    ihrem Datum ab, und damit ist Wohngeld für immer ein Juli-Vorgang.

    Die Regel schließt aus, statt zu ergänzen — eine zugeordnete Buchung steht
    in **einem** Monat, nicht in zweien. Sonst zählte sie doppelt, sobald man
    Summen über das Buch bildet.
    """
    query = select(Transaction).where(Transaction.owner_id == user.id)

    if year is not None and month is not None:
        in_month = and_(
            extract("year", Transaction.occurred_on) == year,
            extract("month", Transaction.occurred_on) == month,
        )
        belongs_to_plan = Transaction.position_id.in_(
            select(PlanPosition.id)
            .join(Plan, Plan.id == PlanPosition.plan_id)
            .where(Plan.user_id == user.id, Plan.year == year, Plan.month == month)
        )
        # Ohne Posten zählt das Datum, mit Posten der Plan — nie beides.
        query = query.where(
            or_(and_(Transaction.position_id.is_(None), in_month), belongs_to_plan)
        )
    elif year is not None:
        query = query.where(extract("year", Transaction.occurred_on) == year)
    elif month is not None:
        query = query.where(extract("month", Transaction.occurred_on) == month)

    result = await session.execute(
        query.order_by(Transaction.occurred_on.desc(), Transaction.created_at.desc())
    )
    return list(result.scalars())


@router.post("", response_model=TransactionRead, status_code=status.HTTP_201_CREATED)
async def create_transaction(
    payload: TransactionCreate,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> Transaction:
    await _own_account(session, payload.account_id, user)
    if payload.counter_account_id is not None:
        await _own_account(session, payload.counter_account_id, user)
    if payload.position_id is not None:
        await _own_position(session, payload.position_id, user)

    transaction = Transaction(owner_id=user.id, **payload.model_dump())
    session.add(transaction)
    await session.flush()

    await _recalc_position(session, transaction.position_id)
    await session.commit()
    await session.refresh(transaction)
    return transaction


@router.patch("/{transaction_id}", response_model=TransactionRead)
async def update_transaction(
    transaction_id: uuid.UUID,
    payload: TransactionUpdate,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> Transaction:
    transaction = await _load(session, transaction_id, user)
    changes = payload.model_dump(exclude_unset=True)

    for field in ("account_id", "counter_account_id"):
        if changes.get(field) is not None:
            await _own_account(session, changes[field], user)
    if changes.get("position_id") is not None:
        await _own_position(session, changes["position_id"], user)

    # Wandert die Buchung zu einem anderen Posten, müssen **beide** neu
    # gerechnet werden — der alte verliert sie, der neue bekommt sie.
    previous_position = transaction.position_id

    for field, value in changes.items():
        setattr(transaction, field, value)

    await session.flush()
    await _recalc_position(session, previous_position)
    if transaction.position_id != previous_position:
        await _recalc_position(session, transaction.position_id)

    await session.commit()
    await session.refresh(transaction)
    return transaction


@router.delete("/{transaction_id}", status_code=status.HTTP_204_NO_CONTENT)
async def delete_transaction(
    transaction_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> None:
    transaction = await _load(session, transaction_id, user)
    position_id = transaction.position_id

    await session.delete(transaction)
    await session.flush()
    await _recalc_position(session, position_id)
    await session.commit()
