"""Monthly plans and their positions.

A plan **always belongs to one person**, never to a household. The household plan
is not a table of its own — it is the composition of every member position that
has `household_id` set. Hence two routes to the same presentation: `/plans/...`
for your own plan, `/plans/household/...` for the shared view. Individual positions
live under `/positions`, otherwise `positions` would collide with the year.
"""

import uuid
from decimal import Decimal

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.orm import selectinload

from app.core.auth import current_active_user
from app.core.permissions import (
    can_assign_to_household,
    granted_level,
    is_member,
    owns_plan,
    require,
)
from app.db.session import get_session
from app.models.commitment import Commitment
from app.models.enums import AccessLevel, Block, CommitmentType
from app.models.household import Household, HouseholdMember
from app.models.plan import Plan, PlanPosition
from app.models.user import User
from app.schemas.plan import (
    BudgetTotals,
    HouseholdPlanRead,
    HouseholdPositionRead,
    MemberPlanRead,
    PlanCreate,
    PlanRead,
    PlanSummary,
    PlanUpdate,
    PositionCreate,
    PositionRead,
)

router = APIRouter()

ZERO = Decimal("0.00")


def _summarize(
    *,
    year: int,
    month: int,
    targets: tuple[Decimal, Decimal, Decimal],
    buffer_percent: Decimal,
    positions: list[PlanPosition],
) -> dict:
    """The figures the overview and the detail page show.

    `budget` is income minus buffer — the basis the quotas are computed on. Not to
    be confused with what is left to allocate: that is the remainder of it and is
    derived in the frontend, where it updates live anyway.
    """
    # Pass-through positions stay out — they were never budget. Counting them would
    # inflate the budget and the savings quota with it, although the household has
    # not a cent more to distribute.
    counting = [p for p in positions if not p.pass_through]

    income = sum((p.amount_planned for p in counting if p.block is Block.INCOME), ZERO)
    budget = income - (income * buffer_percent / 100)

    def total(block: Block) -> Decimal:
        return sum((p.amount_planned for p in counting if p.block is block), ZERO)

    def remaining(position: PlanPosition) -> Decimal:
        """What is still to go out for this position.

        Ticked off means done. Otherwise what counts is the planned amount minus
        what the book already records: a 600 budget with 127.50 of purchases booked
        still expects 472.50, not 600.

        Never negative — overspending a budget does not leave anything over.
        """
        if position.block is Block.INCOME or position.paid_at is not None:
            return ZERO
        # A pass-through position stands and falls with its own income. Counting it
        # here would make the month look underfunded although no money of your own
        # is missing — only the forwarding is still pending.
        if position.pass_through:
            return ZERO
        booked = position.amount_actual or ZERO
        return max(position.amount_planned - booked, ZERO)

    unpaid = sum((remaining(p) for p in positions), ZERO)

    household_ids = sorted(
        {p.household_id for p in positions if p.household_id is not None},
        key=str,
    )

    return {
        "year": year,
        "month": month,
        "target_needs": targets[0],
        "target_wants": targets[1],
        "target_savings": targets[2],
        "buffer_percent": buffer_percent,
        "income": income,
        "budget": budget,
        "spent": BudgetTotals(
            needs=total(Block.NEEDS),
            wants=total(Block.WANTS),
            savings=total(Block.SAVINGS),
        ),
        "unpaid": unpaid,
        "household_ids": household_ids,
    }


async def _load_plan(session: AsyncSession, plan_id: uuid.UUID, user: User) -> Plan:
    plan = await session.get(Plan, plan_id, options=[selectinload(Plan.positions)])
    if plan is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "plan_not_found"})
    require(owns_plan(user, plan), "not_plan_owner")
    return plan


