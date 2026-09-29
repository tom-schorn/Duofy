"""Every area × every level × every action, against the server (#254).

One representative endpoint per area and action. The owner grants the helper one
level in one area — per person, the way the grants page writes it — and the
helper tries to see, create, change and delete. Allowed means the rights check
let the request through (whatever the endpoint made of it afterwards); refused
means a 403 naming the missing step.

The other areas stay at `none` in every case, so this also shows that no area
opens another.
"""

from collections.abc import Awaitable, Callable
from datetime import date
from decimal import Decimal

import pytest
from httpx import AsyncClient, Response
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.auth import current_active_user
from app.core.permissions import Area
from app.main import app
from app.models.enums import AccessLevel, Role, TransactionKind
from app.models.household import HouseholdMember
from app.models.transaction import Transaction
from app.models.user import User
from app.services.grants import set_levels
from tests.test_access_paths import _account, _commitment, _entry, _position, _transaction
from tests.test_area_permissions import add_member, make_household, make_user
from tests.test_delegation import position_payload, sign_in

ACTIONS = ("view", "create", "edit", "delete")

#: The step each action needs and the code a refusal carries.
NEEDS = {
    "view": (AccessLevel.VIEW, "no_insight_granted"),
    "create": (AccessLevel.CREATE, "no_create_granted"),
    "edit": (AccessLevel.EDIT, "no_edit_granted"),
    "delete": (AccessLevel.DELETE, "no_delete_granted"),
}

Call = Callable[[AsyncClient, AsyncSession, User], Awaitable[Response]]


async def _plan_view(client, session, owner):
    await _position(session, owner)
    await session.commit()
    return await client.get(f"/api/v1/plans/2026/9?owner={owner.id}")


async def _plan_create(client, session, owner):
    position = await _position(session, owner)
    await session.commit()
    return await client.post(f"/api/v1/plans/{position.plan_id}/positions", json=position_payload())


async def _plan_edit(client, session, owner):
    position = await _position(session, owner)
    await session.commit()
    return await client.patch(f"/api/v1/positions/{position.id}", json={"label": "X"})


async def _plan_delete(client, session, owner):
    position = await _position(session, owner)
    await session.commit()
    return await client.delete(f"/api/v1/positions/{position.id}")


async def _book_view(client, session, owner):
    return await client.get(f"/api/v1/transactions?owner={owner.id}")


async def _book_create(client, session, owner):
    account = await _account(session, owner)
    await session.commit()
    return await client.post(
        "/api/v1/transactions",
        json={
            "accountId": str(account.id),
            "occurredOn": "2026-09-01",
            "amount": "10.00",
            "category": "leisure.subscriptions",
            "budget": "wants",
        },
    )


async def _book_edit(client, session, owner):
    transaction = await _transaction(session, owner)
    await session.commit()
    return await client.patch(f"/api/v1/transactions/{transaction.id}", json={"note": "X"})


async def _book_delete(client, session, owner):
    transaction = await _transaction(session, owner)
    await session.commit()
    return await client.delete(f"/api/v1/transactions/{transaction.id}")


async def _accounts_view(client, session, owner):
    return await client.get(f"/api/v1/accounts?owner={owner.id}")


async def _accounts_create(client, session, owner):
    return await client.post(
        f"/api/v1/accounts?owner={owner.id}",
        json={
            "name": "Giro",
            "type": "checking",
            "openingBalance": "0.00",
            "openingDate": "2026-01-01",
        },
    )


async def _accounts_edit(client, session, owner):
    account = await _account(session, owner)
    await session.commit()
    return await client.patch(f"/api/v1/accounts/{account.id}", json={"name": "X"})


async def _accounts_delete(client, session, owner):
    account = await _account(session, owner)
    await session.commit()
    return await client.delete(f"/api/v1/accounts/{account.id}")


async def _commitments_view(client, session, owner):
    return await client.get(f"/api/v1/commitments?owner={owner.id}")


async def _commitments_create(client, session, owner):
    return await client.post(
        f"/api/v1/commitments?owner={owner.id}",
        json={
            "name": "Contract",
            "amount": "9.99",
            "category": "leisure.subscriptions",
            "budget": "wants",
            "type": "contract",
            "intervalMonths": 1,
            "firstDueDate": "2026-01-01",
        },
    )


async def _commitments_edit(client, session, owner):
    commitment = await _commitment(session, owner)
    await session.commit()
    return await client.patch(f"/api/v1/commitments/{commitment.id}", json={"name": "X"})


async def _commitments_delete(client, session, owner):
    commitment = await _commitment(session, owner)
    await session.commit()
    return await client.delete(f"/api/v1/commitments/{commitment.id}")


async def _import_view(client, session, owner):
    return await client.get(f"/api/v1/imports?owner={owner.id}")


async def _import_create(client, session, owner):
    return await client.post(
        f"/api/v1/imports?owner={owner.id}",
        files={"file": ("x.csv", b"irrelevant", "text/csv")},
    )


async def _import_edit(client, session, owner):
    entry = await _entry(session, owner)
    await session.commit()
    return await client.patch(f"/api/v1/imports/{entry.id}", json={})


async def _import_delete(client, session, owner):
    entry = await _entry(session, owner)
    await session.commit()
    return await client.delete(f"/api/v1/imports/{entry.id}")


