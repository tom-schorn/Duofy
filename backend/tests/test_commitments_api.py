"""The commitment endpoints and what they promise about `isLimit`.

`is_limit` says the planned amount is a limit that fills up over the month. That
only makes sense on a `contract`: a debt or a savings goal has a fixed amount,
income has no limit at all. A limit on anything else would be copied onto the
month's position by `create_plan` and take away its tick box for good.

Part of #106.
"""

from datetime import date, timedelta
from decimal import Decimal

import pytest
from httpx import AsyncClient
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.commitment import Commitment
from app.models.enums import Budget, Category, CommitmentType
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
        "intervalMonths": 1,
        "firstDueDate": "2026-01-01",
    }
    return body | changes


async def test_is_limit_round_trips_in_camel_case(
    client: AsyncClient, owner: User
):
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


async def test_a_limit_on_a_debt_is_rejected_on_update(
    client: AsyncClient, owner: User
):
    created = await client.post("/api/v1/commitments", json=payload(type="debt"))
    assert created.status_code == 201

    response = await client.patch(
        f"/api/v1/commitments/{created.json()['id']}", json={"isLimit": True}
    )
    assert response.status_code == 422
    assert response.json()["detail"] == {"code": "limit_only_for_contract"}


async def test_a_rejected_update_changes_nothing(
    client: AsyncClient, owner: User
):
    created = await client.post("/api/v1/commitments", json=payload(type="debt"))
    await client.patch(
        f"/api/v1/commitments/{created.json()['id']}", json={"isLimit": True, "name": "X"}
    )

    listed = await client.get("/api/v1/commitments")
    assert [(row["name"], row["isLimit"]) for row in listed.json()] == [("Groceries", False)]


async def test_a_limit_on_a_contract_is_accepted(
    client: AsyncClient, owner: User
):
    created = await client.post("/api/v1/commitments", json=payload())
    response = await client.patch(
        f"/api/v1/commitments/{created.json()['id']}", json={"isLimit": True}
    )
    assert response.status_code == 200
    assert response.json()["isLimit"] is True


@pytest.mark.parametrize("field", ["isLimit", "name", "amount", "firstDueDate", "budget"])
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


@pytest.mark.parametrize("months", [0, -1, 121])
async def test_creating_with_an_interval_outside_one_to_120_is_rejected(
    client: AsyncClient, owner: User, months: int
):

    response = await client.post(
        "/api/v1/commitments",
        json=payload(intervalMonths=months, firstDueDate="2026-01-01"),
    )

    assert response.status_code == 422
    assert "interval_months_out_of_range" in response.text


@pytest.mark.parametrize("months", [1, 5, 120])
async def test_creating_with_an_interval_inside_the_range_works(
    client: AsyncClient, owner: User, months: int
):

    response = await client.post(
        "/api/v1/commitments",
        json=payload(intervalMonths=months, firstDueDate="2026-01-01"),
    )

    assert response.status_code == 201
    assert response.json()["intervalMonths"] == months


@pytest.mark.parametrize("months", [0, 121])
async def test_updating_to_an_interval_outside_the_range_is_rejected(
    client: AsyncClient, owner: User, months: int
):
    created = await client.post("/api/v1/commitments", json=payload())

    response = await client.patch(
        f"/api/v1/commitments/{created.json()['id']}", json={"intervalMonths": months}
    )

    assert response.status_code == 422
    assert "interval_months_out_of_range" in response.text


async def test_a_commitment_without_a_first_due_date_is_rejected_even_when_monthly(
    client: AsyncClient, owner: User
):
    """The date is the only source of the due day, so every commitment needs one."""
    body = payload()
    del body["firstDueDate"]

    response = await client.post("/api/v1/commitments", json=body)

    assert response.status_code == 422
    assert "firstDueDate" in response.text


async def test_the_due_day_is_read_from_the_first_due_date_and_not_sent_back(
    client: AsyncClient, owner: User
):
    created = await client.post("/api/v1/commitments", json=payload(firstDueDate="2026-03-17"))

    assert created.status_code == 201, created.text
    assert created.json()["firstDueDate"] == "2026-03-17"
    assert "dueDay" not in created.json()


async def test_an_explicit_null_interval_is_not_allowed(
    client: AsyncClient, owner: User
):
    created = await client.post("/api/v1/commitments", json=payload())

    response = await client.patch(
        f"/api/v1/commitments/{created.json()['id']}", json={"intervalMonths": None}
    )

    assert response.status_code == 422
    assert response.json()["detail"]["code"] == "null_not_allowed"


