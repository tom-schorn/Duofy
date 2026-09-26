import uuid
from collections.abc import AsyncGenerator
from datetime import UTC, datetime
from typing import Any

from fastapi import Depends, HTTPException, status
from fastapi_users import BaseUserManager, FastAPIUsers, UUIDIDMixin
from fastapi_users.authentication import AuthenticationBackend, BearerTransport, JWTStrategy
from fastapi_users.db import SQLAlchemyUserDatabase
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.config import settings
from app.db.session import get_session
from app.models.instance_invitation import InstanceInvitation
from app.models.user import User


class RegistrationUserDatabase(SQLAlchemyUserDatabase):
    """Creates the user and redeems the invitation in one transaction.

    fastapi-users commits inside `create`. Marking the invitation used from the
    route afterwards would leave a window in which the user exists and the token is
    still good — or the other way round. Doing it here, before that one commit, keeps
    both or neither.
    """

    #: Set by the register route when the registration is paid for with an invitation.
    invitation: InstanceInvitation | None = None

    async def create(self, create_dict: dict[str, Any]) -> User:
        # The configured first admin is admin from the moment they exist, not only
        # after the next restart.
        if settings.is_admin_email(create_dict["email"]):
            create_dict = {**create_dict, "is_superuser": True}
        user = self.user_table(**create_dict)
        self.session.add(user)
        await self.session.flush()
        if self.invitation is not None:
            self.invitation.used_at = datetime.now(UTC)
            self.invitation.used_by_id = user.id
        await self.session.commit()
        await self.session.refresh(user)
        return user


async def get_user_db(
    session: AsyncSession = Depends(get_session),
) -> AsyncGenerator[RegistrationUserDatabase]:
    yield RegistrationUserDatabase(session, User)


class UserManager(UUIDIDMixin, BaseUserManager[User, uuid.UUID]):
    reset_password_token_secret = settings.jwt_secret
    verification_token_secret = settings.jwt_secret

    async def update(self, user_update, user, safe=False, request=None):
        """Nobody may take over `ADMIN_EMAIL` by renaming themselves to it.

        Registering with that address is what makes somebody the first admin; if an
        existing user could change their address to it, they could sit on the
        reservation. Only the person who already has it may keep it.
        """
        new_email = user_update.email
        if (
            new_email is not None
            and settings.is_admin_email(new_email)
            and not settings.is_admin_email(user.email)
        ):
            raise HTTPException(status.HTTP_400_BAD_REQUEST, detail={"code": "email_reserved"})
        return await super().update(user_update, user, safe=safe, request=request)


async def get_user_manager(
    user_db: RegistrationUserDatabase = Depends(get_user_db),
) -> AsyncGenerator[UserManager]:
    yield UserManager(user_db)


def get_jwt_strategy() -> JWTStrategy:
    return JWTStrategy(secret=settings.jwt_secret, lifetime_seconds=settings.jwt_lifetime_seconds)


auth_backend = AuthenticationBackend(
    name="jwt",
    transport=BearerTransport(tokenUrl="api/v1/auth/jwt/login"),
    get_strategy=get_jwt_strategy,
)

fastapi_users = FastAPIUsers[User, uuid.UUID](get_user_manager, [auth_backend])

current_active_user = fastapi_users.current_user(active=True)


async def require_admin(user: User = Depends(current_active_user)) -> User:
    """The instance level. An admin runs the instance; they own no one's data.

    Nothing behind this dependency may hand out plans, books or accounts of other
    people — that only ever happens through household grants (#91).
    """
    if not user.is_superuser:
        raise HTTPException(status.HTTP_403_FORBIDDEN, detail={"code": "admin_required"})
    return user
