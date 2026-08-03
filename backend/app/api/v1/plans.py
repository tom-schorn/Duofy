"""Monatspläne und ihre Posten.

Ein Plan gehört **immer einer Person**, nie einem Haushalt. Der Haushaltsplan
ist keine eigene Tabelle — er ist die Zusammenstellung aller Posten aller
Mitglieder, bei denen `household_id` gesetzt ist. Deshalb gibt es hier zwei
Wege zur selben Darstellung: `/plans/...` für den eigenen Plan,
`/plans/household/...` für die gemeinsame Sicht. Einzelne Posten liegen
unter `/positions` — sonst kollidiert `positions` mit der Jahreszahl.
"""

import uuid
from datetime import UTC, datetime
from decimal import Decimal

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.orm import selectinload

from app.core.auth import current_active_user
from app.core.permissions import (
    can_assign_to_household,
    is_member,
    owns_plan,
    require,
)
from app.db.session import get_session
from app.models.commitment import Commitment
from app.models.enums import Block, CommitmentType, PlanStatus
from app.models.household import Household, HouseholdMember
from app.models.plan import Plan, PlanPosition
from app.models.user import User
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
)

router = APIRouter()

ZERO = Decimal("0.00")


def _summarize(
    *,
    year: int,
    month: int,
    status_: PlanStatus,
    targets: tuple[Decimal, Decimal, Decimal],
    buffer_percent: Decimal,
    positions: list[PlanPosition],
) -> dict:
    """Die Kennzahlen, die Übersicht und Detailseite zeigen.

    `budget` ist Einnahmen minus Puffer — die Grundlage für die Quoten.
    Nicht zu verwechseln mit „Verplanbar": das ist der noch freie Rest davon
    und wird im Frontend gebildet, weil es dort ohnehin live mitläuft.
    """
    # Durchlaufende Posten bleiben außen vor — sie sind nie Budget gewesen.
    # Die Nebenkostenrückzahlung kommt an und wird sofort weggelegt: zählte man
    # sie mit, wüchse das Budget um 1.139 € und die Sparquote gleich mit,
    # obwohl der Haushalt keinen Cent mehr zu verteilen hat.
    counting = [p for p in positions if not p.pass_through]

    income = sum((p.amount_planned for p in counting if p.block is Block.INCOME), ZERO)
    budget = income - (income * buffer_percent / 100)

    def total(block: Block) -> Decimal:
        return sum((p.amount_planned for p in counting if p.block is block), ZERO)

    def remaining(position: PlanPosition) -> Decimal:
        """Was von diesem Posten noch rausgeht.

        Abgehakt heißt erledigt. Sonst zählt, was vom geplanten Betrag noch
        nicht im Haushaltsbuch steht: bei 600 € Lebensmittel und 127,50 €
        erfassten Einkäufen sind noch 472,50 € zu erwarten, nicht 600 €.

        Nie negativ — wer sein Budget überzieht, hat nichts „übrig".
        """
        if position.block is Block.INCOME or position.paid_at is not None:
            return ZERO
        # Ein durchlaufender Posten steht und fällt mit seiner Einnahme.
        # Bliebe er hier drin, sähe der Monat unterdeckt aus, obwohl kein
        # eigenes Geld fehlt — nur die Weiterleitung steht noch aus.
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
        "status": status_,
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
                status_=plan.status,
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
    """Einen Monat anlegen.

    Dabei entstehen die Posten aus allen aktiven Verpflichtungen, die in
    diesem Monat fällig werden. **Kein** „Vormonat übernehmen" — das
    Wiederkehrende kommt aus den Verträgen, Einzelposten schreibt man von Hand.
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
                # Der 31. existiert nicht in jedem Monat — hier steht der
                # bereits abgeklemmte Tag, nicht die 31.
                due_day=commitment.effective_due_day(payload.year, payload.month),
                # Kommen vom Vertrag, bleiben im Posten überschreibbar.
                account_id=commitment.account_id,
                payment_method=commitment.payment_method,
                # „Setze ich selbst" wird zum Budget-Posten: kein Haken,
                # stattdessen ein Füllstand aus den Buchungen.
                is_budget=commitment.type is CommitmentType.BUDGET,
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
    """Quoten und Puffer ändern. Richtwerte, keine Regel."""
    plan = await _load_plan(session, plan_id, user)
    for field, value in payload.model_dump(exclude_unset=True).items():
        setattr(plan, field, value)
    await session.commit()
    await session.refresh(plan, ["positions"])
    return _plan_read(plan)


@router.post("/{plan_id}/confirm", response_model=PlanRead)
async def confirm_plan(
    plan_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> PlanRead:
    """Den Monat bestätigen — ab hier läuft er.

    TODO: Im Haushalt müssen **beide** bestätigen. Dafür fehlt eine Tabelle,
    die je Mitglied festhält, wer zugestimmt hat — `Plan.confirmed_at` kennt
    nur den eigenen Plan. Solange das fehlt, bestätigt jeder für sich.
    """
    plan = await _load_plan(session, plan_id, user)
    plan.status = PlanStatus.CONFIRMED
    plan.confirmed_at = datetime.now(UTC)
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
    """Einen Einmal-Posten anlegen.

    Wiederkehrendes gehört zu den Verträgen — die erzeugen ihre Posten selbst.
    Ändern und Löschen laufen über `/positions/{id}`.
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


@router.get("/household/{household_id}/{year}/{month}", response_model=HouseholdPlanRead)
async def get_household_plan(
    household_id: uuid.UUID,
    year: int,
    month: int,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> PlanSummary:
    """Der gemeinsame Plan — zusammengesetzt, nicht gespeichert.

    Er entsteht aus allen Posten aller Mitglieder mit dieser `household_id`.
    Die Quoten kommen vom Haushalt, nicht von einem einzelnen Plan.
    """
    require(await is_member(session, user.id, household_id), "not_household_member")

    household = await session.get(Household, household_id)
    if household is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, detail={"code": "household_not_found"})

    # Der Besitzer kommt gleich mit: „wer trägt was" ist der Zweck dieser Sicht,
    # und ein Nachladen pro Posten wäre ein N+1.
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
            # Ein zusammengesetzter Plan hat keinen eigenen Status — er ist
            # bestätigt, wenn alle Einzelpläne es sind. Bis die Zustimmung je
            # Mitglied existiert, steht hier Entwurf.
            status_=PlanStatus.DRAFT,
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
        confirmed_at=plan.confirmed_at,
        positions=[PositionRead.model_validate(p) for p in plan.positions],
        **_summarize(
            year=plan.year,
            month=plan.month,
            status_=plan.status,
            targets=(plan.target_needs, plan.target_wants, plan.target_savings),
            buffer_percent=plan.buffer_percent,
            positions=plan.positions,
        ),
    )
