"""Every access path by id, pinned down before the checks move into one place (#254).

Five endpoints each had their own copy of "load it, 404 if missing, allowed for
the owner or from the level they granted". They agreed — mostly. This file writes
down what each one answers today, code for code, so that folding them into one
loader cannot quietly change an answer:

* an unknown id is a 404 with the resource's own code
* a stranger, and a member below `edit`, get `no_edit_granted`
* `edit` changes but does not delete; deleting asks for `delete`
* discarding an import entry counts as deleting it (decision 65, since step 3)

Plus the lists by person and by household, where the accounts count only members
who granted insight while the book counts everybody (decision 48 vs. 54).
"""

import uuid
from datetime import date
from decimal import Decimal

import pytest
from httpx import AsyncClient
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.auth import current_active_user
from app.main import app
from app.models.account import Account
from app.models.enums import AccessLevel, AccountType, Budget, Category
from app.models.imported_entry import ImportedEntry
from app.models.plan import Plan, PlanPosition
from app.models.transaction import Transaction
from tests.test_area_permissions import add_member, make_household, make_user
from tests.test_delegation import grant_area, make_commitment, sign_in


@pytest.fixture
async def people(session: AsyncSession):
    """Owner and helper share a household; the stranger shares nothing with them."""
    owner = await make_user(session, "Owner")
    helper = await make_user(session, "Helper")
    stranger = await make_user(session, "Stranger")
    household = await make_household(session, "Shared")
    await add_member(session, household, owner)
    await add_member(session, household, helper)
    await session.commit()
    yield owner, helper, stranger, household
    app.dependency_overrides.pop(current_active_user, None)


async def _account(session: AsyncSession, owner, name: str = "Giro") -> Account:
    account = Account(
        owner_id=owner.id,
        name=name,
        type=AccountType.CHECKING,
        opening_balance=Decimal("0.00"),
        opening_date=date(2026, 1, 1),
    )
    session.add(account)
    await session.flush()
    return account


async def _position(session: AsyncSession, owner) -> PlanPosition:
    plan = Plan(user_id=owner.id, year=2026, month=9)
    session.add(plan)
    await session.flush()
    position = PlanPosition(
        plan_id=plan.id,
        label="Rent",
        amount_planned=Decimal("10.00"),
        category=Category.LEISURE_SUBSCRIPTIONS,
        budget=Budget.WANTS,
        due_day=1,
    )
    session.add(position)
    await session.flush()
    return position


async def _transaction(session: AsyncSession, owner) -> Transaction:
    account = await _account(session, owner)
    transaction = Transaction(
        owner_id=owner.id,
        account_id=account.id,
        occurred_on=date(2026, 9, 1),
        plan_year=2026,
        plan_month=9,
        amount=Decimal("10.00"),
        category=Category.LEISURE_SUBSCRIPTIONS,
        budget=Budget.WANTS,
    )
    session.add(transaction)
    await session.flush()
    return transaction


async def _entry(session: AsyncSession, owner) -> ImportedEntry:
    account = await _account(session, owner)
    entry = ImportedEntry(
        owner_id=owner.id,
        account_id=account.id,
        external_ref=uuid.uuid4().hex,
        occurred_on=date(2026, 9, 1),
        value_on=date(2026, 9, 1),
        amount=Decimal("10.00"),
        incoming=False,
    )
    session.add(entry)
    await session.flush()
    return entry


async def _commitment(session: AsyncSession, owner):
    return await make_commitment(session, owner, "Contract")


#: path, how to make one, which area guards it, the 404 code, a harmless change.
RESOURCES = {
    "account": ("accounts", _account, "accounts", "account_not_found", {"name": "X"}),
    "commitment": (
        "commitments",
        _commitment,
        "commitments",
        "commitment_not_found",
        {"name": "X"},
    ),
    "position": ("positions", _position, "plan", "position_not_found", {"label": "X"}),
    "transaction": (
        "transactions",
        _transaction,
        "book",
        "transaction_not_found",
        {"note": "X"},
    ),
    "imported_entry": ("imports", _entry, "import", "imported_entry_not_found", {}),
}


def _code(response) -> str:
    return response.json()["detail"]["code"]


