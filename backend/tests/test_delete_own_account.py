"""Deleting one's own account for good (#221).

The tricky part is the order: `transactions.account_id` and `.counter_account_id`
are `RESTRICT`, so nothing may cascade-delete an account while a booking still
points at it. The other question is what happens to a household this person
owns — ownership has to land somewhere, or the household is left with nobody who
can rename it, invite anyone or change its quotas.
"""

from datetime import date
from decimal import Decimal

from httpx import AsyncClient
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.account import Account
from app.models.commitment import Commitment
from app.models.enums import AccountType, Budget, Category, CommitmentType, Role
from app.models.household import Household, HouseholdMember
from app.models.plan import Plan, PlanPosition, PlanPositionChange
from app.models.transaction import Transaction
from app.models.user import User
from tests.test_area_permissions import make_household, make_user
from tests.test_delegation import sign_in
from tests.test_refresh_tokens import PASSWORD, register_and_login


async def make_owned_household(session: AsyncSession, owner: User, partner: User) -> Household:
    household = await make_household(session, "Shared")
    session.add(HouseholdMember(household_id=household.id, user_id=owner.id, role=Role.OWNER))
    session.add(HouseholdMember(household_id=household.id, user_id=partner.id, role=Role.MEMBER))
    await session.flush()
    return household


async def make_account(session: AsyncSession, owner: User, name: str = "Girokonto") -> Account:
    account = Account(
        owner_id=owner.id,
        name=name,
        type=AccountType.CHECKING,
        opening_balance=Decimal("100.00"),
        opening_date=date(2026, 1, 1),
    )
    session.add(account)
    await session.flush()
    return account


async def make_shared_position(
    session: AsyncSession, owner: User, household_id, label: str
) -> PlanPosition:
    plan = await session.scalar(select(Plan).where(Plan.user_id == owner.id))
    if plan is None:
        plan = Plan(user_id=owner.id, year=2026, month=9)
        session.add(plan)
        await session.flush()
    position = PlanPosition(
        plan_id=plan.id,
        household_id=household_id,
        label=label,
        amount_planned=Decimal("30.00"),
        category=Category.LEISURE_SUBSCRIPTIONS,
        budget=Budget.WANTS,
        due_day=5,
    )
    session.add(position)
    await session.flush()
    return position


