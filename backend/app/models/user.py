from fastapi_users.db import SQLAlchemyBaseUserTableUUID
from sqlalchemy import String
from sqlalchemy.orm import Mapped, mapped_column

from app.db.base import Base


class User(SQLAlchemyBaseUserTableUUID, Base):
    """A user with their own account.

    SQLAlchemyBaseUserTableUUID already provides:
        id               UUID, primary key
        email            unique, indexed
        hashed_password  the hash only — never the plaintext password
        is_active        whether the account is blocked
        is_superuser
        is_verified      email confirmed
    """

    # "user" is a reserved word in Postgres — hence "users".
    __tablename__ = "users"

    first_name: Mapped[str] = mapped_column(String(100))
    last_name: Mapped[str] = mapped_column(String(100))