@pytest.mark.parametrize("kind", RESOURCES)
async def test_an_unknown_id_is_a_404_with_the_resources_own_code(
    client: AsyncClient, session: AsyncSession, people, kind: str
) -> None:
    path, _, _, not_found, change = RESOURCES[kind]
    owner, *_ = people
    sign_in(owner)

    changed = await client.patch(f"/api/v1/{path}/{uuid.uuid4()}", json=change)
    deleted = await client.delete(f"/api/v1/{path}/{uuid.uuid4()}")

    assert (changed.status_code, _code(changed)) == (404, not_found)
    assert (deleted.status_code, _code(deleted)) == (404, not_found)


@pytest.mark.parametrize("kind", RESOURCES)
async def test_a_stranger_may_neither_change_nor_delete(
    client: AsyncClient, session: AsyncSession, people, kind: str
) -> None:
    path, make, _, _, change = RESOURCES[kind]
    owner, _, stranger, _ = people
    thing = await make(session, owner)
    await session.commit()
    sign_in(stranger)

    changed = await client.patch(f"/api/v1/{path}/{thing.id}", json=change)
    deleted = await client.delete(f"/api/v1/{path}/{thing.id}")

    assert (changed.status_code, _code(changed)) == (403, "no_edit_granted")
    assert (deleted.status_code, _code(deleted)) == (403, "no_delete_granted")


@pytest.mark.parametrize("kind", RESOURCES)
async def test_view_is_not_enough_to_change(
    client: AsyncClient, session: AsyncSession, people, kind: str
) -> None:
    path, make, area, _, change = RESOURCES[kind]
    owner, helper, _, household = people
    thing = await make(session, owner)
    await session.commit()
    await grant_area(session, household, owner, area, AccessLevel.VIEW)
    sign_in(helper)

    changed = await client.patch(f"/api/v1/{path}/{thing.id}", json=change)

    assert (changed.status_code, _code(changed)) == (403, "no_edit_granted")


@pytest.mark.parametrize("kind", RESOURCES)
async def test_edit_changes_but_does_not_delete(
    client: AsyncClient, session: AsyncSession, people, kind: str
) -> None:
    path, make, area, _, change = RESOURCES[kind]
    owner, helper, _, household = people
    thing = await make(session, owner)
    await session.commit()
    await grant_area(session, household, owner, area, AccessLevel.EDIT)
    sign_in(helper)

    changed = await client.patch(f"/api/v1/{path}/{thing.id}", json=change)
    deleted = await client.delete(f"/api/v1/{path}/{thing.id}")

    assert changed.status_code == 200, changed.json()
    # Discarding an imported entry counts as deleting it (decision 65).
    assert (deleted.status_code, _code(deleted)) == (403, "no_delete_granted")


@pytest.mark.parametrize("kind", RESOURCES)
async def test_delete_level_deletes(
    client: AsyncClient, session: AsyncSession, people, kind: str
) -> None:
    path, make, area, _, _ = RESOURCES[kind]
    owner, helper, _, household = people
    thing = await make(session, owner)
    await session.commit()
    await grant_area(session, household, owner, area, AccessLevel.DELETE)
    sign_in(helper)

    deleted = await client.delete(f"/api/v1/{path}/{thing.id}")

    assert deleted.status_code in (200, 204), deleted.text


@pytest.mark.parametrize("kind", RESOURCES)
async def test_the_owner_changes_and_deletes_without_any_grant(
    client: AsyncClient, session: AsyncSession, people, kind: str
) -> None:
    path, make, _, _, change = RESOURCES[kind]
    owner, *_ = people
    thing = await make(session, owner)
    await session.commit()
    sign_in(owner)

    changed = await client.patch(f"/api/v1/{path}/{thing.id}", json=change)
    deleted = await client.delete(f"/api/v1/{path}/{thing.id}")

    assert changed.status_code == 200, changed.json()
    assert deleted.status_code in (200, 204), deleted.text


# --- Ticking off a position ------------------------------------------------


