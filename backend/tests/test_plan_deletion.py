"""Deleting a monthly plan until something is booked against it (#219).

Same rule as ending a commitment or deleting an account (#139): a month
nobody has booked against yet is a typo, not history. Once a booking hangs on
one of its positions the month stays — a booking's link to its position is
`SET NULL`, not `RESTRICT`, so nothing else would stop it from silently losing
that link.
"""

from datetime import date
from decimal import Decimal

from httpx import AsyncClient
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.enums import AccessLevel, Budget, Category
from app.models.plan import Plan, PlanPosition
from app.models.transaction import Transaction
from tests.test_delegation import grant_area, make_plan, pair, sign_in  # noqa: F401
from tests.test_transfers import make_account


async def add_position(session: AsyncSession, plan: Plan) -> PlanPosition:
    position = PlanPosition(
        plan_id=plan.id,
        label="Rent",
        amount_planned=Decimal("890.00"),
        category=Category.HOUSING_RENT,
        budget=Budget.NEEDS,
        due_day=1,
    )
    session.add(position)
    await session.commit()
    return position


async def book(session: AsyncSession, owner, account, position: PlanPosition) -> None:
    session.add(
        Transaction(
            owner_id=owner.id,
            account_id=account.id,
            occurred_on=date(2026, 9, 5),
            plan_year=2026,
            plan_month=9,
            amount=Decimal("890.00"),
            note="Rent",
            category=Category.HOUSING_RENT,
            budget=Budget.NEEDS,
            position_id=position.id,
        )
    )
    await session.commit()


async def test_an_owner_deletes_their_unused_plan(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
):
    owner, _, _ = pair
    plan = await make_plan(session, owner)
    await add_position(session, plan)
    sign_in(owner)

    listed = await client.get("/api/v1/plans")
    assert [row["deletable"] for row in listed.json()] == [True]

    response = await client.delete("/api/v1/plans/2026/9")
    assert response.status_code == 204

    assert (await session.execute(select(Plan))).scalars().first() is None
    assert (await client.get("/api/v1/plans/2026/9")).status_code == 404


async def test_a_plan_with_a_booking_is_refused_with_its_code(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
):
    owner, _, _ = pair
    plan = await make_plan(session, owner)
    position = await add_position(session, plan)
    account = await make_account(session, owner, "Checking")
    await book(session, owner, account, position)
    sign_in(owner)

    detail = await client.get("/api/v1/plans/2026/9")
    assert detail.json()["deletable"] is False

    response = await client.delete("/api/v1/plans/2026/9")
    assert response.status_code == 409
    assert response.json()["detail"]["code"] == "plan_has_transactions"

    # Refused, so the plan and its position are still there.
    assert (await session.execute(select(Plan))).scalars().one() is not None


async def test_someone_elses_unused_plan_needs_the_delete_right(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
):
    owner, helper, household = pair
    await make_plan(session, owner)
    await grant_area(session, household, owner, "plan", AccessLevel.EDIT)
    sign_in(helper)

    response = await client.delete(f"/api/v1/plans/2026/9?owner={owner.id}")
    assert response.status_code == 403
    assert response.json()["detail"]["code"] == "no_delete_granted"


async def test_a_member_with_the_delete_right_deletes_an_unused_plan(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
):
    owner, helper, household = pair
    await make_plan(session, owner)
    await grant_area(session, household, owner, "plan", AccessLevel.DELETE)
    sign_in(helper)

    response = await client.delete(f"/api/v1/plans/2026/9?owner={owner.id}")
    assert response.status_code == 204


async def test_a_member_with_the_delete_right_is_still_refused_when_a_plan_has_bookings(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
):
    owner, helper, household = pair
    plan = await make_plan(session, owner)
    position = await add_position(session, plan)
    account = await make_account(session, owner, "Checking")
    await book(session, owner, account, position)
    await grant_area(session, household, owner, "plan", AccessLevel.DELETE)
    sign_in(helper)

    response = await client.delete(f"/api/v1/plans/2026/9?owner={owner.id}")
    assert response.status_code == 409
    assert response.json()["detail"]["code"] == "plan_has_transactions"


async def test_a_missing_plan_is_not_found(
    client: AsyncClient, session: AsyncSession, pair  # noqa: F811
):
    owner, _, _ = pair
    sign_in(owner)

    response = await client.delete("/api/v1/plans/2026/9")
    assert response.status_code == 404
    assert response.json()["detail"]["code"] == "plan_not_found"