async def test_deletes_everything_and_hands_household_ownership_to_the_partner(
    client: AsyncClient, session: AsyncSession
) -> None:
    owner = await make_user(session, "Owner")
    partner = await make_user(session, "Partner")
    household = await make_owned_household(session, owner, partner)
    household_id = household.id

    checking = await make_account(session, owner, "Girokonto")
    savings = await make_account(session, owner, "Sparkonto")
    session.add(
        Transaction(
            owner_id=owner.id,
            account_id=checking.id,
            counter_account_id=savings.id,
            occurred_on=date(2026, 9, 10),
            amount=Decimal("20.00"),
        )
    )
    session.add(
        Transaction(
            owner_id=owner.id,
            account_id=checking.id,
            occurred_on=date(2026, 9, 12),
            amount=Decimal("15.00"),
            category=Category.LEISURE_SUBSCRIPTIONS,
            budget=Budget.WANTS,
        )
    )
    session.add(
        Commitment(
            owner_id=owner.id,
            type=CommitmentType.CONTRACT,
            name="Owner contract",
            amount=Decimal("50.00"),
            category=Category.LEISURE_SUBSCRIPTIONS,
            budget=Budget.WANTS,
            interval_months=1,
            first_due_date=date(2026, 1, 1),
        )
    )
    owners_position = await make_shared_position(session, owner, household_id, "Owner position")
    partners_position = await make_shared_position(
        session, partner, household_id, "Partner position"
    )
    session.add(
        PlanPositionChange(
            position_id=partners_position.id,
            changed_by_id=owner.id,
            field="amount_planned",
            old_value="30.00",
            new_value="35.00",
        )
    )
    await session.commit()
    owner_id, partner_id = owner.id, partner.id
    owners_position_id, partners_position_id = owners_position.id, partners_position.id
    sign_in(owner)

    response = await client.delete("/api/v1/users/me")

    assert response.status_code == 204
    assert await session.get(User, owner_id) is None
    assert await session.scalar(select(Account).where(Account.owner_id == owner_id)) is None
    assert await session.scalar(select(Transaction).where(Transaction.owner_id == owner_id)) is None
    assert await session.scalar(select(Commitment).where(Commitment.owner_id == owner_id)) is None
    assert await session.scalar(select(Plan).where(Plan.user_id == owner_id)) is None
    # `session.get()` would hand back the identity-mapped Python object without
    # asking the database — the position went via the CASCADE from `users`, not
    # through the ORM, so a real SELECT is the only way to see that it is gone.
    assert (
        await session.scalar(select(PlanPosition).where(PlanPosition.id == owners_position_id))
        is None
    )

    # The household survives; ownership passed to the partner, the only other
    # member left to promote.
    partner_membership = await session.scalar(
        select(HouseholdMember).where(
            HouseholdMember.household_id == household_id, HouseholdMember.user_id == partner_id
        )
    )
    assert partner_membership is not None
    assert partner_membership.role is Role.OWNER
    assert await session.get(Household, household_id) is not None

    # The partner's own position is untouched, but the household plan the partner
    # sees no longer carries the deleted person's position — it is gone outright.
    sign_in(partner)
    plan_response = await client.get(f"/api/v1/plans/household/{household_id}/2026/9")
    assert plan_response.status_code == 200
    labels = [position["label"] for position in plan_response.json()["positions"]]
    assert labels == ["Partner position"]
    assert (
        await session.scalar(select(PlanPosition).where(PlanPosition.id == partners_position_id))
        is not None
    )

    # The change the deleted person left on the partner's position stays as part
    # of that position's history, just without an author (#66).
    change = await session.scalar(
        select(PlanPositionChange).where(PlanPositionChange.position_id == partners_position_id)
    )
    assert change is not None
    assert change.changed_by_id is None


async def test_deletes_a_household_left_with_no_members(
    client: AsyncClient, session: AsyncSession
) -> None:
    owner = await make_user(session, "Solo")
    household = await make_household(session, "Alone")
    session.add(HouseholdMember(household_id=household.id, user_id=owner.id, role=Role.OWNER))
    await session.commit()
    household_id = household.id
    sign_in(owner)

    response = await client.delete("/api/v1/users/me")

    assert response.status_code == 204
    assert await session.get(Household, household_id) is None


async def test_the_last_admin_cannot_delete_their_own_account(
    client: AsyncClient, session: AsyncSession
) -> None:
    admin = await make_user(session, "Admin")
    admin.is_superuser = True
    await session.commit()
    sign_in(admin)

    response = await client.delete("/api/v1/users/me")

    assert response.status_code == 409
    assert response.json()["detail"]["code"] == "last_admin"
    assert await session.get(User, admin.id) is not None


async def test_an_admin_can_delete_their_account_if_another_admin_remains(
    client: AsyncClient, session: AsyncSession
) -> None:
    admin = await make_user(session, "Admin")
    admin.is_superuser = True
    other_admin = await make_user(session, "OtherAdmin")
    other_admin.is_superuser = True
    await session.commit()
    admin_id = admin.id
    sign_in(admin)

    response = await client.delete("/api/v1/users/me")

    assert response.status_code == 204
    assert await session.get(User, admin_id) is None


async def test_signing_in_afterwards_fails(client: AsyncClient) -> None:
    email = "leaving@example.org"
    await register_and_login(client, email)

    login = await client.post("/api/v1/auth/login", data={"username": email, "password": PASSWORD})
    token = login.json()["access_token"]
    headers = {"Authorization": f"Bearer {token}"}

    delete_response = await client.delete("/api/v1/users/me", headers=headers)
    assert delete_response.status_code == 204

    retry = await client.post("/api/v1/auth/login", data={"username": email, "password": PASSWORD})
    assert retry.status_code == 400
    assert retry.json()["detail"]["code"] == "LOGIN_BAD_CREDENTIALS"