async def test_ticking_off_somebody_elses_position_asks_the_book_not_the_plan(
    client: AsyncClient, session: AsyncSession, people
) -> None:
    """Decision 61: a tick writes into the owner's book. Even `delete` on the plan
    does not open it; `view` on the book is not enough."""
    owner, helper, _, household = people
    position = await _position(session, owner)
    await session.commit()
    await grant_area(session, household, owner, "plan", AccessLevel.DELETE)
    await grant_area(session, household, owner, "book", AccessLevel.VIEW)
    sign_in(helper)

    ticked = await client.post(f"/api/v1/positions/{position.id}/paid", json={})
    unticked = await client.delete(f"/api/v1/positions/{position.id}/paid")
    missing = await client.post(f"/api/v1/positions/{uuid.uuid4()}/paid", json={})

    assert (ticked.status_code, _code(ticked)) == (403, "no_create_granted")
    assert (unticked.status_code, _code(unticked)) == (403, "no_delete_granted")
    assert (missing.status_code, _code(missing)) == (404, "position_not_found")


async def test_book_create_ticks_but_only_book_delete_takes_the_tick_back(
    client: AsyncClient, session: AsyncSession, people
) -> None:
    owner, helper, _, household = people
    position = await _position(session, owner)
    account = await _account(session, owner)
    account.is_default = True
    await session.commit()
    await grant_area(session, household, owner, "book", AccessLevel.CREATE)
    sign_in(helper)

    ticked = await client.post(f"/api/v1/positions/{position.id}/paid", json={})
    refused = await client.delete(f"/api/v1/positions/{position.id}/paid")
    await grant_area(session, household, owner, "book", AccessLevel.DELETE)
    unticked = await client.delete(f"/api/v1/positions/{position.id}/paid")

    assert ticked.status_code == 200, ticked.json()
    assert (refused.status_code, _code(refused)) == (403, "no_delete_granted")
    assert unticked.status_code == 200, unticked.json()


# --- Booking on what the booking points at ---------------------------------


async def test_creating_a_booking_checks_account_counter_account_and_position(
    client: AsyncClient, session: AsyncSession, people
) -> None:
    owner, helper, _, household = people
    own = await _account(session, helper, "Own")
    foreign = await _account(session, owner, "Foreign")
    foreign_position = await _position(session, owner)
    await session.commit()
    await grant_area(session, household, owner, "book", AccessLevel.VIEW)
    sign_in(helper)
    base = {
        "occurredOn": "2026-09-01",
        "amount": "10.00",
        "category": Category.LEISURE_SUBSCRIPTIONS.value,
        "budget": Budget.WANTS.value,
    }

    cases = [
        ({"accountId": str(uuid.uuid4())}, 404, "account_not_found"),
        ({"accountId": str(foreign.id)}, 403, "no_create_granted"),
        (
            {"accountId": str(own.id), "counterAccountId": str(uuid.uuid4())},
            404,
            "account_not_found",
        ),
        (
            {"accountId": str(own.id), "counterAccountId": str(foreign.id)},
            403,
            "no_create_granted",
        ),
        (
            {"accountId": str(own.id), "positionId": str(uuid.uuid4())},
            404,
            "position_not_found",
        ),
        (
            {"accountId": str(own.id), "positionId": str(foreign_position.id)},
            403,
            "no_create_granted",
        ),
    ]
    for extra, status_code, code in cases:
        response = await client.post("/api/v1/transactions", json=base | extra)
        assert (response.status_code, _code(response)) == (status_code, code), extra


async def test_a_booking_cannot_move_to_an_account_of_somebody_else_even_with_edit(
    client: AsyncClient, session: AsyncSession, people
) -> None:
    """Edit on the other person's accounts lets the account through the first
    check; the booking still stays with its owner."""
    owner, helper, _, household = people
    booking = await _transaction(session, helper)
    foreign = await _account(session, owner, "Foreign")
    await session.commit()
    await grant_area(session, household, owner, "accounts", AccessLevel.EDIT)
    sign_in(helper)

    moved = await client.patch(
        f"/api/v1/transactions/{booking.id}", json={"accountId": str(foreign.id)}
    )

    assert (moved.status_code, _code(moved)) == (403, "not_account_owner")


# --- Whose accounts a list covers ------------------------------------------


