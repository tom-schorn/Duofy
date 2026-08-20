"""Money moving between the user's own accounts (#71).

A transfer appears in **both** statements — once outgoing, once incoming. Booked
twice it becomes an expense that was not one and an income that was not one. The
balances still come out right, which is what makes it easy to miss and worth a
file of its own.

The entries here are parked directly rather than uploaded: the fixtures report
one fixed set of counterparties, and what is under test is what happens when a
counterparty IBAN turns out to be one of the user's own accounts.
"""

import uuid
from datetime import date
from decimal import Decimal

from httpx import AsyncClient
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.main import app
from app.models.account import Account
from app.models.enums import AccountType, Block, Category
from app.models.imported_entry import ImportedEntry
from app.models.plan import Plan, PlanPosition
from app.models.transaction import Transaction
from app.models.user import User
from tests.test_area_permissions import make_user
from tests.test_imports import sign_in

GIRO_IBAN = "DE02120300000000202051"
SAVINGS_IBAN = "DE02500105170137075030"


async def make_account(
    session: AsyncSession,
    owner: User,
    name: str,
    *,
    iban: str | None = None,
    type: AccountType = AccountType.CHECKING,
) -> Account:
    account = Account(
        owner_id=owner.id,
        name=name,
        type=type,
        opening_balance=Decimal("0.00"),
        opening_date=date(2026, 1, 1),
        external_ref=iban,
        counts_as_available=type is not AccountType.SAVINGS,
    )
    session.add(account)
    await session.flush()
    return account


async def park(
    session: AsyncSession,
    owner: User,
    account: Account,
    *,
    amount: str,
    incoming: bool,
    on: date = date(2026, 8, 15),
    iban: str | None = None,
    name: str = "Testkonto Duofy",
    ref: str | None = None,
) -> ImportedEntry:
    entry = ImportedEntry(
        owner_id=owner.id,
        imported_by_id=owner.id,
        account_id=account.id,
        external_ref=ref or f"ref-{uuid.uuid4().hex[:12]}",
        occurred_on=on,
        value_on=on,
        amount=Decimal(amount),
        incoming=incoming,
        counterparty_name=name,
        counterparty_iban=iban,
        purpose="Übertrag",
    )
    session.add(entry)
    await session.flush()
    return entry


async def suggestion_for(client: AsyncClient, entry_id: uuid.UUID) -> dict | None:
    response = await client.get("/api/v1/imports")
    assert response.status_code == 200
    for row in response.json():
        if row["id"] == str(entry_id):
            return row["suggestion"]
    raise AssertionError("entry is not in the parking area")


# --- Recognising one ------------------------------------------------------


async def test_an_own_iban_makes_it_a_transfer(
    client: AsyncClient, session: AsyncSession
):
    """The counterparty is the user's savings account, so this is not spending.

    Asking for a category here gets an answer that is wrong however it is given:
    the money has not been spent, it has moved.
    """
    user = await make_user(session, "Owner")
    giro = await make_account(session, user, "Giro", iban=GIRO_IBAN)
    savings = await make_account(
        session, user, "Tagesgeld", iban=SAVINGS_IBAN, type=AccountType.SAVINGS
    )
    entry = await park(session, user, giro, amount="200.00", incoming=False, iban=SAVINGS_IBAN)
    await session.commit()
    sign_in(user)

    found = await suggestion_for(client, entry.id)
    assert found is not None
    assert found["kind"] == "transfer"
    assert found["counterAccountId"] == str(savings.id)
    assert found["counterAccountName"] == "Tagesgeld"
    assert found["category"] is None

    app.dependency_overrides.clear()


async def test_an_account_without_an_iban_cannot_be_recognised(
    client: AsyncClient, session: AsyncSession
):
    """The savings account has no IBAN, so nothing links the two.

    This is the whole reason the IBAN is editable by hand: the account people
    move money to most is the one that never delivers a statement to import.
    """
    user = await make_user(session, "Owner")
    giro = await make_account(session, user, "Giro", iban=GIRO_IBAN)
    await make_account(session, user, "Tagesgeld", type=AccountType.SAVINGS)
    entry = await park(session, user, giro, amount="200.00", incoming=False, iban=SAVINGS_IBAN)
    await session.commit()
    sign_in(user)

    assert await suggestion_for(client, entry.id) is None

    app.dependency_overrides.clear()


