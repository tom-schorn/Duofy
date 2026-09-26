"""The commitment endpoints and what they promise about `isLimit`.

`is_limit` says the planned amount is a limit that fills up over the month. That
only makes sense on a `contract`: a debt or a savings goal has a fixed amount,
income has no limit at all. A limit on anything else would be copied onto the
month's position by `create_plan` and take away its tick box for good.

Part of #106.
"""

import pytest
from httpx import AsyncClient
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.user import User
from tests.test_area_permissions import make_user
from tests.test_delegation import sign_in


@pytest.fixture
async def owner(session: AsyncSession) -> User:
    user = await make_user(session, "Owner")
    await session.commit()
    sign_in(user)
    return user


def payload(**changes) -> dict:
    body = {
        "type": "contract",
        "name": "Groceries",
        "amount": "600.00",
        "category": "household.groceries",
        "budget": "needs",
        "rhythm": "monthly",
        "dueDay": 1,
    }
    return body | changes


async def test_is_limit_round_trips_in_camel_case(client: AsyncClient, owner: User):
    created = await client.post("/api/v1/commitments", json=payload(isLimit=True))
    assert created.status_code == 201
    assert created.json()["isLimit"] is True
    assert "is_limit" not in created.json()

    listed = await client.get("/api/v1/commitments")
    assert [row["isLimit"] for row in listed.json()] == [True]

    patched = await client.patch(
        f"/api/v1/commitments/{created.json()['id']}", json={"isLimit": False}
    )
    assert patched.status_code == 200
    assert patched.json()["isLimit"] is False


async def test_is_limit_defaults_to_false_and_is_always_in_the_response(
    client: AsyncClient, owner: User
):
    created = await client.post("/api/v1/commitments", json=payload())
    assert created.status_code == 201
    assert created.json()["isLimit"] is False


@pytest.mark.parametrize("kind", ["income", "debt", "savings_goal"])
async def test_a_limit_on_anything_but_a_contract_is_rejected_on_create(
    client: AsyncClient, owner: User, kind: str
):
    response = await client.post(
        "/api/v1/commitments", json=payload(type=kind, isLimit=True)
    )
    assert response.status_code == 422
    assert response.json()["detail"] == {"code": "limit_only_for_contract"}


async def test_a_limit_on_a_debt_is_rejected_on_update(client: AsyncClient, owner: User):
    created = await client.post("/api/v1/commitments", json=payload(type="debt"))
    assert created.status_code == 201

    response = await client.patch(
        f"/api/v1/commitments/{created.json()['id']}", json={"isLimit": True}
    )
    assert response.status_code == 422
    assert response.json()["detail"] == {"code": "limit_only_for_contract"}


async def test_a_rejected_update_changes_nothing(client: AsyncClient, owner: User):
    created = await client.post("/api/v1/commitments", json=payload(type="debt"))
    await client.patch(
        f"/api/v1/commitments/{created.json()['id']}", json={"isLimit": True, "name": "X"}
    )

    listed = await client.get("/api/v1/commitments")
    assert [(row["name"], row["isLimit"]) for row in listed.json()] == [("Groceries", False)]


async def test_a_limit_on_a_contract_is_accepted(client: AsyncClient, owner: User):
    created = await client.post("/api/v1/commitments", json=payload())
    response = await client.patch(
        f"/api/v1/commitments/{created.json()['id']}", json={"isLimit": True}
    )
    assert response.status_code == 200
    assert response.json()["isLimit"] is True


@pytest.mark.parametrize("field", ["isLimit", "name", "amount", "dueDay", "active", "budget"])
async def test_an_explicit_null_on_a_required_field_is_a_422_not_a_500(
    client: AsyncClient, owner: User, field: str
):
    created = await client.post("/api/v1/commitments", json=payload())

    response = await client.patch(
        f"/api/v1/commitments/{created.json()['id']}", json={field: None}
    )
    assert response.status_code == 422
    assert response.json()["detail"] == {"code": "null_not_allowed"}


async def test_an_explicit_null_on_an_optional_field_still_clears_it(
    client: AsyncClient, owner: User
):
    created = await client.post(
        "/api/v1/commitments", json=payload(paymentMethod="direct_debit")
    )
    assert created.status_code == 201, created.text

    response = await client.patch(
        f"/api/v1/commitments/{created.json()['id']}", json={"paymentMethod": None}
    )
    assert response.status_code == 200
    assert response.json()["paymentMethod"] is None
