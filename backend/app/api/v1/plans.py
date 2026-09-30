"""Monthly plans and their positions.

A plan **always belongs to one person**, never to a household. The household plan
is not a table of its own — it is the composition of every member position that
is not private. Whose plan a read covers says the query, as for accounts and the
book: nothing for your own, `?owner=` for another person's, `?household=` for the
shared view (`resolve_scope`). Individual positions live under `/positions`,
otherwise `positions` would collide with the year.
"""

import uuid
from datetime import date
from decimal import Decimal

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy import func, or_, select
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.orm import selectinload

from app.core.auth import current_active_user
from app.core.permissions import Area, require_level
from app.core.scope import (
    HouseholdMembers,
    Lens,
    eligible_member_ids,
    household_member_ids,
    may_see,
    resolve_scope,
)
from app.db.session import get_session
from app.models.account import Account
from app.models.commitment import Commitment
from app.models.enums import AccessLevel, Budget, TransactionKind
from app.models.household import Household, HouseholdMember
from app.models.plan import Plan, PlanPosition
from app.models.transaction import Transaction
from app.models.user import User
from app.schemas.flow import FlowRead
from app.schemas.plan import (
    BudgetTotals,
    HouseholdPlanRead,
    HouseholdPositionRead,
    PlanCreate,
    PlanRead,
    PlanSummary,
    PlanUpdate,
    PositionCreate,
    PositionRead,
    UnplannedTotals,
)
from app.services.flow import Source, build_flow
from app.services.hints import plan_hints
from app.services.savings_goal import amount_to_plan

router = APIRouter()

ZERO = Decimal("0.00")


async def _household_missing_members(
    session: AsyncSession, household_id: uuid.UUID, year: int, month: int
) -> list[str]:
    """First names of members already part of the household this month who have
    not created their own plan for it yet."""
    members = [
        (member_id, first_name, created_at)
        for member_id, first_name, created_at in await session.execute(
            select(HouseholdMember.user_id, User.first_name, HouseholdMember.created_at)
            .join(User, User.id == HouseholdMember.user_id)
            .where(HouseholdMember.household_id == household_id)
        )
    ]
    eligible_ids = eligible_member_ids(
        ((member_id, joined_at) for member_id, _, joined_at in members), year, month
    )
    planned_ids = set(
        (
            await session.execute(
                select(Plan.user_id).where(
                    Plan.user_id.in_([member_id for member_id, _, _ in members]),
                    Plan.year == year,
                    Plan.month == month,
                )
            )
        ).scalars()
    )
    return sorted(
        first_name
        for member_id, first_name, _ in members
        if member_id in eligible_ids and member_id not in planned_ids
    )


def _summarize(
    *,
    year: int,
    month: int,
    targets: tuple[Decimal, Decimal, Decimal],
    positions: list[PlanPosition],
) -> dict:
    """The figures the overview and the detail page show.

    `distributable` is the income — the basis the quotas are computed on.
    Not to be confused with what is left to allocate: that is the remainder of it
    and is derived in the frontend, where it updates live anyway.
    """
    # Pass-through positions stay out — that money was never there to distribute.
    # Counting it would inflate `distributable` and the savings quota with it,
    # although the household has not a cent more to spend.
    counting = [p for p in positions if not p.pass_through]

    income = sum((p.amount_planned for p in counting if p.budget is Budget.INCOME), ZERO)
    distributable = income

    def total(budget: Budget) -> Decimal:
        return sum((p.amount_planned for p in counting if p.budget is budget), ZERO)

    def remaining(position: PlanPosition) -> Decimal:
        """What is still to go out for this position.

        Ticked off means done. Otherwise what counts is the planned amount minus
        what the book already records: a 600 budget with 127.50 of purchases booked
        still expects 472.50, not 600.

        Never negative — overspending a budget does not leave anything over.
        """
        if position.budget is Budget.INCOME or position.paid_at is not None:
            return ZERO
        # A limit is never "still to go out": it has no tick, it runs until the
        # month is over. Counting its remainder would keep every month looking
        # unfinished right up to the 31st.
        if position.is_limit:
            return ZERO
        # A pass-through position stands and falls with its own income. Counting it
        # here would make the month look underfunded although no money of your own
        # is missing — only the forwarding is still pending.
        if position.pass_through:
            return ZERO
        booked = position.amount_actual or ZERO
        return max(position.amount_planned - booked, ZERO)

    unpaid = sum((remaining(p) for p in positions), ZERO)

    return {
        "year": year,
        "month": month,
        "target_needs": targets[0],
        "target_wants": targets[1],
        "target_savings": targets[2],
        "income": income,
        "distributable": distributable,
        "spent": BudgetTotals(
            needs=total(Budget.NEEDS),
            wants=total(Budget.WANTS),
            savings=total(Budget.SAVINGS),
        ),
        "unpaid": unpaid,
    }