async def test_a_stranger_with_the_same_name_is_not_a_transfer(
    client: AsyncClient, session: AsyncSession
):
    """Only the IBAN counts. The counterparty name on a statement is the account
    holder's, so paying oneself at a till would otherwise look identical."""
    user = await make_user(session, "Owner")
    giro = await make_account(session, user, "Giro", iban=GIRO_IBAN)
    await make_account(
        session, user, "Tagesgeld", iban=SAVINGS_IBAN, type=AccountType.SAVINGS
    )
    entry = await park(
        session,
        user,
        giro,
        amount="200.00",
        incoming=False,
        iban="DE02300209000106531065",
        name="Tagesgeld",
    )
    await session.commit()
    sign_in(user)

    found = await suggestion_for(client, entry.id)
    assert found is None or found["kind"] != "transfer"

    app.dependency_overrides.clear()


# --- Booking one ----------------------------------------------------------


async def test_a_transfer_books_without_a_category(
    client: AsyncClient, session: AsyncSession
):
    """`category_missing` guarded every booking. A transfer has no category by
    design, and the check constraint on `transactions` allows exactly that."""
    user = await make_user(session, "Owner")
    giro = await make_account(session, user, "Giro", iban=GIRO_IBAN)
    savings = await make_account(
        session, user, "Tagesgeld", iban=SAVINGS_IBAN, type=AccountType.SAVINGS
    )
    entry = await park(session, user, giro, amount="200.00", incoming=False, iban=SAVINGS_IBAN)
    await session.commit()
    sign_in(user)

    assigned = await client.patch(
        f"/api/v1/imports/{entry.id}", json={"counterAccountId": str(savings.id)}
    )
    assert assigned.status_code == 200
    assert assigned.json()["counterAccountId"] == str(savings.id)

    booked = await client.post(f"/api/v1/imports/{entry.id}/book")
    assert booked.status_code == 200

    rows = await session.execute(select(Transaction))
    transaction = rows.scalars().one()
    assert transaction.account_id == giro.id
    assert transaction.counter_account_id == savings.id
    assert transaction.category is None
    assert transaction.block is None

    app.dependency_overrides.clear()


async def test_an_incoming_transfer_books_the_other_way_round(
    client: AsyncClient, session: AsyncSession
):
    """A transfer **leaves** `account_id` and arrives at `counter_account_id`.

    Read on the receiving side, this account is the arrival — so the two swap.
    Booking it unswapped would move the same money the wrong way on both
    accounts at once, and the savings account would end up paying out.
    """
    user = await make_user(session, "Owner")
    giro = await make_account(session, user, "Giro", iban=GIRO_IBAN)
    savings = await make_account(
        session, user, "Tagesgeld", iban=SAVINGS_IBAN, type=AccountType.SAVINGS
    )
    # The statement of the savings account: money arriving from the current one.
    entry = await park(
        session, user, savings, amount="200.00", incoming=True, iban=GIRO_IBAN
    )
    await session.commit()
    sign_in(user)

    await client.patch(
        f"/api/v1/imports/{entry.id}", json={"counterAccountId": str(giro.id)}
    )
    await client.post(f"/api/v1/imports/{entry.id}/book")

    rows = await session.execute(select(Transaction))
    transaction = rows.scalars().one()
    assert transaction.account_id == giro.id
    assert transaction.counter_account_id == savings.id

    app.dependency_overrides.clear()


