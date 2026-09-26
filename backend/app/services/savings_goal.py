import uuid
from decimal import Decimal

from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.commitment import Commitment
from app.models.enums import CommitmentType
from app.models.plan import PlanPosition
from app.models.transaction import Transaction


async def saved_so_far(session: AsyncSession, commitment_id: uuid.UUID) -> Decimal:
    """What was put aside for a goal so far: its ticked instalments, nothing else.

    Derived, not stored (#87): the sum of the bookings that ticking created for the
    goal's positions. Withdrawals are invisible here on purpose; #86 replaces this
    with a balance per goal. Un-ticking deletes the booking, so it lowers the sum.
    """
    total = await session.scalar(
        select(func.coalesce(func.sum(Transaction.amount), 0))
        .join(PlanPosition, PlanPosition.id == Transaction.position_id)
        .where(PlanPosition.commitment_id == commitment_id, Transaction.auto_booked)
    )
    return Decimal(total)


async def amount_to_plan(session: AsyncSession, commitment: Commitment) -> Decimal | None:
    """The amount a due commitment puts into a new month; None means no position.

    Only a savings goal with a target is treated differently: reached means nothing
    is planned, less than one instalment missing means only the rest. Everything
    else plans its amount as before.
    """
    if commitment.type is not CommitmentType.SAVINGS_GOAL or commitment.target_amount is None:
        return commitment.amount

    missing = commitment.target_amount - await saved_so_far(session, commitment.id)
    if missing <= 0:
        return None
    return min(commitment.amount, missing)