@router.get("", response_model=list[PlanSummary])
async def list_plans(
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> list[PlanSummary]:
    """Alle eigenen Monatspläne, neueste zuerst."""
    result = await session.execute(
        select(Plan)
        .where(Plan.user_id == user.id)
        .options(selectinload(Plan.positions))
        .order_by(Plan.year.desc(), Plan.month.desc())
    )
    return [
        PlanSummary(
            **_summarize(
                year=plan.year,
                month=plan.month,
                targets=(plan.target_needs, plan.target_wants, plan.target_savings),
                buffer_percent=plan.buffer_percent,
                positions=plan.positions,
            )
        )
        for plan in result.scalars().unique()
    ]


@router.post("", response_model=PlanRead, status_code=status.HTTP_201_CREATED)
async def create_plan(
    payload: PlanCreate,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> PlanRead:
    """Create a month.

    Positions are generated from every active commitment falling due in that month.
    Deliberately **no** "copy last month" — the recurring part comes from the
    commitments, one-off items are entered by hand.
    """
    existing = await session.execute(
        select(Plan).where(
            Plan.user_id == user.id,
            Plan.year == payload.year,
            Plan.month == payload.month,
        )
    )
    if existing.scalar_one_or_none() is not None:
        raise HTTPException(status.HTTP_409_CONFLICT, detail={"code": "plan_already_exists"})

    plan = Plan(user_id=user.id, year=payload.year, month=payload.month)

    commitments = await session.execute(
        select(Commitment).where(Commitment.owner_id == user.id, Commitment.active.is_(True))
    )
    for commitment in commitments.scalars():
        if not commitment.is_due_in(payload.year, payload.month):
            continue
        plan.positions.append(
            PlanPosition(
                commitment_id=commitment.id,
                household_id=commitment.household_id,
                label=commitment.name,
                amount_planned=commitment.amount,
                category=commitment.category,
                block=commitment.block,
                # The 31st does not exist in every month — this holds the clamped
                # day, not the raw one.
                due_day=commitment.effective_due_day(payload.year, payload.month),
                # Copied from the commitment, still overridable on the position.
                account_id=commitment.account_id,
                payment_method=commitment.payment_method,
                # A budget commitment becomes a budget position: no tick box, a
                # fill level fed by bookings instead.
                is_budget=commitment.type is CommitmentType.BUDGET,
                counter_account_id=commitment.counter_account_id,
                pass_through=commitment.pass_through,
            )
        )

    session.add(plan)
    await session.commit()
    await session.refresh(plan, ["positions"])
    return _plan_read(plan)


@router.get("/{year}/{month}", response_model=PlanRead)
async def get_plan(
    year: int,
    month: int,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> PlanRead:
    result = await session.execute(
        select(Plan)
        .where(Plan.user_id == user.id, Plan.year == year, Plan.month == month)
        .options(selectinload(Plan.positions))
    )
    plan = result.scalar_one_or_none()
    if plan is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "plan_not_found"})
    return _plan_read(plan)


@router.patch("/{plan_id}", response_model=PlanRead)
async def update_plan(
    plan_id: uuid.UUID,
    payload: PlanUpdate,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> PlanRead:
    """Change quotas and buffer. Guidelines, not rules."""
    plan = await _load_plan(session, plan_id, user)
    for field, value in payload.model_dump(exclude_unset=True).items():
        setattr(plan, field, value)
    await session.commit()
    await session.refresh(plan, ["positions"])
    return _plan_read(plan)


@router.post(
    "/{plan_id}/positions", response_model=PositionRead, status_code=status.HTTP_201_CREATED
)
async def create_position(
    plan_id: uuid.UUID,
    payload: PositionCreate,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> PlanPosition:
    """Create a one-off position.

    Anything recurring belongs to the commitments — they generate their own
    positions. Editing and deleting run through `/positions/{id}`.
    """
    plan = await _load_plan(session, plan_id, user)
    require(
        await can_assign_to_household(session, plan.user_id, payload.household_id),
        "not_household_member",
    )

    position = PlanPosition(plan_id=plan.id, **payload.model_dump())
    session.add(position)
    await session.commit()
    await session.refresh(position)
    return position


# --- Haushaltssicht -------------------------------------------------------


@router.get("/member/{owner_id}/{year}/{month}", response_model=MemberPlanRead)
async def get_member_plan(
    owner_id: uuid.UUID,
    year: int,
    month: int,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> MemberPlanRead:
    """The **whole** plan of a household member, private positions included.

    Not the same as the shared plan: that one shows only positions with
    `household_id` set and merges every member. Here one person stands alone, the
    way they see their own month.

    Verlangt mindestens Stufe `view` — und die gibt der Besitzer selbst, siehe
    `AccessLevel`. Private Posten sind bewusst dabei: eine Stufe, die das Buch
    zeigt, aber einen Posten verbirgt, wäre keine Vertrauensstufe, sondern eine
    Lücke — im Buch stünde die Buchung ohnehin.

    Die Route ist `/member/...`, nicht `/{owner_id}/...` — sonst käme sie
    `/{year}/{month}` in den Weg.
    """
    level = await granted_level(session, owner_id, user.id)
    require(level.rank >= AccessLevel.VIEW.rank, "no_insight_granted")

    owner = await session.get(User, owner_id)
    if owner is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "user_not_found"})

    result = await session.execute(
        select(Plan)
        .where(Plan.user_id == owner_id, Plan.year == year, Plan.month == month)
        .options(selectinload(Plan.positions))
    )
    plan = result.scalar_one_or_none()
    if plan is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "plan_not_found"})

    return MemberPlanRead(
        owner_id=owner.id,
        owner_name=owner.first_name,
        # Tells the frontend whether to offer buttons. The real check still happens
        # on the writing endpoint — this is presentation, not protection.
        may_edit=level is AccessLevel.EDIT,
        **_plan_read(plan).model_dump(),
    )


