"""Zahlungskonten — Giro, Tagesgeld, Kreditkarte, PayPal, Bargeld.

Konten sind **privat**, auch im gemeinsamen Haushalt: jeder sieht nur seine
eigenen. Für die gemeinsame Planung zählt, was auf den Posten steht, nicht wo
das Geld liegt.

Der Stand steht bewusst nicht in der Tabelle. Er ergibt sich aus dem
Anfangsbestand plus den Buchungen danach — sobald es Buchungen gibt.
"""

import uuid
from calendar import monthrange
from datetime import date
from decimal import Decimal

from fastapi import APIRouter, Depends, HTTPException, Query, status
from sqlalchemy import case, func, literal, select, update
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.auth import current_active_user
from app.core.permissions import granted_level, require
from app.db.session import get_session
from app.models.account import Account
from app.models.enums import AccessLevel, Block
from app.models.transaction import Transaction
from app.models.user import User
from app.schemas.account import (
    AccountCreate,
    AccountRead,
    AccountUpdate,
    BalanceHistory,
    BalancePoint,
)

router = APIRouter()

ZERO = Decimal("0.00")


async def _clear_other_defaults(
    session: AsyncSession, user: User, keep: uuid.UUID | None = None
) -> None:
    """Nimmt allen anderen Konten die Standard-Markierung.

    Die Datenbank lässt über einen partiellen Unique-Index nur eines zu. Ohne
    dieses Aufräumen bekäme man beim Umstellen einen Integritätsfehler statt
    des erwarteten Verhaltens — ein neues Standardkonto soll das alte ablösen,
    nicht abgelehnt werden.
    """
    query = update(Account).where(Account.owner_id == user.id, Account.is_default)
    if keep is not None:
        query = query.where(Account.id != keep)
    await session.execute(query.values(is_default=False))


async def _load(session: AsyncSession, account_id: uuid.UUID, user: User) -> Account:
    account = await session.get(Account, account_id)
    if account is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "account_not_found"})
    require(account.owner_id == user.id, "not_account_owner")
    return account


async def _target_owner(
    session: AsyncSession, owner: uuid.UUID | None, user: User
) -> uuid.UUID:
    """Wessen Konten gemeint sind — und ob der Fragende sie sehen darf.

    Ohne `owner` die eigenen. Mit `owner` die eines Haushaltsmitglieds, das
    mindestens Stufe `view` gegeben hat. Die Stufe gibt der Besitzer selbst,
    siehe `AccessLevel`.
    """
    if owner is None or owner == user.id:
        return user.id

    level = await granted_level(session, owner, user.id)
    require(level.rank >= AccessLevel.VIEW.rank, "no_insight_granted")
    return owner


async def _balances(session: AsyncSession, owner_id: uuid.UUID) -> dict[uuid.UUID, Decimal]:
    """Wieviel jede Buchung am Stand des jeweiligen Kontos bewegt hat.

    Die Buchung trägt kein Vorzeichen, die Richtung steht woanders:

    * **Umbuchung** — verlässt `account_id`, kommt bei `counter_account_id` an.
    * **sonst** — `block = income` ist Zufluss, alles andere Abfluss.

    Zwei Summen, weil ein Konto in beiden Spalten vorkommen kann: als Quelle
    der einen Umbuchung und als Ziel der nächsten.
    """
    outgoing = await session.execute(
        select(
            Transaction.account_id,
            func.sum(
                case(
                    # Umbuchung: geht raus, egal welcher Block.
                    (Transaction.counter_account_id.isnot(None), -Transaction.amount),
                    (Transaction.block == Block.INCOME, Transaction.amount),
                    else_=-Transaction.amount,
                )
            ),
        )
        .where(Transaction.owner_id == owner_id)
        .group_by(Transaction.account_id)
    )
    incoming = await session.execute(
        select(Transaction.counter_account_id, func.sum(Transaction.amount))
        .where(Transaction.owner_id == owner_id, Transaction.counter_account_id.isnot(None))
        .group_by(Transaction.counter_account_id)
    )

    moved: dict[uuid.UUID, Decimal] = {}
    for account_id, total in list(outgoing.all()) + list(incoming.all()):
        moved[account_id] = moved.get(account_id, Decimal("0.00")) + total
    return moved


async def _with_balance(
    session: AsyncSession, account: Account, owner_id: uuid.UUID
) -> AccountRead:
    """Ein Konto samt Stand.

    Nötig, weil `balance` berechnet ist und nicht am Objekt hängt: ohne diesen
    Schritt meldete `POST /accounts` den Vorgabewert 0,00, obwohl gerade ein
    Anfangsbestand von 150,96 gesetzt wurde. Ein Client, der die Antwort
    anzeigt, zeigte dann eine Zahl, die es nie gab.
    """
    moved = await _balances(session, owner_id)
    return AccountRead.model_validate(account).model_copy(
        update={"balance": account.opening_balance + moved.get(account.id, ZERO)}
    )