async def test_a_transfer_cannot_point_at_its_own_account(
    client: AsyncClient, session: AsyncSession
):
    """Booking from an account to itself would move the balance twice."""
    user = await make_user(session, "Owner")
    giro = await make_account(session, user, "Giro", iban=GIRO_IBAN)
    entry = await park(session, user, giro, amount="200.00", incoming=False)
    await session.commit()
    sign_in(user)

    response = await client.patch(
        f"/api/v1/imports/{entry.id}", json={"counterAccountId": str(giro.id)}
    )
    assert response.status_code == 403
    assert response.json()["detail"]["code"] == "transfer_needs_two_accounts"

    app.dependency_overrides.clear()


async def test_a_category_takes_the_transfer_mark_off(
    client: AsyncClient, session: AsyncSession
):
    """A purpose and a transfer cannot both be true, so the later answer wins.

    Silently ignoring the category would leave the row looking categorised and
    booking as a movement.
    """
    user = await make_user(session, "Owner")
    giro = await make_account(session, user, "Giro", iban=GIRO_IBAN)
    savings = await make_account(
        session, user, "Tagesgeld", iban=SAVINGS_IBAN, type=AccountType.SAVINGS
    )
    entry = await park(session, user, giro, amount="200.00", incoming=False, iban=SAVINGS_IBAN)
    await session.commit()
    sign_in(user)

    await client.patch(
        f"/api/v1/imports/{entry.id}", json={"counterAccountId": str(savings.id)}
    )
    response = await client.patch(
        f"/api/v1/imports/{entry.id}", json={"category": "household.groceries"}
    )
    assert response.status_code == 200
    assert response.json()["counterAccountId"] is None
    assert response.json()["category"] == "household.groceries"

    app.dependency_overrides.clear()


# --- The other side -------------------------------------------------------


async def test_the_other_side_of_a_booked_movement_is_not_offered_again(
    client: AsyncClient, session: AsyncSession
):
    """The book already holds this movement; this entry is its second half.

    `external_ref` cannot see it — the two sides carry different references,
    each bank numbering its own statement. So the movement itself is matched.
    """
    user = await make_user(session, "Owner")
    giro = await make_account(session, user, "Giro", iban=GIRO_IBAN)
    savings = await make_account(
        session, user, "Tagesgeld", iban=SAVINGS_IBAN, type=AccountType.SAVINGS
    )
    session.add(
        Transaction(
            owner_id=user.id,
            account_id=giro.id,
            counter_account_id=savings.id,
            occurred_on=date(2026, 8, 15),
            amount=Decimal("200.00"),
            note="Übertrag",
        )
    )
    # Two days later the savings account's own statement arrives.
    entry = await park(
        session,
        user,
        savings,
        amount="200.00",
        incoming=True,
        on=date(2026, 8, 17),
        iban=GIRO_IBAN,
    )
    await session.commit()
    sign_in(user)

    found = await suggestion_for(client, entry.id)
    assert found is not None
    assert found["kind"] == "already_booked"
    assert found["counterAccountName"] == "Giro"

    app.dependency_overrides.clear()


async def test_a_movement_a_fortnight_apart_is_a_second_movement(
    client: AsyncClient, session: AsyncSession
):
    """Outside the window it is not the same money, and it may be booked."""
    user = await make_user(session, "Owner")
    giro = await make_account(session, user, "Giro", iban=GIRO_IBAN)
    savings = await make_account(
        session, user, "Tagesgeld", iban=SAVINGS_IBAN, type=AccountType.SAVINGS
    )
    session.add(
        Transaction(
            owner_id=user.id,
            account_id=giro.id,
            counter_account_id=savings.id,
            occurred_on=date(2026, 8, 1),
            amount=Decimal("200.00"),
            note="Übertrag",
        )
    )
    entry = await park(
        session,
        user,
        savings,
        amount="200.00",
        incoming=True,
        on=date(2026, 8, 15),
        iban=GIRO_IBAN,
    )
    await session.commit()
    sign_in(user)

    found = await suggestion_for(client, entry.id)
    assert found is not None
    assert found["kind"] == "transfer"

    app.dependency_overrides.clear()