async def _unplanned(
    session: AsyncSession, owner_ids: list[uuid.UUID], year: int, month: int
) -> UnplannedTotals:
    """Bookings of the plan month that hang on no position, by their own budget.

    The booking's stored `budget` decides, not the category's suggestion: the person
    already answered that when booking. A transfer is no spending, and a carry-over
    states a balance — both stay out. Income is reported apart (#240).
    """
    totals = {budget: ZERO for budget in Budget}
    if owner_ids:
        rows = await session.execute(
            select(Transaction.budget, func.sum(Transaction.amount))
            .where(
                Transaction.owner_id.in_(owner_ids),
                Transaction.plan_year == year,
                Transaction.plan_month == month,
                Transaction.position_id.is_(None),
                Transaction.counter_account_id.is_(None),
                Transaction.kind != TransactionKind.CARRY_OVER,
                Transaction.budget.is_not(None),
            )
            .group_by(Transaction.budget)
        )
        for budget, total in rows.all():
            totals[budget] = total
    return UnplannedTotals(
        income=totals[Budget.INCOME],
        needs=totals[Budget.NEEDS],
        wants=totals[Budget.WANTS],
        savings=totals[Budget.SAVINGS],
    )


async def _used_position_ids(
    session: AsyncSession, position_ids: list[uuid.UUID]
) -> set[uuid.UUID]:
    """Which of these positions already have a booking hanging off them."""
    if not position_ids:
        return set()
    rows = await session.execute(
        select(Transaction.position_id).where(Transaction.position_id.in_(position_ids)).distinct()
    )
    return set(rows.scalars())


async def _load_plan(
    session: AsyncSession,
    plan_id: uuid.UUID,
    user: User,
    needs: AccessLevel = AccessLevel.EDIT,
) -> Plan:
    """A month, either your own or one you stand in for.

    Same ladder as `positions.py::_load`: your own always, somebody else at the
    level in `Area.PLAN` the owner granted — `create` to add a position, `edit`
    to change the month itself.
    """
    plan = await session.get(Plan, plan_id, options=[selectinload(Plan.positions)])
    if plan is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "plan_not_found"})

    await require_level(session, plan.user_id, user, Area.PLAN, needs)
    return plan


