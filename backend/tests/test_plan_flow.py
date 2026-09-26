"""The flow of a plan month (#93): which account, plan versus bookings, limits.

The curve is one account: the default account of the plan owner. Plan until
something is booked, then bookings; limits follow the viewer's switch. Nothing is
estimated, and the curve starts at zero.
"""

from datetime import UTC, date, datetime
from decimal import Decimal

import pytest
from httpx import AsyncClient
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.account import Account
from app.models.enums import AccessLevel, AccountType, Budget, Category, FlowLimitsBy
from app.models.plan import Plan, PlanPosition
from app.models.transaction import Transaction
from app.models.user import User
from app.schemas.user import UserUpdate
from tests.test_area_permissions import add_member, make_household, make_user
from tests.test_delegation import grant_area, sign_in


async def make_account(
    session: AsyncSession, owner: User, name: str, *, default: bool = False
) -> Account:
    account = Account(
        owner_id=owner.id,
        name=name,
        type=AccountType.CHECKING,
        opening_date=date(2026, 1, 1),
        is_default=default,
    )
    session.add(account)
    await session.flush()
    return account


@pytest.fixture
async def owner(session: AsyncSession) -> User:
    user = await make_user(session, "Owner")
    await session.commit()
    await session.refresh(user)
    sign_in(user)
    return user


@pytest.fixture
async def main_account(session: AsyncSession, owner: User) -> Account:
    account = await make_account(session, owner, "Girokonto", default=True)
    await session.commit()
    return account


async def make_plan(session: AsyncSession, owner: User, *, month: int = 9) -> Plan:
    plan = Plan(user_id=owner.id, year=2026, month=month)
    session.add(plan)
    await session.flush()
    return plan


def position(plan: Plan, label: str, amount: str, due_day: int = 1, **kwargs) -> PlanPosition:
    return PlanPosition(
        plan_id=plan.id,
        label=label,
        amount_planned=Decimal(amount),
        category=kwargs.pop("category", Category.HOUSING_RENT),
        budget=kwargs.pop("budget", Budget.NEEDS),
        due_day=due_day,
        **kwargs,
    )


def booking(owner: User, account: Account, amount: str, day: date, **kwargs) -> Transaction:
    return Transaction(
        owner_id=owner.id,
        account_id=account.id,
        amount=Decimal(amount),
        occurred_on=day,
        category=kwargs.pop("category", Category.HOUSING_RENT),
        budget=kwargs.pop("budget", Budget.NEEDS),
        **kwargs,
    )


async def flow(client: AsyncClient, url: str = "/api/v1/plans/2026/9/flow") -> dict:
    response = await client.get(url)
    assert response.status_code == 200, response.text
    return response.json()


def steps(body: dict) -> list[tuple[int, str]]:
    return [(entry["day"], entry["amount"]) for entry in body["entries"]]


async def test_the_curve_starts_at_zero_and_a_plan_counts_on_its_due_day(
    client: AsyncClient, session: AsyncSession, owner: User, main_account: Account
):
    plan = await make_plan(session, owner)
    session.add_all(
        [
            position(plan, "Rent", "800.00", 1),
            position(
                plan, "Salary", "2000.00", 15, budget=Budget.INCOME, category=Category.INCOME_EARNED
            ),
        ]
    )
    await session.commit()

    body = await flow(client)

    assert Decimal(body["start"]) == 0
    assert steps(body) == [(1, "-800.00"), (15, "2000.00")]
    assert [Decimal(d["balance"]) for d in body["days"]][:2] == [Decimal("-800"), Decimal("-800")]
    assert Decimal(body["days"][14]["balance"]) == Decimal("1200")
    assert [e["kind"] for e in body["entries"]] == ["plan", "plan"]


async def test_positions_on_other_accounts_stay_out_and_those_without_an_account_count(
    client: AsyncClient, session: AsyncSession, owner: User, main_account: Account
):
    other = await make_account(session, owner, "Tagesgeld")
    plan = await make_plan(session, owner)
    session.add_all(
        [
            position(plan, "No account", "10.00"),
            position(plan, "On default", "20.00", account_id=main_account.id),
            position(plan, "On other", "40.00", account_id=other.id),
        ]
    )
    await session.commit()

    assert sorted(a for _, a in steps(await flow(client))) == ["-10.00", "-20.00"]


