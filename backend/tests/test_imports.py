"""Uploading a bank file, parking it, and turning entries into bookings.

Runs against the invented CAMT fixtures, so the numbers here are the numbers in
`tests/fixtures/camt/`: page one holds four entries of which one is pending,
page two holds two more.
"""

import uuid
from datetime import date
from decimal import Decimal
from pathlib import Path

from httpx import AsyncClient
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.auth import current_active_user
from app.main import app
from app.models.account import Account
from app.models.enums import AccessLevel, AccountType, Block, Category
from app.models.imported_entry import ImportedEntry
from app.models.transaction import Transaction
from app.models.user import User
from tests.test_area_permissions import add_member, make_household, make_user
from tests.test_delegation import grant_area

FIXTURES = Path(__file__).parent / "fixtures" / "camt"

#: The IBAN the fixture reports on.
FILE_IBAN = "DE02120300000000202051"


def sign_in(user: User) -> None:
    app.dependency_overrides[current_active_user] = lambda: user


async def make_account(
    session: AsyncSession, owner: User, *, iban: str | None = None
) -> Account:
    account = Account(
        owner_id=owner.id,
        name="Giro",
        type=AccountType.CHECKING,
        opening_balance=Decimal("0.00"),
        opening_date=date(2026, 1, 1),
        external_ref=iban,
    )
    session.add(account)
    await session.commit()
    await session.refresh(account)
    return account


def upload_file(name: str = "report_page1.xml") -> dict:
    return {"file": (name, (FIXTURES / name).read_bytes(), "application/xml")}


async def parked(session: AsyncSession, owner: User) -> list[ImportedEntry]:
    rows = await session.execute(
        select(ImportedEntry)
        .where(ImportedEntry.owner_id == owner.id)
        .order_by(ImportedEntry.occurred_on)
    )
    return list(rows.scalars())


# --- Uploading -------------------------------------------------------------


async def test_upload_parks_the_entries(client: AsyncClient, session: AsyncSession):
    """The account is found by the IBAN in the file, nothing is booked yet."""
    user = await make_user(session, "Owner")
    account = await make_account(session, user, iban=FILE_IBAN)
    await session.commit()
    sign_in(user)

    response = await client.post("/api/v1/imports", files=upload_file())
    assert response.status_code == 201

    body = response.json()
    assert body["read"] == 3  # four entries, one of them pending
    assert body["parked"] == 3
    assert body["known"] == 0
    assert body["accountId"] == str(account.id)

    rows = await parked(session, user)
    assert len(rows) == 3
    assert all(row.category is None for row in rows)

    booked = await session.execute(select(Transaction))
    assert booked.scalars().first() is None

    app.dependency_overrides.clear()


async def test_the_same_file_twice_changes_nothing(
    client: AsyncClient, session: AsyncSession
):
    """`AcctSvcrRef` is what makes this exact instead of a guess."""
    user = await make_user(session, "Owner")
    await make_account(session, user, iban=FILE_IBAN)
    await session.commit()
    sign_in(user)

    await client.post("/api/v1/imports", files=upload_file())
    second = await client.post("/api/v1/imports", files=upload_file())

    assert second.json()["parked"] == 0
    assert second.json()["known"] == 3
    assert len(await parked(session, user)) == 3

    app.dependency_overrides.clear()


async def test_an_unknown_iban_asks_once(client: AsyncClient, session: AsyncSession):
    """No account carries this IBAN yet, so the client is asked which one it is.

    Not an error: the file is fine, Duofy simply does not know the account. On
    the retry the IBAN is written onto the chosen account and never asked again.
    """
    user = await make_user(session, "Owner")
    account = await make_account(session, user)
    await session.commit()
    sign_in(user)

    asked = await client.post("/api/v1/imports", files=upload_file())
    assert asked.json()["unknownIban"] == FILE_IBAN
    assert asked.json()["parked"] == 0
    assert len(await parked(session, user)) == 0

    answered = await client.post(
        f"/api/v1/imports?account={account.id}", files=upload_file()
    )
    assert answered.json()["parked"] == 3

    await session.refresh(account)
    assert account.external_ref == FILE_IBAN

    again = await client.post("/api/v1/imports", files=upload_file())
    assert again.json()["known"] == 3

    app.dependency_overrides.clear()


