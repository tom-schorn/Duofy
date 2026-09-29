"""Whose data a request covers — the "Sicht".

`permissions.py` answers "may I?". This module answers the question before it:
which people a list or a total is about. There are three lenses, and every read
endpoint that takes `owner` or `household` picks one of them here:

    own        nothing given, or oneself: one's own data, no check
    member     `owner` of somebody else: needs `view` in the area, from them
    household  `household`: every member, or only those who granted insight —
               the endpoint says which (see `HouseholdMembers`)

`household` wins if both are given.
"""

import uuid
from calendar import monthrange
from collections.abc import Iterable
from dataclasses import dataclass
from datetime import date, datetime
from enum import StrEnum

from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.permissions import (
    GRANT_COLUMN,
    Area,
    granted_level,
    is_member,
    require,
    require_level,
)
from app.models.enums import AccessLevel
from app.models.household import HouseholdMember
from app.models.user import User


class Lens(StrEnum):
    OWN = "own"
    MEMBER = "member"
    HOUSEHOLD = "household"


class HouseholdMembers(StrEnum):
    """Who a household lens counts in.

    `ALL` for what the household shares: plan positions and the bookings on them
    show everybody regardless of grants (decision 48). `GRANTED` for what stays a
    person's own even inside the household — accounts and their balances; the
    household owns no account (decision 54).
    """

    ALL = "all"
    GRANTED = "granted"


@dataclass(frozen=True)
class Scope:
    lens: Lens
    owner_ids: list[uuid.UUID]


async def resolve_scope(
    session: AsyncSession,
    user: User,
    area: Area,
    *,
    owner: uuid.UUID | None,
    household: uuid.UUID | None,
    members: HouseholdMembers,
) -> Scope:
    """Pick the lens for a read and check the asker may look through it."""
    if household is not None:
        require(await is_member(session, user.id, household), "not_household_member")
        if members is HouseholdMembers.ALL:
            owner_ids = await household_member_ids(session, household)
        else:
            owner_ids = await viewable_members(session, household, user.id, area)
        return Scope(Lens.HOUSEHOLD, owner_ids)

    if owner is None or owner == user.id:
        return Scope(Lens.OWN, [user.id])

    await require_level(session, owner, user, area, AccessLevel.VIEW)
    return Scope(Lens.MEMBER, [owner])


async def may_see(session: AsyncSession, owner_id: uuid.UUID, viewer: User, area: Area) -> bool:
    """Whether `viewer` may see data of `owner_id` in `area`, as a yes or no.

    For the parts of a view that come from another area than the view itself —
    another person's plan shows their bookings only with the accounts grant
    (decision 53). One's own always.
    """
    if owner_id == viewer.id:
        return True
    level = await granted_level(session, owner_id, viewer.id, area)
    return level.rank >= AccessLevel.VIEW.rank


async def household_member_ids(session: AsyncSession, household_id: uuid.UUID) -> list[uuid.UUID]:
    """Every member of a household. The household plan and book show everything of
    everybody except what sits on a private position, so no grant narrows this list
    (decision 48, #242)."""
    result = await session.execute(
        select(HouseholdMember.user_id).where(HouseholdMember.household_id == household_id)
    )
    return list(result.scalars())


async def viewable_members(
    session: AsyncSession, household_id: uuid.UUID, viewer_id: uuid.UUID, area: Area
) -> list[uuid.UUID]:
    """Whose figures may be added up for a household view of one area.

    Always oneself, plus every member who granted at least `view` in that area.
    Anyone below that is missing from the list — the totals are then incomplete and
    the frontend says so. A number silently missing a person would be worse than no
    number at all.

    The area matters: household accounts add up balances, so somebody who shares
    their month but not their accounts does not belong in that total.

    Assumes the asker is a member; the caller checks that.
    """
    result = await session.execute(
        select(HouseholdMember.user_id, GRANT_COLUMN[area]).where(
            HouseholdMember.household_id == household_id
        )
    )
    return [
        user_id
        for user_id, level in result.all()
        if user_id == viewer_id or AccessLevel(level).rank >= AccessLevel.VIEW.rank
    ]


def eligible_member_ids(
    members: Iterable[tuple[uuid.UUID, datetime]], year: int, month: int
) -> set[uuid.UUID]:
    """Members already part of the household by the last day of that month.

    A member who joins later must not make earlier months incomplete, and does
    not need a plan for a month before they joined.
    """
    last_day = date(year, month, monthrange(year, month)[1])
    return {member_id for member_id, joined_at in members if joined_at.date() <= last_day}