async def test_a_transfer_away_lowers_the_curve_and_one_towards_it_raises_it(
    client: AsyncClient, session: AsyncSession, owner: User, main_account: Account
):
    other = await make_account(session, owner, "Tagesgeld")
    plan = await make_plan(session, owner)
    session.add_all(
        [
            position(plan, "Save", "100.00", 3, budget=Budget.SAVINGS, counter_account_id=other.id),
            position(
                plan,
                "Back",
                "30.00",
                5,
                budget=Budget.SAVINGS,
                account_id=other.id,
                counter_account_id=main_account.id,
            ),
        ]
    )
    await session.commit()

    assert steps(await flow(client)) == [(3, "-100.00"), (5, "30.00")]


async def test_a_ticked_off_commitment_shows_its_booking_not_its_plan_even_in_another_month(
    client: AsyncClient, session: AsyncSession, owner: User, main_account: Account
):
    plan = await make_plan(session, owner)
    rent = position(plan, "Rent", "800.00", 1, paid_at=datetime(2026, 8, 30, tzinfo=UTC))
    session.add(rent)
    await session.flush()
    session.add(booking(owner, main_account, "795.50", date(2026, 8, 30), position_id=rent.id))
    await session.commit()

    body = await flow(client)

    assert steps(body) == [(1, "-795.50")]
    [entry] = body["entries"]
    assert entry["kind"] == "booking"
    assert entry["date"] == "2026-08-30"


async def test_a_manual_booking_without_a_position_counts_on_its_date(
    client: AsyncClient, session: AsyncSession, owner: User, main_account: Account
):
    await make_plan(session, owner)
    session.add(
        booking(owner, main_account, "12.00", date(2026, 9, 9), note="Bakery"),
    )
    session.add(booking(owner, main_account, "5.00", date(2026, 10, 2), note="Next month"))
    await session.commit()

    body = await flow(client)

    assert steps(body) == [(9, "-12.00")]
    assert body["entries"][0]["label"] == "Bakery"


async def test_a_limit_counts_by_plan_and_ignores_bookings_by_default(
    client: AsyncClient, session: AsyncSession, owner: User, main_account: Account
):
    plan = await make_plan(session, owner)
    groceries = position(plan, "Groceries", "400.00", 1, is_limit=True)
    session.add(groceries)
    await session.flush()
    session.add(booking(owner, main_account, "60.00", date(2026, 9, 6), position_id=groceries.id))
    await session.commit()

    assert steps(await flow(client)) == [(1, "-400.00")]


async def test_a_limit_counts_only_what_is_booked_when_the_switch_says_bookings(
    client: AsyncClient, session: AsyncSession, owner: User, main_account: Account
):
    owner.flow_limits_by = FlowLimitsBy.BOOKINGS
    plan = await make_plan(session, owner)
    groceries = position(plan, "Groceries", "400.00", 1, is_limit=True)
    session.add(groceries)
    await session.flush()
    session.add(booking(owner, main_account, "60.00", date(2026, 9, 6), position_id=groceries.id))
    session.add(booking(owner, main_account, "25.00", date(2026, 9, 13), position_id=groceries.id))
    await session.commit()
    await session.refresh(owner)

    body = await flow(client)

    assert steps(body) == [(6, "-60.00"), (13, "-25.00")]
    assert body["flowLimitsBy"] == "bookings"


async def test_a_shortfall_comes_as_a_hint_with_account_day_and_amount(
    client: AsyncClient, session: AsyncSession, owner: User, main_account: Account
):
    plan = await make_plan(session, owner)
    session.add_all(
        [
            position(plan, "Rent", "800.00", 1),
            position(
                plan, "Salary", "2000.00", 20, budget=Budget.INCOME, category=Category.INCOME_EARNED
            ),
        ]
    )
    await session.commit()

    [hint] = (await flow(client))["hints"]

    assert hint["code"] == "flow_shortfall"
    assert hint["severity"] == "warning"
    assert hint["params"] == {
        "day": 1,
        "amount": "800.00",
        "account_id": str(main_account.id),
        "account_name": "Girokonto",
    }