async def test_balances_are_checked(client: AsyncClient, session: AsyncSession):
    """Page one alone does not add up — the report continues on page two.

    Worth reporting rather than hiding: the import cannot tell whether something
    was misread or whether a page is missing, but the user can.
    """
    user = await make_user(session, "Owner")
    await make_account(session, user, iban=FILE_IBAN)
    await session.commit()
    sign_in(user)

    one = await client.post("/api/v1/imports", files=upload_file())
    assert one.json()["balancesMatch"] is False

    whole = await client.post(
        "/api/v1/imports", files=upload_file("report_both_pages.zip")
    )
    assert whole.json()["balancesMatch"] is True

    app.dependency_overrides.clear()


async def test_something_that_is_not_a_bank_file(
    client: AsyncClient, session: AsyncSession
):
    user = await make_user(session, "Owner")
    await make_account(session, user, iban=FILE_IBAN)
    await session.commit()
    sign_in(user)

    response = await client.post(
        "/api/v1/imports",
        files={"file": ("umsaetze.csv", b"Datum;Betrag\n01.08.2026;-12,99\n", "text/csv")},
    )
    assert response.status_code == 422
    assert response.json()["detail"]["code"] == "not_a_bank_file"

    app.dependency_overrides.clear()


# --- Assigning and booking -------------------------------------------------


async def test_booking_needs_a_category(client: AsyncClient, session: AsyncSession):
    """Without a category there is no block, and without a block no quota."""
    user = await make_user(session, "Owner")
    await make_account(session, user, iban=FILE_IBAN)
    await session.commit()
    sign_in(user)
    await client.post("/api/v1/imports", files=upload_file())

    entry = (await parked(session, user))[0]
    response = await client.post(f"/api/v1/imports/{entry.id}/book")

    assert response.status_code == 403
    assert response.json()["detail"]["code"] == "category_missing"

    app.dependency_overrides.clear()


async def test_assign_then_book(client: AsyncClient, session: AsyncSession):
    """The whole way: park, give it a meaning, turn it into a booking."""
    user = await make_user(session, "Owner")
    account = await make_account(session, user, iban=FILE_IBAN)
    await session.commit()
    sign_in(user)
    await client.post("/api/v1/imports", files=upload_file())

    entry = (await parked(session, user))[0]
    assigned = await client.patch(
        f"/api/v1/imports/{entry.id}", json={"category": "housing.rent"}
    )
    assert assigned.status_code == 200
    # The block follows the category, it is never asked for.
    assert assigned.json()["block"] == "needs"

    booked = await client.post(f"/api/v1/imports/{entry.id}/book")
    assert booked.status_code == 200

    rows = await session.execute(select(Transaction))
    transaction = rows.scalars().one()
    assert transaction.account_id == account.id
    assert transaction.owner_id == user.id
    assert transaction.amount == entry.amount
    assert transaction.external_ref == entry.external_ref

    assert len(await parked(session, user)) == 2

    app.dependency_overrides.clear()


async def test_a_booked_entry_does_not_come_back(
    client: AsyncClient, session: AsyncSession
):
    """The identity survives in `Transaction.external_ref`."""
    user = await make_user(session, "Owner")
    await make_account(session, user, iban=FILE_IBAN)
    await session.commit()
    sign_in(user)
    await client.post("/api/v1/imports", files=upload_file())

    entry = (await parked(session, user))[0]
    await client.patch(f"/api/v1/imports/{entry.id}", json={"category": "housing.rent"})
    await client.post(f"/api/v1/imports/{entry.id}/book")

    again = await client.post("/api/v1/imports", files=upload_file())
    assert again.json()["parked"] == 0
    assert len(await parked(session, user)) == 2

    app.dependency_overrides.clear()


