from sqlalchemy.orm import DeclarativeBase


class Base(DeclarativeBase):
    """Basisklasse für alle Modelle.

    Neue Modelle in app/models/ anlegen und in app/models/__init__.py
    importieren — sonst sieht Alembic sie nicht.
    """
