from sqlalchemy.orm import DeclarativeBase


class Base(DeclarativeBase):
    """Base class for every model.

    Put new models in app/models/ and import them in app/models/__init__.py —
    Alembic does not see them otherwise.
    """