async def test_a_discarded_entry_does_not_come_back(
    client: AsyncClient, session: AsyncSession
):
    """Thrown out stays thrown out, even after re-uploading the same file.

    The row keeps existing with `discarded_at` set. Deleting it would mean
    throwing the same entry out again every month.
    """
    user = await make_user(session, "Owner")
    await make_account(session, user, iban=FILE_IBAN)
    await session.commit()
    sign_in(user)
    await client.post("/api/v1/imports", files=upload_file())

    entry = (await parked(session, user))[0]
    discarded = await client.delete(f"/api/v1/imports/{entry.id}")
    assert discarded.status_code == 200
    assert discarded.json()["discardedAt"] is not None

    listed = await client.get("/api/v1/imports")
    assert len(listed.json()) == 2

    again = await client.post("/api/v1/imports", files=upload_file())
    assert again.json()["parked"] == 0

    app.dependency_overrides.clear()


# --- Permissions -----------------------------------------------------------


async def test_importing_for_another_member(client: AsyncClient, session: AsyncSession):
    """Importing is a way of writing bookings, so it needs `edit` on accounts."""
    owner = await make_user(session, "Owner")
    helper = await make_user(session, "Helper")
    household = await make_household(session, "Shared")
    await add_member(session, household, owner)
    await add_member(session, household, helper)
    await make_account(session, owner, iban=FILE_IBAN)
    await session.commit()

    await grant_area(session, household, owner, "accounts", AccessLevel.VIEW)
    sign_in(helper)

    refused = await client.post(
        f"/api/v1/imports?owner={owner.id}", files=upload_file()
    )
    assert refused.status_code == 403

    await grant_area(session, household, owner, "accounts", AccessLevel.EDIT)
    allowed = await client.post(
        f"/api/v1/imports?owner={owner.id}", files=upload_file()
    )
    assert allowed.status_code == 201

    rows = await parked(session, owner)
    assert len(rows) == 3
    # The pile belongs to the owner, not to whoever uploaded it.
    assert all(row.owner_id == owner.id for row in rows)
    assert all(row.imported_by_id == helper.id for row in rows)

    app.dependency_overrides.clear()


async def test_a_stranger_cannot_import(client: AsyncClient, session: AsyncSession):
    owner = await make_user(session, "Owner")
    stranger = await make_user(session, "Stranger")
    await make_account(session, owner, iban=FILE_IBAN)
    await session.commit()
    sign_in(stranger)

    response = await client.post(
        f"/api/v1/imports?owner={owner.id}", files=upload_file()
    )
    assert response.status_code == 403
    assert len(await parked(session, owner)) == 0

    app.dependency_overrides.clear()


def test_the_upload_limit_is_declared():
    """A guard against raising it by accident: the parser holds the whole file."""
    from app.api.v1.imports import MAX_UPLOAD_BYTES

    assert MAX_UPLOAD_BYTES <= 20 * 1024 * 1024
    assert isinstance(uuid.UUID(int=0), uuid.UUID)


async def test_a_position_brings_its_own_category(
    client: AsyncClient, session: AsyncSession
):
    """Assigning a position settles the category — it is not asked twice.

    `PlanPosition.category` is not nullable, so a position always carries one.
    Letting the two disagree would leave the booking with a category its
    position does not share.
    """
    from app.models.enums import Block, Category
    from app.models.plan import Plan, PlanPosition

    user = await make_user(session, "Owner")
    await make_account(session, user, iban=FILE_IBAN)

    plan = Plan(user_id=user.id, year=2026, month=8)
    session.add(plan)
    await session.flush()
    position = PlanPosition(
        plan_id=plan.id,
        label="Miete",
        amount_planned=Decimal("890.00"),
        category=Category.HOUSING_RENT,
        block=Block.NEEDS,
        due_day=15,
    )
    session.add(position)
    await session.commit()

    sign_in(user)
    await client.post("/api/v1/imports", files=upload_file())
    entry = (await parked(session, user))[0]

    response = await client.patch(
        f"/api/v1/imports/{entry.id}", json={"positionId": str(position.id)}
    )
    assert response.status_code == 200
    assert response.json()["category"] == "housing.rent"
    assert response.json()["block"] == "needs"

    booked = await client.post(f"/api/v1/imports/{entry.id}/book")
    assert booked.status_code == 200

    rows = await session.execute(select(Transaction))
    assert rows.scalars().one().position_id == position.id

    app.dependency_overrides.clear()