async def test_one_booking_answers_for_one_entry_only(
    client: AsyncClient, session: AsyncSession
):
    """Two transfers of 200 € in one week are two movements.

    Letting a single booking cover both would hide the second one for good — and
    a standing order is the same amount every month, so this is the normal case,
    not an edge one.
    """
    user = await make_user(session, "Owner")
    giro = await make_account(session, user, "Giro", iban=GIRO_IBAN)
    savings = await make_account(
        session, user, "Tagesgeld", iban=SAVINGS_IBAN, type=AccountType.SAVINGS
    )
    session.add(
        Transaction(
            owner_id=user.id,
            account_id=giro.id,
            counter_account_id=savings.id,
            occurred_on=date(2026, 8, 15),
            amount=Decimal("200.00"),
            note="Übertrag",
        )
    )
    first = await park(
        session,
        user,
        savings,
        amount="200.00",
        incoming=True,
        on=date(2026, 8, 15),
        iban=GIRO_IBAN,
    )
    second = await park(
        session,
        user,
        savings,
        amount="200.00",
        incoming=True,
        on=date(2026, 8, 16),
        iban=GIRO_IBAN,
    )
    await session.commit()
    sign_in(user)

    kinds = sorted(
        [
            (await suggestion_for(client, first.id))["kind"],
            (await suggestion_for(client, second.id))["kind"],
        ]
    )
    assert kinds == ["already_booked", "transfer"]

    app.dependency_overrides.clear()


async def test_booking_one_side_recognises_the_other(
    client: AsyncClient, session: AsyncSession
):
    """Both halves parked at once: booking the first settles the second.

    The suggestion is worked out while the list is read, so this needs no job and
    no queue — the next reload has the answer.
    """
    user = await make_user(session, "Owner")
    giro = await make_account(session, user, "Giro", iban=GIRO_IBAN)
    savings = await make_account(
        session, user, "Tagesgeld", iban=SAVINGS_IBAN, type=AccountType.SAVINGS
    )
    out = await park(
        session, user, giro, amount="200.00", incoming=False, iban=SAVINGS_IBAN
    )
    back = await park(
        session, user, savings, amount="200.00", incoming=True, iban=GIRO_IBAN
    )
    await session.commit()
    sign_in(user)

    assert (await suggestion_for(client, back.id))["kind"] == "transfer"

    await client.patch(
        f"/api/v1/imports/{out.id}", json={"counterAccountId": str(savings.id)}
    )
    await client.post(f"/api/v1/imports/{out.id}/book")

    assert (await suggestion_for(client, back.id))["kind"] == "already_booked"

    app.dependency_overrides.clear()


async def test_a_booked_transfer_does_not_come_back_on_either_side(
    client: AsyncClient, session: AsyncSession
):
    """Duplicate detection has to look at both account columns.

    An entry read on the receiving side becomes a booking whose `account_id` is
    the *other* account. Asking only for `account_id` would park it again on the
    next import of that same file.
    """
    user = await make_user(session, "Owner")
    giro = await make_account(session, user, "Giro", iban=GIRO_IBAN)
    savings = await make_account(
        session, user, "Tagesgeld", iban=SAVINGS_IBAN, type=AccountType.SAVINGS
    )
    entry = await park(
        session,
        user,
        savings,
        amount="200.00",
        incoming=True,
        iban=GIRO_IBAN,
        ref="9100000000000000042",
    )
    await session.commit()
    sign_in(user)

    await client.patch(
        f"/api/v1/imports/{entry.id}", json={"counterAccountId": str(giro.id)}
    )
    await client.post(f"/api/v1/imports/{entry.id}/book")

    from app.api.v1.imports import _already_known

    known = await _already_known(session, savings.id, ["9100000000000000042"])
    assert known == {"9100000000000000042"}

    app.dependency_overrides.clear()


# --- The month after ------------------------------------------------------