async def test_a_curve_that_stays_above_zero_has_no_hint(
    client: AsyncClient, session: AsyncSession, owner: User, main_account: Account
):
    plan = await make_plan(session, owner)
    session.add(
        position(
            plan, "Salary", "2000.00", 1, budget=Budget.INCOME, category=Category.INCOME_EARNED
        )
    )
    await session.commit()

    assert (await flow(client))["hints"] == []


async def test_the_household_flow_is_one_curve_and_follows_the_viewers_switch(
    client: AsyncClient, session: AsyncSession
):
    """Two members see the same household plan differently: each with their own
    setting for limits."""
    ada = await make_user(session, "Ada")
    bob = await make_user(session, "Bob")
    household = await make_household(session, "Shared")
    await add_member(session, household, ada)
    await add_member(session, household, bob)
    ada_account = await make_account(session, ada, "Ada Giro", default=True)
    await make_account(session, bob, "Bob Giro", default=True)
    ada.flow_limits_by = FlowLimitsBy.PLAN
    bob.flow_limits_by = FlowLimitsBy.BOOKINGS

    ada_plan = await make_plan(session, ada)
    bob_plan = await make_plan(session, bob)
    groceries = position(
        ada_plan, "Groceries", "300.00", 1, is_limit=True, household_id=household.id
    )
    rent = position(bob_plan, "Rent", "700.00", 2, household_id=household.id)
    private = position(bob_plan, "Private", "50.00", 3)
    session.add_all([groceries, rent, private])
    await session.flush()
    session.add(booking(ada, ada_account, "80.00", date(2026, 9, 5), position_id=groceries.id))
    await session.commit()
    await session.refresh(ada)
    await session.refresh(bob)

    url = f"/api/v1/plans/household/{household.id}/2026/9/flow"

    sign_in(ada)
    assert steps(await flow(client, url)) == [(1, "-300.00"), (2, "-700.00")]

    sign_in(bob)
    assert steps(await flow(client, url)) == [(2, "-700.00"), (5, "-80.00")]


def test_the_setting_can_be_changed_through_the_user_update():
    update = UserUpdate.model_validate({"flowLimitsBy": "bookings"})
    assert update.flow_limits_by is FlowLimitsBy.BOOKINGS


def test_the_curve_can_start_at_a_carry_over_instead_of_zero():
    """The seam for the month carry-over (#94): a start value moves the whole curve,
    and a start below zero is a shortfall on the 1st already."""
    from app.services.flow import build_flow

    body = build_flow([], 2026, 9, FlowLimitsBy.PLAN, merged=False, start=Decimal("-40.00"))

    assert body.start == Decimal("-40.00")
    assert {d.balance for d in body.days} == {Decimal("-40.00")}
    assert body.hints[0].params["amount"] == "40.00"


async def test_a_booking_dated_after_the_month_sits_on_the_last_day(
    client: AsyncClient, session: AsyncSession, owner: User, main_account: Account
):
    plan = await make_plan(session, owner)
    rent = position(plan, "Rent", "800.00", 1, paid_at=datetime(2026, 10, 2, tzinfo=UTC))
    session.add(rent)
    await session.flush()
    session.add(booking(owner, main_account, "800.00", date(2026, 10, 2), position_id=rent.id))
    await session.commit()

    body = await flow(client)

    assert steps(body) == [(30, "-800.00")]
    assert body["entries"][0]["date"] == "2026-10-02"