@router.get("", response_model=list[PlanSummary])
async def list_plans(
    owner: uuid.UUID | None = None,
    household: uuid.UUID | None = None,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> list[PlanSummary]:
    """Monatspläne, neueste zuerst.

    Ohne `owner` die eigenen, mit `owner` die einer Person, die mindestens `view`
    auf `Area.PLAN` gegeben hat — dieselbe Regel wie beim einzelnen Monat. Mit
    `household` die Monate des Haushalts (`_list_household_plans`).
    """
    scope = await resolve_scope(
        session, user, Area.PLAN, owner=owner, household=household, members=HouseholdMembers.ALL
    )
    if scope.lens is Lens.HOUSEHOLD:
        assert household is not None
        return await _list_household_plans(session, household)
    (owner_id,) = scope.owner_ids

    result = await session.execute(
        select(Plan)
        .where(Plan.user_id == owner_id)
        .options(selectinload(Plan.positions))
        .order_by(Plan.year.desc(), Plan.month.desc())
    )
    plans = list(result.scalars().unique())
    used = await _used_position_ids(
        session, [position.id for plan in plans for position in plan.positions]
    )
    return [
        PlanSummary(
            deletable=not any(position.id in used for position in plan.positions),
            **_summarize(
                year=plan.year,
                month=plan.month,
                targets=(plan.target_needs, plan.target_wants, plan.target_savings),
                positions=plan.positions,
            ),
        )
        for plan in plans
    ]


async def _list_household_plans(
    session: AsyncSession, household_id: uuid.UUID
) -> list[PlanSummary]:
    """The months that carry this household, newest first.

    Feeds "Alle Pläne" on the household plan: the household owns no plan of its
    own to list, so this reads the months off every member's plans. A month
    belongs on this list only once **every member already part of the household
    that month** has created their own plan for it — even with zero shared
    positions, since that is exactly the state `_household_plan` shows in full
    rather than behind a notice. A member who joins later does not make earlier
    months incomplete; they count from the month they join in. A month where an
    already-eligible member has not planned yet does not appear at all, no
    matter how many shared positions the others already carry.

    The caller has checked membership (`resolve_scope`).
    """
    household = await session.get(Household, household_id)
    if household is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "household_not_found"})

    members = [
        (member_id, created_at)
        for member_id, created_at in await session.execute(
            select(HouseholdMember.user_id, HouseholdMember.created_at).where(
                HouseholdMember.household_id == household_id
            )
        )
    ]
    member_ids = [member_id for member_id, _ in members]

    owners_by_month: dict[tuple[int, int], set[uuid.UUID]] = {}
    for year, month, owner_id in await session.execute(
        select(Plan.year, Plan.month, Plan.user_id).where(Plan.user_id.in_(member_ids))
    ):
        owners_by_month.setdefault((year, month), set()).add(owner_id)

    complete_months = {
        key
        for key, owners in owners_by_month.items()
        if (eligible := eligible_member_ids(members, *key)) and eligible <= owners
    }
    if not complete_months:
        return []

    result = await session.execute(
        select(PlanPosition, Plan.year, Plan.month)
        .join(Plan, Plan.id == PlanPosition.plan_id)
        .where(
            PlanPosition.is_private.is_(False),
            Plan.user_id.in_(member_ids),
        )
    )

    months: dict[tuple[int, int], list[PlanPosition]] = {key: [] for key in complete_months}
    for position, year, month in result.unique().all():
        if (year, month) in months:
            months[(year, month)].append(position)

    return [
        PlanSummary(
            **_summarize(
                year=year,
                month=month,
                targets=(
                    household.target_needs,
                    household.target_wants,
                    household.target_savings,
                ),
                positions=positions,
            )
        )
        for (year, month), positions in sorted(months.items(), reverse=True)
    ]