@router.get("", response_model=list[AccountRead])
async def list_accounts(
    owner: uuid.UUID | None = Query(default=None),
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> list[AccountRead]:
    """Konten mit Stand, aufgelöste zuletzt.

    Ohne `owner` die eigenen. Mit `owner` die eines Mitglieds, das Einblick
    gegeben hat — für die Personenansicht im Haushalt.
    """
    owner_id = await _target_owner(session, owner, user)
    result = await session.execute(
        select(Account)
        .where(Account.owner_id == owner_id)
        .order_by(Account.active.desc(), Account.name)
    )
    moved = await _balances(session, owner_id)

    return [
        AccountRead.model_validate(account).model_copy(
            update={"balance": account.opening_balance + moved.get(account.id, ZERO)}
        )
        for account in result.scalars()
    ]


#: Wieviel eine Buchung am **Gesamtstand** bewegt. Umbuchungen fehlen bewusst:
#: sie schieben zwischen eigenen Konten, in der Summe passiert nichts.
_TOTAL_DELTA = case(
    (Transaction.counter_account_id.isnot(None), literal(0)),
    (Transaction.block == Block.INCOME, Transaction.amount),
    else_=-Transaction.amount,
)


@router.get("/history", response_model=BalanceHistory)
async def balance_history(
    year: int = Query(ge=2000, le=2100),
    month: int = Query(ge=1, le=12),
    owner: uuid.UUID | None = Query(default=None),
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> BalanceHistory:
    """Wie sich der Gesamtstand über einen Kalendermonat entwickelt hat.

    Das Gegenstück zum Verlauf im Plan: der zeigt, wie der Monat gedacht war,
    dieser, wie er lief.

    **Nach Datum, nicht nach Posten.** Das Buch ordnet eine Buchung dem Monat
    ihres Postens zu — für eine Kontostandkurve wäre das falsch. Das Geld war
    am 30. Juli auf dem Konto, auch wenn es für den August gedacht ist.

    Muss **vor** `/{account_id}` stehen, sonst versucht FastAPI, „history" als
    UUID zu lesen.
    """
    owner_id = await _target_owner(session, owner, user)
    first = date(year, month, 1)
    last = date(year, month, monthrange(year, month)[1])

    opening = await session.scalar(
        select(func.coalesce(func.sum(Account.opening_balance), ZERO)).where(
            Account.owner_id == owner_id
        )
    )
    before = await session.scalar(
        select(func.coalesce(func.sum(_TOTAL_DELTA), ZERO)).where(
            Transaction.owner_id == owner_id, Transaction.occurred_on < first
        )
    )

    daily = await session.execute(
        select(Transaction.occurred_on, func.sum(_TOTAL_DELTA))
        .where(
            Transaction.owner_id == owner_id,
            Transaction.occurred_on >= first,
            Transaction.occurred_on <= last,
        )
        .group_by(Transaction.occurred_on)
        .order_by(Transaction.occurred_on)
    )

    # Ein Punkt je Tag **mit** Bewegung. Die Tage dazwischen ergänzt das
    # Diagramm als Stufe — sie tragen keine Information.
    running = (opening or ZERO) + (before or ZERO)
    points = []
    for day, delta in daily.all():
        running += delta
        points.append(BalancePoint(day=day, balance=running, change=delta))

    return BalanceHistory(
        opening_balance=(opening or ZERO) + (before or ZERO),
        closing_balance=running,
        points=points,
    )


@router.post("", response_model=AccountRead, status_code=status.HTTP_201_CREATED)
async def create_account(
    payload: AccountCreate,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> AccountRead:
    if payload.is_default:
        await _clear_other_defaults(session, user)

    account = Account(owner_id=user.id, **payload.model_dump())
    session.add(account)
    await session.commit()
    await session.refresh(account)
    return await _with_balance(session, account, user.id)


@router.patch("/{account_id}", response_model=AccountRead)
async def update_account(
    account_id: uuid.UUID,
    payload: AccountUpdate,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> AccountRead:
    account = await _load(session, account_id, user)
    changes = payload.model_dump(exclude_unset=True)

    if changes.get("is_default"):
        await _clear_other_defaults(session, user, keep=account.id)

    for field, value in changes.items():
        setattr(account, field, value)

    await session.commit()
    await session.refresh(account)
    return await _with_balance(session, account, user.id)


@router.delete("/{account_id}", status_code=status.HTTP_204_NO_CONTENT)
async def delete_account(
    account_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> None:
    """Löschen ist für Versehen da.

    Ein Konto, das man nicht mehr benutzt, setzt man auf `active = false` —
    dann behalten die späteren Buchungen ihren Bezug.
    """
    account = await _load(session, account_id, user)
    await session.delete(account)
    await session.commit()
