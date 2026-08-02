"""Zahlungskonten — Giro, Tagesgeld, Kreditkarte, PayPal, Bargeld.

Konten sind **privat**, auch im gemeinsamen Haushalt: jeder sieht nur seine
eigenen. Für die gemeinsame Planung zählt, was auf den Posten steht, nicht wo
das Geld liegt.

Der Stand steht bewusst nicht in der Tabelle. Er ergibt sich aus dem
Anfangsbestand plus den Buchungen danach — sobald es Buchungen gibt.
"""

import uuid

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy import select, update
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.auth import current_active_user
from app.core.permissions import require
from app.db.session import get_session
from app.models.account import Account
from app.models.user import User
from app.schemas.account import AccountCreate, AccountRead, AccountUpdate

router = APIRouter()


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


@router.get("", response_model=list[AccountRead])
async def list_accounts(
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> list[Account]:
    """Eigene Konten, aufgelöste zuletzt."""
    result = await session.execute(
        select(Account)
        .where(Account.owner_id == user.id)
        .order_by(Account.active.desc(), Account.name)
    )
    return list(result.scalars())


@router.post("", response_model=AccountRead, status_code=status.HTTP_201_CREATED)
async def create_account(
    payload: AccountCreate,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> Account:
    if payload.is_default:
        await _clear_other_defaults(session, user)

    account = Account(owner_id=user.id, **payload.model_dump())
    session.add(account)
    await session.commit()
    await session.refresh(account)
    return account


@router.patch("/{account_id}", response_model=AccountRead)
async def update_account(
    account_id: uuid.UUID,
    payload: AccountUpdate,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> Account:
    account = await _load(session, account_id, user)
    changes = payload.model_dump(exclude_unset=True)

    if changes.get("is_default"):
        await _clear_other_defaults(session, user, keep=account.id)

    for field, value in changes.items():
        setattr(account, field, value)

    await session.commit()
    await session.refresh(account)
    return account


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