async def test_the_order_survives_an_assignment(
    client: AsyncClient, session: AsyncSession
):
    """Entries must not jump when one of them is given a category.

    Six entries **on the same day**, written in one transaction — which is what
    an import does. `now()` is the transaction's start time, so `created_at` is
    identical across all six and cannot decide anything. Without a unique
    tiebreaker Postgres returns them in whatever order it likes, and the row
    that was just updated comes back last.

    The fixture cannot produce this: its five entries fall on five different
    days, so the date alone settles the order and the bug stays hidden.
    """
    user = await make_user(session, "Owner")
    account = await make_account(session, user, iban=FILE_IBAN)

    for number in range(6):
        session.add(
            ImportedEntry(
                owner_id=user.id,
                imported_by_id=user.id,
                account_id=account.id,
                external_ref=f"9{number:018d}",
                occurred_on=date(2026, 8, 14),
                value_on=date(2026, 8, 14),
                amount=Decimal("10.00") + number,
                incoming=False,
                purpose=f"Zeile {number}",
            )
        )
    await session.commit()
    sign_in(user)

    before = [row["id"] for row in (await client.get("/api/v1/imports")).json()]
    assert len(before) == 6

    middle = before[3]
    await client.patch(f"/api/v1/imports/{middle}", json={"category": "housing.rent"})

    after = [row["id"] for row in (await client.get("/api/v1/imports")).json()]
    assert after == before

    app.dependency_overrides.clear()


async def test_booking_keeps_the_counterparty(
    client: AsyncClient, session: AsyncSession
):
    """Name and IBAN survive on the booking.

    The parked row is deleted when it becomes a booking. Without these two
    columns the counterparty would only live on as free text in `note`, and
    nothing could ever be looked up by it — every assignment made by hand would
    be lost to the recognition meant to learn from it.
    """
    user = await make_user(session, "Owner")
    await make_account(session, user, iban=FILE_IBAN)
    await session.commit()
    sign_in(user)
    await client.post("/api/v1/imports", files=upload_file())

    entry = next(
        row for row in await parked(session, user) if row.counterparty_iban is not None
    )
    await client.patch(f"/api/v1/imports/{entry.id}", json={"category": "housing.rent"})
    await client.post(f"/api/v1/imports/{entry.id}/book")

    rows = await session.execute(select(Transaction))
    transaction = rows.scalars().one()
    assert transaction.counterparty_name == entry.counterparty_name
    assert transaction.counterparty_iban == entry.counterparty_iban

    app.dependency_overrides.clear()


async def test_a_savings_position_books_a_transfer(
    client: AsyncClient, session: AsyncSession
):
    """Assigning a savings position makes the booking a transfer.

    `PlanPosition.counter_account_id` says where the money goes, and `mark_paid`
    has always honoured it. Without the same here, a transfer to the savings
    account became an **expense**: the savings account never saw the money and
    the savings quota stayed empty while the money was demonstrably saved.
    """
    from app.models.enums import AccountType, Block, Category
    from app.models.plan import Plan, PlanPosition

    user = await make_user(session, "Owner")
    giro = await make_account(session, user, iban=FILE_IBAN)
    savings = Account(
        owner_id=user.id,
        name="Tagesgeld",
        type=AccountType.SAVINGS,
        opening_balance=Decimal("0.00"),
        opening_date=date(2026, 1, 1),
        counts_as_available=False,
    )
    session.add(savings)
    await session.flush()

    plan = Plan(user_id=user.id, year=2026, month=8)
    session.add(plan)
    await session.flush()
    position = PlanPosition(
        plan_id=plan.id,
        label="Rücklage",
        amount_planned=Decimal("890.00"),
        category=Category.FINANCE_SAVINGS,
        block=Block.SAVINGS,
        due_day=15,
        counter_account_id=savings.id,
    )
    session.add(position)
    await session.commit()

    sign_in(user)
    await client.post("/api/v1/imports", files=upload_file())
    entry = (await parked(session, user))[0]

    await client.patch(
        f"/api/v1/imports/{entry.id}", json={"positionId": str(position.id)}
    )
    await client.post(f"/api/v1/imports/{entry.id}/book")

    rows = await session.execute(select(Transaction))
    transaction = rows.scalars().one()
    assert transaction.account_id == giro.id
    assert transaction.counter_account_id == savings.id

    app.dependency_overrides.clear()