async def test_a_manual_transfer_counts_by_its_direction(
    client: AsyncClient, session: AsyncSession, owner: User, main_account: Account
):
    other = await make_account(session, owner, "Tagesgeld")
    await make_plan(session, owner)
    session.add_all(
        [
            booking(owner, main_account, "50.00", date(2026, 9, 4), counter_account_id=other.id),
            booking(owner, other, "20.00", date(2026, 9, 8), counter_account_id=main_account.id),
        ]
    )
    await session.commit()

    assert steps(await flow(client)) == [(4, "-50.00"), (8, "20.00")]


async def test_a_booking_on_another_account_stays_out(
    client: AsyncClient, session: AsyncSession, owner: User, main_account: Account
):
    other = await make_account(session, owner, "Tagesgeld")
    await make_plan(session, owner)
    session.add(booking(owner, other, "50.00", date(2026, 9, 4), note="Elsewhere"))
    await session.commit()

    assert steps(await flow(client)) == []


async def test_a_ticked_position_without_any_booking_counts_nothing(
    client: AsyncClient, session: AsyncSession, owner: User, main_account: Account
):
    plan = await make_plan(session, owner)
    session.add(position(plan, "Rent", "800.00", 1, paid_at=datetime(2026, 9, 2, tzinfo=UTC)))
    await session.commit()

    assert steps(await flow(client)) == []


# --- Access to somebody else's flow ---------------------------------------


async def foreign_flow_setup(session: AsyncSession, *, plan_level, accounts_level):
    """Ada owns the plan and hands out `plan_level` / `accounts_level`; Bob looks."""
    ada = await make_user(session, "Ada")
    bob = await make_user(session, "Bob")
    household = await make_household(session, "Shared")
    await add_member(session, household, ada)
    await add_member(session, household, bob)
    account = await make_account(session, ada, "Ada Giro", default=True)
    plan = await make_plan(session, ada)
    rent = position(plan, "Rent", "800.00", 1)
    session.add(rent)
    await session.flush()
    session.add(booking(ada, account, "12.00", date(2026, 9, 9), note="Bakery"))
    await session.commit()
    await grant_area(session, household, ada, "plan", plan_level)
    await grant_area(session, household, ada, "accounts", accounts_level)
    await session.refresh(bob)
    sign_in(bob)
    return ada


async def test_a_foreign_plan_without_the_accounts_grant_shows_no_manual_bookings(
    client: AsyncClient, session: AsyncSession
):
    ada = await foreign_flow_setup(
        session, plan_level=AccessLevel.VIEW, accounts_level=AccessLevel.PLAN
    )

    body = await flow(client, f"/api/v1/plans/2026/9/flow?owner={ada.id}")

    assert steps(body) == [(1, "-800.00")]
    [hint] = body["hints"]
    assert hint["params"]["account_name"] is None
    assert hint["params"]["account_id"] is None


async def test_a_foreign_plan_with_the_accounts_grant_shows_them_and_names_the_account(
    client: AsyncClient, session: AsyncSession
):
    ada = await foreign_flow_setup(
        session, plan_level=AccessLevel.VIEW, accounts_level=AccessLevel.VIEW
    )

    body = await flow(client, f"/api/v1/plans/2026/9/flow?owner={ada.id}")

    assert steps(body) == [(1, "-800.00"), (9, "-12.00")]
    assert body["hints"][0]["params"]["account_name"] == "Ada Giro"


async def test_a_foreign_flow_without_any_plan_grant_is_refused(
    client: AsyncClient, session: AsyncSession
):
    ada = await foreign_flow_setup(
        session, plan_level=AccessLevel.PLAN, accounts_level=AccessLevel.VIEW
    )

    response = await client.get(f"/api/v1/plans/2026/9/flow?owner={ada.id}")

    assert response.status_code == 403
    assert "no_insight_granted" in response.text


async def test_the_household_flow_is_refused_to_a_non_member(
    client: AsyncClient, session: AsyncSession
):
    household = await make_household(session, "Not mine")
    outsider = await make_user(session, "Outsider")
    await session.commit()
    await session.refresh(outsider)
    sign_in(outsider)

    response = await client.get(f"/api/v1/plans/household/{household.id}/2026/9/flow")

    assert response.status_code == 403
    assert "not_household_member" in response.text