@router.get("/household/{household_id}/{year}/{month}", response_model=HouseholdPlanRead)
async def get_household_plan(
    household_id: uuid.UUID,
    year: int,
    month: int,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> PlanSummary:
    """The shared plan — composed, not stored.

    It is built from every member position carrying this `household_id`. The quotas
    come from the household, not from any single plan.
    """
    require(await is_member(session, user.id, household_id), "not_household_member")

    household = await session.get(Household, household_id)
    if household is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "household_not_found"})

    # The owner is joined in right away: "who carries what" is the point of this
    # view, and loading it per position would be an N+1.
    result = await session.execute(
        select(PlanPosition, User.id, User.first_name)
        .join(Plan, Plan.id == PlanPosition.plan_id)
        .join(User, User.id == Plan.user_id)
        .join(HouseholdMember, HouseholdMember.user_id == Plan.user_id)
        .where(
            PlanPosition.household_id == household_id,
            HouseholdMember.household_id == household_id,
            Plan.year == year,
            Plan.month == month,
        )
    )
    rows = result.unique().all()
    positions = [row[0] for row in rows]

    return HouseholdPlanRead(
        household_id=household_id,
        household_name=household.name,
        positions=[
            HouseholdPositionRead(
                **PositionRead.model_validate(position).model_dump(),
                owner_id=owner_id,
                owner_name=owner_name,
            )
            for position, owner_id, owner_name in rows
        ],
        **_summarize(
            year=year,
            month=month,
            targets=(
                household.target_needs,
                household.target_wants,
                household.target_savings,
            ),
            buffer_percent=household.buffer_percent,
            positions=positions,
        )
    )


# --- Hilfen ---------------------------------------------------------------


def _plan_read(plan: Plan) -> PlanRead:
    return PlanRead(
        id=plan.id,
        positions=[PositionRead.model_validate(p) for p in plan.positions],
        **_summarize(
            year=plan.year,
            month=plan.month,
            targets=(plan.target_needs, plan.target_wants, plan.target_savings),
            buffer_percent=plan.buffer_percent,
            positions=plan.positions,
        ),
    )