async def make_position(
    session: AsyncSession,
    user: User,
    year: int,
    month: int,
    *,
    label: str,
    amount: str,
    category: Category = Category.HOUSING_RENT,
) -> PlanPosition:
    plan = (
        await session.execute(
            select(Plan).where(
                Plan.user_id == user.id, Plan.year == year, Plan.month == month
            )
        )
    ).scalars().first()
    if plan is None:
        plan = Plan(user_id=user.id, year=year, month=month)
        session.add(plan)
        await session.flush()
    position = PlanPosition(
        plan_id=plan.id,
        label=label,
        amount_planned=Decimal(amount),
        category=category,
        block=Block.NEEDS,
        due_day=1,
    )
    session.add(position)
    await session.flush()
    return position


async def test_a_position_from_the_month_after_is_offered(
    client: AsyncClient, session: AsyncSession
):
    """Rent leaves the account on the 28th for the month starting on the 1st.

    Offering only the calendar month means the entries easiest to place are the
    ones with no position to place them on.
    """
    user = await make_user(session, "Owner")
    giro = await make_account(session, user, "Giro", iban=GIRO_IBAN)
    position = await make_position(
        session, user, 2026, 9, label="Miete", amount="890.00"
    )
    entry = await park(
        session,
        user,
        giro,
        amount="890.00",
        incoming=False,
        on=date(2026, 8, 28),
        name="Wohnungsgesellschaft",
    )
    await session.commit()
    sign_in(user)

    await client.patch(
        f"/api/v1/imports/{entry.id}", json={"category": "housing.rent"}
    )
    found = await suggestion_for(client, entry.id)
    assert found is not None
    assert found["positionId"] == str(position.id)
    assert "Folgemonat" in found["reason"]

    app.dependency_overrides.clear()


async def test_the_booking_month_decides_alone_where_it_has_positions(
    client: AsyncClient, session: AsyncSession
):
    """An ambiguous month is not handed on to the next one.

    Two candidates and no amount close enough means "I don't know". Reaching
    into September to avoid saying so would move the choice a month sideways,
    which is the worse of the two answers.
    """
    user = await make_user(session, "Owner")
    giro = await make_account(session, user, "Giro", iban=GIRO_IBAN)
    await make_position(session, user, 2026, 8, label="Miete A", amount="500.00")
    await make_position(session, user, 2026, 8, label="Miete B", amount="501.00")
    await make_position(session, user, 2026, 9, label="Miete", amount="890.00")
    entry = await park(
        session,
        user,
        giro,
        amount="890.00",
        incoming=False,
        on=date(2026, 8, 28),
        name="Wohnungsgesellschaft",
    )
    await session.commit()
    sign_in(user)

    await client.patch(
        f"/api/v1/imports/{entry.id}", json={"category": "housing.rent"}
    )
    assert await suggestion_for(client, entry.id) is None

    app.dependency_overrides.clear()


# --- Without an IBAN ------------------------------------------------------


async def test_two_nameless_halves_find_each_other(
    client: AsyncClient, session: AsyncSession
):
    """Not every bank names the other side — a card top-up often names nobody.

    What is left is the shape of the movement: same amount, opposite directions,
    two accounts of one person, days apart. Offered as a question, never applied.
    """
    user = await make_user(session, "Owner")
    giro = await make_account(session, user, "Giro", iban=GIRO_IBAN)
    card = await make_account(session, user, "Kreditkarte", type=AccountType.CREDIT_CARD)
    out = await park(
        session, user, giro, amount="150.00", incoming=False, name="Kartenaufladung"
    )
    into = await park(
        session,
        user,
        card,
        amount="150.00",
        incoming=True,
        on=date(2026, 8, 17),
        name="Gutschrift",
    )
    await session.commit()
    sign_in(user)

    for entry, other in ((out, card), (into, giro)):
        found = await suggestion_for(client, entry.id)
        assert found is not None
        assert found["kind"] == "transfer"
        assert found["certain"] is False
        assert found["counterAccountId"] == str(other.id)

    app.dependency_overrides.clear()