# --- Recognition -----------------------------------------------------------


async def test_no_suggestion_without_history(
    client: AsyncClient, session: AsyncSession
):
    """The very first import has nothing to go on, and says so by staying quiet."""
    user = await make_user(session, "Owner")
    await make_account(session, user, iban=FILE_IBAN)
    await session.commit()
    sign_in(user)
    await client.post("/api/v1/imports", files=upload_file())

    rows = (await client.get("/api/v1/imports")).json()
    assert all(row["suggestion"] is None for row in rows)

    app.dependency_overrides.clear()


async def test_booking_one_row_teaches_the_others(
    client: AsyncClient, session: AsyncSession
):
    """The case that makes the first import worth anything.

    Two entries share a counterparty. Book the first and the second has a
    suggestion immediately — no history from earlier months needed, the
    recognition learns inside the pile being worked through right now.
    """
    user = await make_user(session, "Owner")
    account = await make_account(session, user, iban=FILE_IBAN)

    for number, day in ((1, 3), (2, 17)):
        session.add(
            ImportedEntry(
                owner_id=user.id,
                imported_by_id=user.id,
                account_id=account.id,
                external_ref=f"8{number:018d}",
                occurred_on=date(2026, 8, day),
                value_on=date(2026, 8, day),
                amount=Decimal("20.00"),
                incoming=False,
                counterparty_name="Lebensmittelmarkt Nord",
                counterparty_iban="DE02300209000106531065",
            )
        )
    await session.commit()
    sign_in(user)

    rows = (await client.get("/api/v1/imports")).json()
    assert all(row["suggestion"] is None for row in rows)

    first = rows[0]["id"]
    await client.patch(
        f"/api/v1/imports/{first}", json={"category": "household.groceries"}
    )
    await client.post(f"/api/v1/imports/{first}/book")

    remaining = (await client.get("/api/v1/imports")).json()
    assert len(remaining) == 1
    assert remaining[0]["suggestion"]["category"] == "household.groceries"
    assert "IBAN" in remaining[0]["suggestion"]["reason"]

    app.dependency_overrides.clear()


async def test_a_card_payment_is_recognised_by_its_name(
    client: AsyncClient, session: AsyncSession
):
    """Card payments carry no counterparty IBAN, so the name has to do it.

    And it has to survive the way a shop writes itself: different case, a branch
    number, doubled spaces.
    """
    user = await make_user(session, "Owner")
    account = await make_account(session, user, iban=FILE_IBAN)

    session.add(
        Transaction(
            owner_id=user.id,
            account_id=account.id,
            occurred_on=date(2026, 7, 20),
            amount=Decimal("31.00"),
            category=Category.HOUSEHOLD_GROCERIES,
            block=Block.NEEDS,
            counterparty_name="REWE Markt GmbH",
        )
    )
    session.add(
        ImportedEntry(
            owner_id=user.id,
            imported_by_id=user.id,
            account_id=account.id,
            external_ref="8000000000000000099",
            occurred_on=date(2026, 8, 12),
            value_on=date(2026, 8, 12),
            amount=Decimal("18.40"),
            incoming=False,
            counterparty_name="rewe  markt 4471",
        )
    )
    await session.commit()
    sign_in(user)

    rows = (await client.get("/api/v1/imports")).json()
    assert rows[0]["suggestion"]["category"] == "household.groceries"
    assert "Namen" in rows[0]["suggestion"]["reason"]

    app.dependency_overrides.clear()