async def test_somebody_elses_accounts_need_view_and_the_carry_over_suggestion_too(
    client: AsyncClient, session: AsyncSession, people
) -> None:
    owner, helper, _, household = people
    account = await _account(session, owner)
    await session.commit()
    sign_in(helper)

    urls = [
        f"/api/v1/accounts?owner={owner.id}",
        f"/api/v1/accounts/history?year=2026&month=9&owner={owner.id}",
        f"/api/v1/accounts/{account.id}/carry-over-suggestion?year=2026&month=9",
    ]
    for url in urls:
        hidden = await client.get(url)
        assert (hidden.status_code, _code(hidden)) == (403, "no_insight_granted"), url

    missing = await client.get(
        f"/api/v1/accounts/{uuid.uuid4()}/carry-over-suggestion?year=2026&month=9"
    )
    assert (missing.status_code, _code(missing)) == (404, "account_not_found")

    await grant_area(session, household, owner, "accounts", AccessLevel.VIEW)
    for url in urls:
        shown = await client.get(url)
        assert shown.status_code == 200, url


async def test_the_household_accounts_count_only_members_who_granted_insight(
    client: AsyncClient, session: AsyncSession, people
) -> None:
    """Unlike the household book: accounts are no shared positions (decision 54)."""
    owner, helper, stranger, household = people
    await _account(session, owner, "Owner giro")
    await _account(session, helper, "Helper giro")
    await session.commit()
    sign_in(helper)

    before = await client.get(f"/api/v1/accounts?household={household.id}")
    assert [row["name"] for row in before.json()] == ["Helper giro"]

    await grant_area(session, household, owner, "accounts", AccessLevel.VIEW)
    after = await client.get(f"/api/v1/accounts?household={household.id}")
    assert sorted(row["name"] for row in after.json()) == ["Helper giro", "Owner giro"]

    sign_in(stranger)
    for url in (
        f"/api/v1/accounts?household={household.id}",
        f"/api/v1/accounts/history?year=2026&month=9&household={household.id}",
    ):
        refused = await client.get(url)
        assert (refused.status_code, _code(refused)) == (403, "not_household_member"), url


# --- Acting for somebody else without an id --------------------------------


async def test_creating_for_somebody_else_needs_create_in_the_matching_area(
    client: AsyncClient, session: AsyncSession, people
) -> None:
    owner, helper, _, household = people
    await grant_area(session, household, owner, "accounts", AccessLevel.VIEW)
    await grant_area(session, household, owner, "commitments", AccessLevel.VIEW)
    await grant_area(session, household, owner, "plan", AccessLevel.VIEW)
    sign_in(helper)

    account = await client.post(
        f"/api/v1/accounts?owner={owner.id}",
        json={
            "name": "Giro",
            "type": "checking",
            "openingBalance": "0.00",
            "openingDate": "2026-01-01",
        },
    )
    plan = await client.post(f"/api/v1/plans?owner={owner.id}", json={"year": 2026, "month": 9})
    deleted_plan = await client.delete(f"/api/v1/plans/2026/9?owner={owner.id}")

    assert (account.status_code, _code(account)) == (403, "no_create_granted")
    assert (plan.status_code, _code(plan)) == (403, "no_create_granted")
    assert (deleted_plan.status_code, _code(deleted_plan)) == (403, "no_delete_granted")


async def test_changing_somebody_elses_month_needs_edit_on_the_plan(
    client: AsyncClient, session: AsyncSession, people
) -> None:
    owner, helper, _, household = people
    position = await _position(session, owner)
    await session.commit()
    await grant_area(session, household, owner, "plan", AccessLevel.VIEW)
    sign_in(helper)

    changed = await client.patch(
        f"/api/v1/plans/{position.plan_id}",
        json={"targetNeeds": "50.00", "targetWants": "30.00", "targetSavings": "20.00"},
    )
    missing = await client.patch(
        f"/api/v1/plans/{uuid.uuid4()}",
        json={"targetNeeds": "50.00", "targetWants": "30.00", "targetSavings": "20.00"},
    )

    assert (changed.status_code, _code(changed)) == (403, "no_edit_granted")
    assert (missing.status_code, _code(missing)) == (404, "plan_not_found")


async def test_importing_and_reading_imports_of_somebody_else(
    client: AsyncClient, session: AsyncSession, people
) -> None:
    owner, helper, _, household = people
    await grant_area(session, household, owner, "import", AccessLevel.VIEW)
    sign_in(helper)

    listed = await client.get(f"/api/v1/imports?owner={owner.id}")
    uploaded = await client.post(
        f"/api/v1/imports?owner={owner.id}",
        files={"file": ("x.csv", b"irrelevant", "text/csv")},
    )

    assert listed.status_code == 200
    assert (uploaded.status_code, _code(uploaded)) == (403, "no_create_granted")