async def test_a_named_third_party_is_never_paired(
    client: AsyncClient, session: AsyncSession
):
    """An entry whose counterparty IBAN belongs to somebody else is a real
    payment. No coincidence of amount and date makes it a transfer."""
    user = await make_user(session, "Owner")
    giro = await make_account(session, user, "Giro", iban=GIRO_IBAN)
    card = await make_account(session, user, "Kreditkarte", type=AccountType.CREDIT_CARD)
    salary = await park(
        session,
        user,
        giro,
        amount="150.00",
        incoming=True,
        iban="DE02300209000106531065",
        name="Arbeitgeber",
    )
    await park(session, user, card, amount="150.00", incoming=False, name="Abbuchung")
    await session.commit()
    sign_in(user)

    found = await suggestion_for(client, salary.id)
    assert found is None or found["kind"] != "transfer"

    app.dependency_overrides.clear()


async def test_two_possible_mates_mean_silence(
    client: AsyncClient, session: AsyncSession
):
    """Two candidates mean the amount identifies nothing.

    The same silence as everywhere else in the import: no suggestion beats a
    plausible wrong one.
    """
    user = await make_user(session, "Owner")
    giro = await make_account(session, user, "Giro", iban=GIRO_IBAN)
    card = await make_account(session, user, "Kreditkarte", type=AccountType.CREDIT_CARD)
    out = await park(session, user, giro, amount="150.00", incoming=False, name="Abgang")
    await park(session, user, card, amount="150.00", incoming=True, name="Eingang A")
    await park(
        session,
        user,
        card,
        amount="150.00",
        incoming=True,
        on=date(2026, 8, 16),
        name="Eingang B",
    )
    await session.commit()
    sign_in(user)

    found = await suggestion_for(client, out.id)
    assert found is None or found["kind"] != "transfer"

    app.dependency_overrides.clear()


async def test_the_nameless_half_is_still_recognised_after_the_first_is_booked(
    client: AsyncClient, session: AsyncSession
):
    """The gap this closes: booking the first half deletes it.

    Once it is gone the second has nothing left to pair with, so a recognition
    that only ever compared parked rows would go quiet exactly when it matters —
    and the second half would be booked as a real expense.
    """
    user = await make_user(session, "Owner")
    giro = await make_account(session, user, "Giro", iban=GIRO_IBAN)
    card = await make_account(session, user, "Kreditkarte", type=AccountType.CREDIT_CARD)
    out = await park(
        session, user, giro, amount="150.00", incoming=False, name="Kartenaufladung"
    )
    into = await park(
        session,
        user,
        card,
        amount="150.00",
        incoming=True,
        on=date(2026, 8, 17),
        name="Gutschrift",
    )
    await session.commit()
    sign_in(user)

    await client.patch(
        f"/api/v1/imports/{out.id}", json={"counterAccountId": str(card.id)}
    )
    await client.post(f"/api/v1/imports/{out.id}/book")

    found = await suggestion_for(client, into.id)
    assert found is not None
    assert found["kind"] == "already_booked"
    assert found["certain"] is False
    assert found["counterAccountName"] == "Giro"

    app.dependency_overrides.clear()


async def test_a_booked_movement_the_other_way_round_is_not_a_match(
    client: AsyncClient, session: AsyncSession
):
    """Direction decides. Money arriving here can only be the target of a
    transfer, never its source — ignoring that would pair an entry with a
    movement that ran the opposite way."""
    user = await make_user(session, "Owner")
    giro = await make_account(session, user, "Giro", iban=GIRO_IBAN)
    savings = await make_account(
        session, user, "Tagesgeld", iban=SAVINGS_IBAN, type=AccountType.SAVINGS
    )
    # Money moved savings -> giro. The parked entry has money leaving giro.
    session.add(
        Transaction(
            owner_id=user.id,
            account_id=savings.id,
            counter_account_id=giro.id,
            occurred_on=date(2026, 8, 15),
            amount=Decimal("200.00"),
            note="Übertrag",
        )
    )
    entry = await park(
        session, user, giro, amount="200.00", incoming=False, iban=SAVINGS_IBAN
    )
    await session.commit()
    sign_in(user)

    found = await suggestion_for(client, entry.id)
    assert found is not None
    assert found["kind"] == "transfer"

    app.dependency_overrides.clear()