async def test_a_budget_position_comes_with_the_suggestion(
    client: AsyncClient, session: AsyncSession
):
    """A budget position is identified by its category alone.

    It fills up from many bookings over the month, so there is no amount to
    match and no due-date window to guess — the guessing is #61's problem, and
    this case does not have it.
    """
    from app.models.plan import Plan, PlanPosition

    user = await make_user(session, "Owner")
    account = await make_account(session, user, iban=FILE_IBAN)

    plan = Plan(user_id=user.id, year=2026, month=8)
    session.add(plan)
    await session.flush()
    budget = PlanPosition(
        plan_id=plan.id,
        label="Lebensmittel",
        amount_planned=Decimal("520.00"),
        category=Category.HOUSEHOLD_GROCERIES,
        block=Block.NEEDS,
        due_day=1,
        is_budget=True,
    )
    session.add(budget)
    session.add(
        Transaction(
            owner_id=user.id,
            account_id=account.id,
            occurred_on=date(2026, 7, 20),
            amount=Decimal("31.00"),
            category=Category.HOUSEHOLD_GROCERIES,
            block=Block.NEEDS,
            counterparty_iban="DE02300209000106531065",
            counterparty_name="Markt",
        )
    )
    session.add(
        ImportedEntry(
            owner_id=user.id,
            imported_by_id=user.id,
            account_id=account.id,
            external_ref="8000000000000000098",
            occurred_on=date(2026, 8, 12),
            value_on=date(2026, 8, 12),
            amount=Decimal("18.40"),
            incoming=False,
            counterparty_name="Markt",
            counterparty_iban="DE02300209000106531065",
        )
    )
    await session.commit()
    sign_in(user)

    suggestion = (await client.get("/api/v1/imports")).json()[0]["suggestion"]
    assert suggestion["category"] == "household.groceries"
    assert suggestion["positionId"] == str(budget.id)

    app.dependency_overrides.clear()


async def test_an_assigned_row_without_a_matching_budget_gets_nothing(
    client: AsyncClient, session: AsyncSession
):
    """A chosen category with no budget position for it: nothing to add."""
    user = await make_user(session, "Owner")
    account = await make_account(session, user, iban=FILE_IBAN)
    session.add(
        Transaction(
            owner_id=user.id,
            account_id=account.id,
            occurred_on=date(2026, 7, 20),
            amount=Decimal("31.00"),
            category=Category.HOUSEHOLD_GROCERIES,
            block=Block.NEEDS,
            counterparty_iban="DE02300209000106531065",
            counterparty_name="Markt",
        )
    )
    entry = ImportedEntry(
        owner_id=user.id,
        imported_by_id=user.id,
        account_id=account.id,
        external_ref="8000000000000000097",
        occurred_on=date(2026, 8, 12),
        value_on=date(2026, 8, 12),
        amount=Decimal("18.40"),
        incoming=False,
        counterparty_name="Markt",
        counterparty_iban="DE02300209000106531065",
        category=Category.LEISURE_DINING,
        block=Block.WANTS,
    )
    session.add(entry)
    await session.commit()
    sign_in(user)

    assert (await client.get("/api/v1/imports")).json()[0]["suggestion"] is None

    app.dependency_overrides.clear()


async def test_a_payment_service_gets_no_suggestion(
    client: AsyncClient, session: AsyncSession
):
    """One counterparty, many purposes — the case that would be confidently wrong.

    PayPal, Klarna, Amazon, a card statement: the recipient says nothing about
    what was bought. Suggesting the last category there is wrong most of the
    time, so the recognition stays quiet instead.
    """
    user = await make_user(session, "Owner")
    account = await make_account(session, user, iban=FILE_IBAN)
    paypal = "DE02100500000054540402"

    for day, category, block in (
        (5, Category.LEISURE_HOBBIES, Block.WANTS),
        (12, Category.HOUSEHOLD_CLOTHING, Block.NEEDS),
        (19, Category.LEISURE_ENTERTAINMENT, Block.WANTS),
    ):
        session.add(
            Transaction(
                owner_id=user.id,
                account_id=account.id,
                occurred_on=date(2026, 7, day),
                amount=Decimal("20.00"),
                category=category,
                block=block,
                counterparty_iban=paypal,
                counterparty_name="Zahlungsdienst",
            )
        )
    session.add(
        ImportedEntry(
            owner_id=user.id,
            imported_by_id=user.id,
            account_id=account.id,
            external_ref="8000000000000000096",
            occurred_on=date(2026, 8, 12),
            value_on=date(2026, 8, 12),
            amount=Decimal("31.00"),
            incoming=False,
            counterparty_name="Zahlungsdienst",
            counterparty_iban=paypal,
        )
    )
    await session.commit()
    sign_in(user)

    assert (await client.get("/api/v1/imports")).json()[0]["suggestion"] is None

    app.dependency_overrides.clear()