@router.post("", response_model=PlanRead, status_code=status.HTTP_201_CREATED)
async def create_plan(
    payload: PlanCreate,
    owner: uuid.UUID | None = None,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> PlanRead:
    """Create a month.

    Positions are generated from every commitment falling due in that month.
    Deliberately **no** "copy last month" — the recurring part comes from the
    commitments, one-off items are entered by hand.

    Without `owner` your own month, with `owner` that of a person who granted
    `create` in `Area.PLAN`. **Everything is read from the owner**, not from
    whoever is calling: the plan, the commitments it grows from, the check for a
    month that already exists. Taking the caller for any one of those would
    quietly build the wrong person a month out of the wrong contracts.
    """
    owner_id = owner or user.id
    await require_level(session, owner_id, user, Area.PLAN, AccessLevel.CREATE)

    existing = await session.execute(
        select(Plan).where(
            Plan.user_id == owner_id,
            Plan.year == payload.year,
            Plan.month == payload.month,
        )
    )
    if existing.scalar_one_or_none() is not None:
        raise HTTPException(status.HTTP_409_CONFLICT, detail={"code": "plan_already_exists"})

    # The personal default of the owner, copied — a snapshot like the positions.
    owner_row = await session.get(User, owner_id)
    plan = Plan(
        user_id=owner_id,
        year=payload.year,
        month=payload.month,
        target_needs=owner_row.target_needs,
        target_wants=owner_row.target_wants,
        target_savings=owner_row.target_savings,
    )

    commitments = await session.execute(select(Commitment).where(Commitment.owner_id == owner_id))
    for commitment in commitments.scalars():
        if not commitment.is_due_in(payload.year, payload.month):
            continue
        # A savings goal that has reached its target plans nothing; one that is
        # less than a rate short plans only the rest (#87).
        amount = await amount_to_plan(session, commitment)
        if amount is None:
            continue
        plan.positions.append(
            PlanPosition(
                commitment_id=commitment.id,
                is_private=commitment.is_private,
                label=commitment.name,
                amount_planned=amount,
                category=commitment.category,
                budget=commitment.budget,
                # The 31st does not exist in every month — this holds the clamped
                # day, not the raw one.
                due_day=commitment.effective_due_day(payload.year, payload.month),
                # Copied from the commitment, still overridable on the position.
                account_id=commitment.account_id,
                payment_method=commitment.payment_method,
                # A limit stays a limit in every month it is planned: no tick
                # box, a fill level fed by bookings instead.
                is_limit=commitment.is_limit,
                counter_account_id=commitment.counter_account_id,
                pass_through=commitment.pass_through,
            )
        )

    session.add(plan)
    await session.commit()
    await session.refresh(plan, ["positions"])
    return await _plan_read(session, plan, user)


@router.get("/{year}/{month}", response_model=PlanRead | HouseholdPlanRead)
async def get_plan(
    year: int,
    month: int,
    owner: uuid.UUID | None = None,
    household: uuid.UUID | None = None,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> PlanRead | HouseholdPlanRead:
    """One month, whole — private positions included.

    Without `owner` your own month. With `owner` that person's, which needs at least
    `view` on `Area.PLAN`; the level comes from them.

    Private positions are deliberately part of it. A level that shows the book but
    hides a position would not be a degree of trust but a gap — the booking would
    stand in the book anyway.

    Not the same as the household plan (`household`): that one shows only
    positions that are not private and merges every member.
    """
    scope = await resolve_scope(
        session, user, Area.PLAN, owner=owner, household=household, members=HouseholdMembers.ALL
    )
    if scope.lens is Lens.HOUSEHOLD:
        assert household is not None
        return await _household_plan(session, household, year, month)
    (owner_id,) = scope.owner_ids

    plan = await _month_of(session, owner_id, year, month)
    return await _plan_read(session, plan, user)


@router.delete("/{year}/{month}", status_code=status.HTTP_204_NO_CONTENT)
async def delete_plan(
    year: int,
    month: int,
    owner: uuid.UUID | None = None,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> None:
    """Delete a month nobody has booked against yet — a typo, not history.

    Same rule as ending a commitment or deleting an account (#139): allowed only
    while nothing refers to it yet. A booking's link to its position is `SET
    NULL`, not `RESTRICT` — deleting one position must not take a booking's
    history with it — so here the check itself is the only guard. The position
    rows are locked first, so a booking cannot slip in between the check and the
    delete.

    Without `owner` your own month. With `owner` that of a person who granted
    `delete` in `Area.PLAN` — a step above `edit`, like everywhere else deleting
    asks for more than changing.
    """
    owner_id = owner or user.id
    await require_level(session, owner_id, user, Area.PLAN, AccessLevel.DELETE)

    plan = await _month_of(session, owner_id, year, month)

    position_ids = [position.id for position in plan.positions]
    if position_ids:
        await session.execute(
            select(PlanPosition.id).where(PlanPosition.id.in_(position_ids)).with_for_update()
        )
    if await _used_position_ids(session, position_ids):
        raise HTTPException(status.HTTP_409_CONFLICT, detail={"code": "plan_has_transactions"})

    await session.delete(plan)
    await session.commit()


@router.patch("/{plan_id}", response_model=PlanRead)
async def update_plan(
    plan_id: uuid.UUID,
    payload: PlanUpdate,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> PlanRead:
    """Change the quotas. Guidelines, not rules."""
    plan = await _load_plan(session, plan_id, user)
    for field, value in payload.model_dump(exclude_unset=True).items():
        setattr(plan, field, value)
    await session.commit()
    await session.refresh(plan, ["positions"])
    return await _plan_read(session, plan, user)


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
    plan = await _load_plan(session, plan_id, user, AccessLevel.CREATE)
    position = PlanPosition(plan_id=plan.id, **payload.model_dump())
    session.add(position)
    await session.commit()
    await session.refresh(position)
    return position


# --- Haushaltssicht -------------------------------------------------------


async def _household_plan(
    session: AsyncSession, household_id: uuid.UUID, year: int, month: int
) -> HouseholdPlanRead:
    """The shared plan — composed, not stored.

    It is built from every non-private position of every member. The quotas
    come from the household, not from any single plan.

    Shown whole only once **every member already part of the household this
    month** has created their own plan for it — same rule as
    `_list_household_plans`. A member who joins later does not make earlier
    months incomplete. Otherwise this is a half plan, not the household's plan:
    `positions` and `hints` come back empty and `missing_members` names who is
    still missing, so the frontend shows a calm notice instead of numbers nobody
    agreed to yet.

    The caller has checked membership (`resolve_scope`).
    """
    household = await session.get(Household, household_id)
    if household is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "household_not_found"})

    missing_members = await _household_missing_members(session, household_id, year, month)

    positions: list[PlanPosition] = []
    household_positions: list[HouseholdPositionRead] = []
    if not missing_members:
        rows = await _household_positions(session, household_id, year, month)
        positions = [row[0] for row in rows]
        household_positions = [
            HouseholdPositionRead(
                **PositionRead.model_validate(position).model_dump(),
                owner_id=owner_id,
                owner_name=owner_name,
            )
            for position, owner_id, owner_name in rows
        ]

    return HouseholdPlanRead(
        household_id=household_id,
        household_name=household.name,
        hints=plan_hints(year, month, positions),
        positions=household_positions,
        # A half plan shows nothing, and that goes for its sums too.
        unplanned=await _unplanned(
            session,
            [] if missing_members else await household_member_ids(session, household_id),
            year,
            month,
        ),
        missing_members=missing_members,
        **_summarize(
            year=year,
            month=month,
            targets=(
                household.target_needs,
                household.target_wants,
                household.target_savings,
            ),
            positions=positions,
        ),
    )


# --- Verlauf --------------------------------------------------------------


async def _sources(
    session: AsyncSession,
    rows: list[tuple[uuid.UUID, list[PlanPosition]]],
    year: int,
    month: int,
    *,
    with_manual: bool,
    show_account: bool = True,
    with_carry_over: bool = False,
) -> list[Source]:
    """One source per person: default account, positions and the bookings the
    flow needs. Manual bookings (no position) are read only for the own plan.
    `show_account` is False when the viewer has no insight into the accounts: the
    account is then not named. `with_carry_over` reads the carry-over of the default
    account as the start of that person's curve."""
    owner_ids = [owner_id for owner_id, _ in rows]
    accounts = {
        account.owner_id: account
        for account in (
            await session.execute(
                select(Account).where(Account.owner_id.in_(owner_ids), Account.is_default)
            )
        ).scalars()
    }
    position_ids = [p.id for _, positions in rows for p in positions]
    first = date(year, month, 1)

    # A carry-over states the balance the month starts with. It is the curve's
    # start, never a movement in it, so it is read on its own below.
    conditions = [Transaction.position_id.in_(position_ids)]
    if with_manual and accounts:
        default_ids = [account.id for account in accounts.values()]
        conditions.append(
            (Transaction.position_id.is_(None))
            & Transaction.owner_id.in_(owner_ids)
            & (Transaction.plan_year == year)
            & (Transaction.plan_month == month)
            & (
                Transaction.account_id.in_(default_ids)
                | Transaction.counter_account_id.in_(default_ids)
            )
        )
    transactions = (
        (
            await session.execute(
                select(Transaction).where(
                    or_(*conditions), Transaction.kind != TransactionKind.CARRY_OVER
                )
            )
        )
        .scalars()
        .all()
    )
    carry_overs = {}
    if with_carry_over and accounts:
        carry_overs = {
            tx.account_id: tx.amount
            for tx in (
                await session.execute(
                    select(Transaction).where(
                        Transaction.kind == TransactionKind.CARRY_OVER,
                        Transaction.account_id.in_([a.id for a in accounts.values()]),
                        Transaction.occurred_on == first,
                    )
                )
            ).scalars()
        }

    sources = []
    for owner_id, positions in rows:
        account = accounts.get(owner_id)
        ids = {p.id for p in positions}
        sources.append(
            Source(
                account_id=account.id if account and show_account else None,
                account_name=account.name if account and show_account else None,
                start=carry_overs.get(account.id, ZERO) if account else ZERO,
                positions=positions,
                transactions=[
                    tx
                    for tx in transactions
                    if tx.position_id in ids or (tx.position_id is None and tx.owner_id == owner_id)
                ],
            )
        )
    return sources


@router.get("/{year}/{month}/flow", response_model=FlowRead)
async def get_flow(
    year: int,
    month: int,
    owner: uuid.UUID | None = None,
    household: uuid.UUID | None = None,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> FlowRead:
    """The flow of one month on the default account of the plan owner.

    Same access as `get_plan`. Limits follow the setting of **the viewer**, not the
    owner: it is a question of the view, so whoever looks decides. With `household`
    the household's curve (`_household_flow`).
    """
    scope = await resolve_scope(
        session, user, Area.PLAN, owner=owner, household=household, members=HouseholdMembers.ALL
    )
    if scope.lens is Lens.HOUSEHOLD:
        assert household is not None
        return await _household_flow(session, user, household, year, month)
    (owner_id,) = scope.owner_ids
    # Manual bookings belong to the book, the account name and its start balance
    # to the accounts, not to the plan: for somebody else's plan each needs its
    # own grant.
    sees_book = await may_see(session, owner_id, user, Area.BOOK)
    sees_accounts = await may_see(session, owner_id, user, Area.ACCOUNTS)

    plan = await _month_of(session, owner_id, year, month)

    sources = await _sources(
        session,
        [(owner_id, list(plan.positions))],
        year,
        month,
        with_manual=sees_book,
        show_account=sees_accounts,
        with_carry_over=sees_accounts,
    )
    return build_flow(sources, year, month, user.flow_limits_by, merged=False)


async def _household_flow(
    session: AsyncSession, user: User, household_id: uuid.UUID, year: int, month: int
) -> FlowRead:
    """One curve over the household positions of every member, like the household
    plan itself: a lens, nothing stored. It answers whether it works out together,
    not where money is missing.

    The curve starts at zero, not at a carry-over: the household owns no account, and
    a member's balance is theirs to share, not the household's. It shows the change
    the shared positions bring.

    Gated the same way as `_household_plan`: once a member already part of the
    household this month has not created their own plan yet, this is an empty
    curve with `missing_members` set, not a curve half the household agreed to.

    The caller has checked membership (`resolve_scope`).
    """

    missing_members = await _household_missing_members(session, household_id, year, month)
    if missing_members:
        return FlowRead(
            year=year,
            month=month,
            flow_limits_by=user.flow_limits_by,
            start=ZERO,
            entries=[],
            days=[],
            hints=[],
            missing_members=missing_members,
        )

    by_owner: dict[uuid.UUID, list[PlanPosition]] = {}
    for position, owner_id, _ in await _household_positions(session, household_id, year, month):
        by_owner.setdefault(owner_id, []).append(position)

    sources = await _sources(session, list(by_owner.items()), year, month, with_manual=False)
    return build_flow(sources, year, month, user.flow_limits_by, merged=True)


# --- Hilfen ---------------------------------------------------------------


async def _month_of(session: AsyncSession, owner_id: uuid.UUID, year: int, month: int) -> Plan:
    """One person's month with its positions, or `plan_not_found`. The caller has
    checked the right to it."""
    result = await session.execute(
        select(Plan)
        .where(Plan.user_id == owner_id, Plan.year == year, Plan.month == month)
        .options(selectinload(Plan.positions))
    )
    plan = result.scalar_one_or_none()
    if plan is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "plan_not_found"})
    return plan


async def _household_positions(
    session: AsyncSession, household_id: uuid.UUID, year: int, month: int
) -> list[tuple[PlanPosition, uuid.UUID, str]]:
    """Every non-private position of every member in one month, with who carries it.

    What the household plan and its flow are built from (decision 48). The owner is
    joined in right away: "who carries what" is the point of the household view, and
    loading it per position would be an N+1.
    """
    result = await session.execute(
        select(PlanPosition, User.id, User.first_name)
        .join(Plan, Plan.id == PlanPosition.plan_id)
        .join(User, User.id == Plan.user_id)
        .join(HouseholdMember, HouseholdMember.user_id == Plan.user_id)
        .where(
            PlanPosition.is_private.is_(False),
            HouseholdMember.household_id == household_id,
            Plan.year == year,
            Plan.month == month,
        )
    )
    return [(position, owner_id, name) for position, owner_id, name in result.unique().all()]


async def _plan_read(session: AsyncSession, plan: Plan, viewer: User) -> PlanRead:
    used = await _used_position_ids(session, [position.id for position in plan.positions])
    # Unplanned bookings are the owner's book, not the plan: somebody else sees them
    # only with their own grant on the book, like the flow's manual bookings.
    sees_bookings = await may_see(session, plan.user_id, viewer, Area.BOOK)
    return PlanRead(
        id=plan.id,
        hints=plan_hints(plan.year, plan.month, plan.positions),
        positions=[PositionRead.model_validate(p) for p in plan.positions],
        deletable=not used,
        unplanned=await _unplanned(
            session, [plan.user_id] if sees_bookings else [], plan.year, plan.month
        ),
        **_summarize(
            year=plan.year,
            month=plan.month,
            targets=(plan.target_needs, plan.target_wants, plan.target_savings),
            positions=plan.positions,
        ),
    )
