"""Reading and writing `Grant` rows.

A grant is always written by its granter: `set_levels` takes the granter from the
caller's login and has no way to name anybody else (decision 57).
"""

import uuid
from collections.abc import Iterable, Mapping

from sqlalchemy import delete, or_, select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.enums import AccessLevel, Area
from app.models.grant import Grant

type Levels = dict[Area, AccessLevel]


def no_levels() -> Levels:
    """Every area at `none` — what a missing row means."""
    return {area: AccessLevel.NONE for area in Area}


async def levels_with(
    session: AsyncSession, me: uuid.UUID, others: Iterable[uuid.UUID]
) -> tuple[dict[uuid.UUID, Levels], dict[uuid.UUID, Levels]]:
    """What I granted each of `others`, and what each of them granted me.

    Every area is present in every entry; a missing row reads as `none`.
    """
    others = [other for other in others if other != me]
    given = {other: no_levels() for other in others}
    received = {other: no_levels() for other in others}
    if not others:
        return given, received

    rows = await session.execute(
        select(Grant).where(
            or_(
                (Grant.granter_id == me) & Grant.grantee_id.in_(others),
                (Grant.grantee_id == me) & Grant.granter_id.in_(others),
            )
        )
    )
    for grant in rows.scalars():
        if grant.granter_id == me:
            given[grant.grantee_id][grant.area] = grant.level
        else:
            received[grant.granter_id][grant.area] = grant.level
    return given, received


async def set_levels(
    session: AsyncSession,
    granter_id: uuid.UUID,
    grantee_id: uuid.UUID,
    levels: Mapping[Area, AccessLevel],
) -> None:
    """Set what `granter` allows `grantee`, only in the areas given.

    `none` deletes the row, so no row and `none` never mean two things.
    Flushes, does not commit.
    """
    existing = {
        grant.area: grant
        for grant in (
            await session.execute(
                select(Grant).where(Grant.granter_id == granter_id, Grant.grantee_id == grantee_id)
            )
        ).scalars()
    }
    for area, level in levels.items():
        row = existing.get(area)
        if level is AccessLevel.NONE:
            if row is not None:
                await session.delete(row)
        elif row is None:
            session.add(Grant(granter_id=granter_id, grantee_id=grantee_id, area=area, level=level))
        else:
            row.level = level
    await session.flush()


async def drop_all_of(session: AsyncSession, user_id: uuid.UUID) -> None:
    """Every grant from and to a person — when they leave or are removed.

    A grant is trust inside a household. Rejoining later starts at `none` rather
    than silently bringing old rights back.
    """
    await session.execute(
        delete(Grant).where(or_(Grant.granter_id == user_id, Grant.grantee_id == user_id))
    )