CALLS: dict[tuple[Area, str], Call] = {
    (Area.PLAN, "view"): _plan_view,
    (Area.PLAN, "create"): _plan_create,
    (Area.PLAN, "edit"): _plan_edit,
    (Area.PLAN, "delete"): _plan_delete,
    (Area.BOOK, "view"): _book_view,
    (Area.BOOK, "create"): _book_create,
    (Area.BOOK, "edit"): _book_edit,
    (Area.BOOK, "delete"): _book_delete,
    (Area.ACCOUNTS, "view"): _accounts_view,
    (Area.ACCOUNTS, "create"): _accounts_create,
    (Area.ACCOUNTS, "edit"): _accounts_edit,
    (Area.ACCOUNTS, "delete"): _accounts_delete,
    (Area.COMMITMENTS, "view"): _commitments_view,
    (Area.COMMITMENTS, "create"): _commitments_create,
    (Area.COMMITMENTS, "edit"): _commitments_edit,
    (Area.COMMITMENTS, "delete"): _commitments_delete,
    (Area.IMPORT, "view"): _import_view,
    (Area.IMPORT, "create"): _import_create,
    (Area.IMPORT, "edit"): _import_edit,
    (Area.IMPORT, "delete"): _import_delete,
}


@pytest.fixture
async def owner_and_helper(session: AsyncSession):
    owner = await make_user(session, "Owner")
    helper = await make_user(session, "Helper")
    household = await make_household(session, "Home")
    await add_member(session, household, owner)
    await add_member(session, household, helper)
    await session.commit()
    yield owner, helper
    app.dependency_overrides.pop(current_active_user, None)


@pytest.mark.parametrize("action", ACTIONS)
@pytest.mark.parametrize("level", list(AccessLevel), ids=lambda level: level.value)
@pytest.mark.parametrize("area", list(Area), ids=lambda area: area.value)
async def test_the_level_granted_in_an_area_decides_each_action(
    client: AsyncClient,
    session: AsyncSession,
    owner_and_helper,
    area: Area,
    level: AccessLevel,
    action: str,
) -> None:
    owner, helper = owner_and_helper
    await set_levels(session, owner.id, helper.id, {area: level})
    await session.commit()
    sign_in(helper)

    response = await CALLS[(area, action)](client, session, owner)

    needed, code = NEEDS[action]
    if level.rank >= needed.rank:
        assert response.status_code != 403, response.text
    else:
        assert response.status_code == 403, response.text
        assert response.json()["detail"] == {"code": code}


@pytest.mark.parametrize("area", list(Area), ids=lambda area: area.value)
async def test_a_grant_to_one_member_gives_another_nothing(
    client: AsyncClient, session: AsyncSession, owner_and_helper, area: Area
) -> None:
    """Per person (decision 57): the third member of the household sees nothing."""
    owner, helper = owner_and_helper
    third = await make_user(session, "Third")
    household = await session.scalar(
        select(HouseholdMember.household_id).where(HouseholdMember.user_id == owner.id)
    )
    session.add(HouseholdMember(household_id=household, user_id=third.id, role=Role.MEMBER))
    await set_levels(session, owner.id, helper.id, {area: AccessLevel.DELETE})
    await session.commit()
    sign_in(third)

    response = await CALLS[(area, "view")](client, session, owner)

    assert response.status_code == 403
    assert response.json()["detail"] == {"code": "no_insight_granted"}


async def _carry_over(session: AsyncSession, owner) -> Transaction:
    account = await _account(session, owner)
    transaction = Transaction(
        owner_id=owner.id,
        account_id=account.id,
        kind=TransactionKind.CARRY_OVER,
        occurred_on=date(2026, 9, 1),
        plan_year=2026,
        plan_month=9,
        amount=Decimal("10.00"),
    )
    session.add(transaction)
    await session.flush()
    return transaction


async def _carry_over_create(client, session, owner):
    account = await _account(session, owner)
    await session.commit()
    return await client.post(
        "/api/v1/transactions",
        json={
            "kind": "carry_over",
            "accountId": str(account.id),
            "occurredOn": "2026-09-01",
            "amount": "10.00",
        },
    )


async def _carry_over_edit(client, session, owner):
    transaction = await _carry_over(session, owner)
    await session.commit()
    return await client.patch(f"/api/v1/transactions/{transaction.id}", json={"amount": "12.00"})


async def _carry_over_delete(client, session, owner):
    transaction = await _carry_over(session, owner)
    await session.commit()
    return await client.delete(f"/api/v1/transactions/{transaction.id}")


CARRY_OVER_CALLS: dict[str, tuple[Call, AccessLevel, str]] = {
    "create": (_carry_over_create, AccessLevel.EDIT, "no_edit_granted"),
    "edit": (_carry_over_edit, AccessLevel.EDIT, "no_edit_granted"),
    "delete": (_carry_over_delete, AccessLevel.DELETE, "no_delete_granted"),
}


@pytest.mark.parametrize("action", list(CARRY_OVER_CALLS))
@pytest.mark.parametrize("level", list(AccessLevel), ids=lambda level: level.value)
async def test_a_carry_over_on_another_persons_account_asks_the_accounts_not_the_book(
    client: AsyncClient,
    session: AsyncSession,
    owner_and_helper,
    level: AccessLevel,
    action: str,
) -> None:
    """Decision 69: a carry-over sets where an account starts. Creating and changing
    it need accounts `edit`, deleting it accounts `delete` — the book grant, even
    at its top, opens none of it."""
    owner, helper = owner_and_helper
    await set_levels(
        session, owner.id, helper.id, {Area.ACCOUNTS: level, Area.BOOK: AccessLevel.DELETE}
    )
    await session.commit()
    sign_in(helper)

    call, needed, code = CARRY_OVER_CALLS[action]
    response = await call(client, session, owner)

    if level.rank >= needed.rank:
        assert response.status_code != 403, response.text
    else:
        assert response.status_code == 403, response.text
        assert response.json()["detail"] == {"code": code}