async def test_an_explicit_null_on_the_end_clears_it(client: AsyncClient, owner: User):
    created = await client.post("/api/v1/commitments", json=payload(endsOn="2026-09-30"))
    assert created.json()["endsOn"] == "2026-09-30"

    response = await client.patch(
        f"/api/v1/commitments/{created.json()['id']}", json={"endsOn": None}
    )
    assert response.status_code == 200
    assert response.json()["endsOn"] is None


async def test_the_end_is_empty_unless_given(client: AsyncClient, owner: User):
    created = await client.post("/api/v1/commitments", json=payload())
    assert created.json()["endsOn"] is None
    assert "active" not in created.json()


async def test_an_end_before_the_start_month_is_rejected_on_create(
    client: AsyncClient, owner: User
):
    response = await client.post(
        "/api/v1/commitments", json=payload(firstDueDate="2026-03-17", endsOn="2026-02-28")
    )
    assert response.status_code == 422
    assert "ends_on_before_start" in response.text


async def test_an_end_in_the_start_month_is_accepted(client: AsyncClient, owner: User):
    response = await client.post(
        "/api/v1/commitments", json=payload(firstDueDate="2026-03-17", endsOn="2026-03-01")
    )
    assert response.status_code == 201


async def test_an_end_before_the_start_month_is_rejected_on_update(
    client: AsyncClient, owner: User
):
    created = await client.post("/api/v1/commitments", json=payload(firstDueDate="2026-03-17"))
    url = f"/api/v1/commitments/{created.json()['id']}"

    ended = await client.patch(url, json={"endsOn": "2026-02-28"})
    assert ended.status_code == 422
    assert ended.json()["detail"] == {"code": "ends_on_before_start"}

    # Moving the start behind an existing end breaks the pair just the same.
    await client.patch(url, json={"endsOn": "2026-05-31"})
    moved = await client.patch(url, json={"firstDueDate": "2026-06-01"})
    assert moved.status_code == 422
    assert moved.json()["detail"] == {"code": "ends_on_before_start"}


async def test_the_list_filters_by_status_and_defaults_to_all(client: AsyncClient, owner: User):
    today = date.today()
    last_month = (today.replace(day=1) - timedelta(days=1)).isoformat()
    for name, ends_on in (
        ("Open", None),
        ("Ends this month", today.isoformat()),
        ("Ended", last_month),
    ):
        response = await client.post(
            "/api/v1/commitments",
            json=payload(name=name, firstDueDate="2020-01-01", endsOn=ends_on),
        )
        assert response.status_code == 201, response.text

    async def names(query: str) -> set[str]:
        response = await client.get(f"/api/v1/commitments{query}")
        assert response.status_code == 200
        return {row["name"] for row in response.json()}

    assert await names("") == {"Open", "Ends this month", "Ended"}
    assert await names("?status=all") == {"Open", "Ends this month", "Ended"}
    assert await names("?status=active") == {"Open", "Ends this month"}
    assert await names("?status=ended") == {"Ended"}


async def test_an_unknown_status_is_a_422(client: AsyncClient, owner: User):
    response = await client.get("/api/v1/commitments?status=paused")
    assert response.status_code == 422
    assert response.json()["detail"] == {"code": "invalid_status_filter"}


async def test_a_stored_end_before_the_start_does_not_block_unrelated_changes(
    client: AsyncClient, session: AsyncSession, owner: User
):
    """The migration can leave such a row (a switched-off contract starting later)."""
    stale = Commitment(
        owner_id=owner.id,
        type=CommitmentType.CONTRACT,
        name="Old name",
        amount=Decimal("10.00"),
        category=Category.LEISURE_SUBSCRIPTIONS,
        budget=Budget.WANTS,
        interval_months=1,
        first_due_date=date(2027, 3, 1),
        ends_on=date(2026, 9, 26),
    )
    session.add(stale)
    await session.commit()
    url = f"/api/v1/commitments/{stale.id}"

    renamed = await client.patch(url, json={"name": "New name"})
    assert renamed.status_code == 200, renamed.text

    still_invalid = await client.patch(url, json={"endsOn": "2026-10-31"})
    assert still_invalid.status_code == 422
    assert still_invalid.json()["detail"] == {"code": "ends_on_before_start"}
    also_invalid = await client.patch(url, json={"firstDueDate": "2027-04-01"})
    assert also_invalid.status_code == 422

    fixed = await client.patch(url, json={"endsOn": "2027-06-30"})
    assert fixed.status_code == 200
