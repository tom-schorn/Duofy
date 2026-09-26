"""What happens to the change log when the person who wrote entries is deleted (#66).

An entry a person left on **another** member's position is part of that position's
history and stays, without an author. An entry on the deleted person's **own**
positions goes with those positions.

The user is deleted straight in the database rather than through an endpoint: no
endpoint deletes a user (that is a decision still to be taken), and the foreign keys
are what is under test.
"""

from decimal import Decimal

from sqlalchemy import delete, select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.enums import Budget, Category
from app.models.plan import Plan, PlanPosition, PlanPositionChange
from app.models.user import User
from tests.test_area_permissions import make_user


async def make_position(session: AsyncSession, owner: User, label: str) -> PlanPosition:
    plan = Plan(user_id=owner.id, year=2026, month=10)
    session.add(plan)
    await session.flush()
    position = PlanPosition(
        plan_id=plan.id,
        label=label,
        amount_planned=Decimal("40.00"),
        category=Category.LEISURE_SUBSCRIPTIONS,
        budget=Budget.WANTS,
        due_day=1,
    )
    session.add(position)
    await session.flush()
    return position


async def log_change(session: AsyncSession, position: PlanPosition, author: User) -> None:
    session.add(
        PlanPositionChange(
            position_id=position.id,
            changed_by_id=author.id,
            field="amount_planned",
            old_value="40.00",
            new_value="45.00",
        )
    )
    await session.flush()


async def test_entries_on_other_positions_stay_without_author_when_the_author_is_deleted(
    session: AsyncSession,
):
    author = await make_user(session, "Author")
    other = await make_user(session, "Other")
    others_position = await make_position(session, other, "Other position")
    await log_change(session, others_position, author)
    await session.commit()

    await session.execute(delete(User).where(User.id == author.id))
    await session.commit()

    entries = (await session.execute(select(PlanPositionChange))).scalars().all()
    assert len(entries) == 1
    assert entries[0].position_id == others_position.id
    assert entries[0].changed_by_id is None


async def test_entries_on_the_deleted_persons_own_positions_go_with_them(
    session: AsyncSession,
):
    author = await make_user(session, "Author")
    other = await make_user(session, "Other")
    own_position = await make_position(session, author, "Own position")
    others_position = await make_position(session, other, "Other position")
    await log_change(session, own_position, author)
    await log_change(session, own_position, other)
    await log_change(session, others_position, author)
    await session.commit()

    await session.execute(delete(User).where(User.id == author.id))
    await session.commit()

    entries = (await session.execute(select(PlanPositionChange))).scalars().all()
    assert [entry.position_id for entry in entries] == [others_position.id]
    assert entries[0].changed_by_id is None
