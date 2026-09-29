"""The household every person belongs to.

Everybody is in exactly one: their own from the day they register, or the one they
were invited into. So there is always a household to look at, and a household view
never has to ask "which one".
"""

from sqlalchemy.ext.asyncio import AsyncSession

from app.models.enums import Role
from app.models.household import Household, HouseholdMember
from app.models.user import User


def household_name_for(user: User) -> str:
    return f"Haushalt von {user.first_name}"[:100]


async def create_own_household(session: AsyncSession, user: User) -> Household:
    """A household with `user` as its only member and owner. Flushes, does not commit,
    so the caller decides what else belongs in the same transaction."""
    household = Household(name=household_name_for(user))
    household.members.append(HouseholdMember(user_id=user.id, role=Role.OWNER))
    session.add(household)
    await session.flush()
    return household
