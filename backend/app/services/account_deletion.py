"""Deleting one's own account, for good."""

from fastapi import HTTPException, status
from sqlalchemy import delete, func, select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.account import Account
from app.models.enums import Role
from app.models.household import Household, HouseholdMember
from app.models.transaction import Transaction
from app.models.user import User


async def delete_own_account(session: AsyncSession, user: User) -> None:
    """Remove a person and everything that is theirs, in one transaction.

    **Bookings go first, outside the cascade.** `transactions.account_id` and
    `.counter_account_id` are `RESTRICT` — an account with history is never
    silently emptied by a stray delete elsewhere (see `Transaction`). Deleting the
    person's own bookings before the cascade from `users` reaches their accounts
    keeps the two from racing: nothing still references an account by the time it
    is dropped.

    **A household this person is admin of keeps its other members**: if no other
    admin is left, the role passes to whoever joined earliest. A household with
    nobody left in it goes too —
    `HouseholdMember`'s cascade from `users` would otherwise leave it behind empty
    and unreachable, since there is no route that deletes a household directly.

    Everything else — accounts, plans and their positions, commitments,
    memberships, invitations sent — is a plain `ON DELETE CASCADE` from `users`.
    """
    if user.is_superuser:
        other_admins = await session.scalar(
            select(func.count())
            .select_from(User)
            .where(User.is_superuser.is_(True), User.id != user.id)
        )
        if not other_admins:
            raise HTTPException(status.HTTP_409_CONFLICT, detail={"code": "last_admin"})

    owned = await session.scalars(
        select(HouseholdMember).where(
            HouseholdMember.user_id == user.id, HouseholdMember.role == Role.ADMIN
        )
    )
    for membership in owned:
        successor = await session.scalar(
            select(HouseholdMember)
            .where(
                HouseholdMember.household_id == membership.household_id,
                HouseholdMember.user_id != user.id,
            )
            # Another admin keeps the household as it is; otherwise the earliest member.
            .order_by((HouseholdMember.role == Role.ADMIN).desc(), HouseholdMember.created_at)
            .limit(1)
        )
        if successor is not None:
            successor.role = Role.ADMIN
        else:
            household = await session.get(Household, membership.household_id)
            if household is not None:
                await session.delete(household)

    own_accounts = select(Account.id).where(Account.owner_id == user.id)
    await session.execute(
        delete(Transaction).where(
            (Transaction.owner_id == user.id)
            | Transaction.account_id.in_(own_accounts)
            | Transaction.counter_account_id.in_(own_accounts)
        )
    )

    await session.delete(user)
    await session.commit()
