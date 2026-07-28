from fastapi_users.db import SQLAlchemyBaseUserTableUUID
from sqlalchemy import String
from sqlalchemy.orm import Mapped, mapped_column

from app.db.base import Base


class User(SQLAlchemyBaseUserTableUUID, Base):
    """Ein Nutzer mit eigenem Account.

    Von SQLAlchemyBaseUserTableUUID kommen bereits:
        id               UUID, Primärschlüssel
        email            eindeutig, indiziert
        hashed_password  nur der Hash — nie das Klartext-Passwort
        is_active        Konto gesperrt oder nicht
        is_superuser
        is_verified      E-Mail bestätigt
    """

    # "user" ist in Postgres ein reserviertes Wort — deshalb "users".
    __tablename__ = "users"

    first_name: Mapped[str] = mapped_column(String(100))
    last_name: Mapped[str] = mapped_column(String(100))
