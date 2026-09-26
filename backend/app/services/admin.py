"""The first admin."""

import logging

from sqlalchemy import func, update
from sqlalchemy.exc import SQLAlchemyError
from sqlalchemy.ext.asyncio import async_sessionmaker

from app.core.config import settings
from app.db.session import async_session
from app.models.user import User

logger = logging.getLogger(__name__)


async def promote_admin(session_factory: async_sessionmaker = async_session) -> None:
    """At start: whoever has `ADMIN_EMAIL` is admin.

    Covers an address that registered before the setting existed. A person who
    registers later is promoted by `RegistrationUserDatabase.create`. A failure is
    logged and does not stop the start: the app is still usable without it, and the
    log says why nobody is admin.
    """
    if not settings.admin_email:
        return
    try:
        async with session_factory() as session:
            result = await session.execute(
                update(User)
                .where(func.lower(User.email) == settings.admin_email.strip().lower())
                .where(User.is_superuser.is_(False))
                .values(is_superuser=True)
            )
            await session.commit()
    except SQLAlchemyError:
        logger.exception("Could not promote the ADMIN_EMAIL user at start")
        return
    if result.rowcount:
        logger.info("Promoted the ADMIN_EMAIL user to admin")