async def test_one_slip_does_not_poison_a_counterparty(
    client: AsyncClient, session: AsyncSession
):
    """A mistyped category falls out of the window instead of silencing the shop.

    Only the newest three are asked. Book the supermarket right twice after a
    slip and it speaks again — otherwise a single wrong click would cost the
    suggestion for good.
    """
    user = await make_user(session, "Owner")
    account = await make_account(session, user, iban=FILE_IBAN)
    shop = "DE02300209000106531065"

    for day, category, block in (
        (2, Category.LEISURE_DINING, Block.WANTS),  # der Ausrutscher
        (9, Category.HOUSEHOLD_GROCERIES, Block.NEEDS),
        (16, Category.HOUSEHOLD_GROCERIES, Block.NEEDS),
        (23, Category.HOUSEHOLD_GROCERIES, Block.NEEDS),
    ):
        session.add(
            Transaction(
                owner_id=user.id,
                account_id=account.id,
                occurred_on=date(2026, 7, day),
                amount=Decimal("20.00"),
                category=category,
                block=block,
                counterparty_iban=shop,
                counterparty_name="Markt",
            )
        )
    session.add(
        ImportedEntry(
            owner_id=user.id,
            imported_by_id=user.id,
            account_id=account.id,
            external_ref="8000000000000000095",
            occurred_on=date(2026, 8, 12),
            value_on=date(2026, 8, 12),
            amount=Decimal("18.40"),
            incoming=False,
            counterparty_name="Markt",
            counterparty_iban=shop,
        )
    )
    await session.commit()
    sign_in(user)

    suggestion = (await client.get("/api/v1/imports")).json()[0]["suggestion"]
    assert suggestion is not None
    assert suggestion["category"] == "household.groceries"

    app.dependency_overrides.clear()


async def test_your_own_category_still_gets_its_budget_position(
    client: AsyncClient, session: AsyncSession
):
    """Choosing a category by hand must not switch the help off.

    The budget position follows from the category alone — no learning, no
    tolerance, no guessing. Staying quiet here meant the import stopped helping
    exactly when the answer was most certain.
    """
    from app.models.plan import Plan, PlanPosition

    user = await make_user(session, "Owner")
    account = await make_account(session, user, iban=FILE_IBAN)

    plan = Plan(user_id=user.id, year=2026, month=8)
    session.add(plan)
    await session.flush()
    budget = PlanPosition(
        plan_id=plan.id,
        label="Lebensmittel",
        amount_planned=Decimal("520.00"),
        category=Category.HOUSEHOLD_GROCERIES,
        block=Block.NEEDS,
        due_day=1,
        is_budget=True,
    )
    session.add(budget)
    session.add(
        ImportedEntry(
            owner_id=user.id,
            imported_by_id=user.id,
            account_id=account.id,
            external_ref="8000000000000000094",
            occurred_on=date(2026, 8, 12),
            value_on=date(2026, 8, 12),
            amount=Decimal("18.40"),
            incoming=False,
            counterparty_name="Ein Laden ohne Geschichte",
        )
    )
    await session.commit()
    sign_in(user)

    entry = (await parked(session, user))[0]
    # Nichts gelernt, also zunächst kein Vorschlag.
    assert (await client.get("/api/v1/imports")).json()[0]["suggestion"] is None

    await client.patch(
        f"/api/v1/imports/{entry.id}", json={"category": "household.groceries"}
    )

    suggestion = (await client.get("/api/v1/imports")).json()[0]["suggestion"]
    assert suggestion is not None
    assert suggestion["positionId"] == str(budget.id)
    assert "Budgetposten" in suggestion["reason"]

    app.dependency_overrides.clear()
