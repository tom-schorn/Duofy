import uuid

from sqlalchemy import CheckConstraint, ForeignKey
from sqlalchemy.orm import Mapped, mapped_column

from app.db.base import Base
from app.db.types import enum_column
from app.models.enums import AccessLevel, Area
from app.models.mixins import TimestampMixin


class Grant(TimestampMixin, Base):
    """One person lets one other person act on their data in one area.

    "A grants B in the book: create." Each person sets only the rows where they
    are the granter (decision 57); the endpoint takes the granter from the login,
    never from the request.

    There is no household column. A person belongs to exactly one household, and a
    grant counts only while granter and grantee share it; leaving or being removed
    deletes every grant from and to that person. Storing the household as well
    would be a second copy of that fact, free to contradict the first.

    No row means `none`. Setting `none` deletes the row, so "nothing granted" is
    the one default and a new member starts there without anything being written.
    """

    __tablename__ = "grants"
    __table_args__ = (
        CheckConstraint("granter_id <> grantee_id", name="ck_grant_not_self"),
        CheckConstraint(
            "area IN ('plan', 'book', 'accounts', 'commitments', 'import')",
            name="ck_grant_area",
        ),
        CheckConstraint("level IN ('view', 'create', 'edit', 'delete')", name="ck_grant_level"),
    )

    granter_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("users.id", ondelete="CASCADE", name="fk_grants_granter_id_users"),
        primary_key=True,
    )
    grantee_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("users.id", ondelete="CASCADE", name="fk_grants_grantee_id_users"),
        primary_key=True,
        index=True,
    )
    area: Mapped[Area] = mapped_column(enum_column(Area), primary_key=True)
    level: Mapped[AccessLevel] = mapped_column(enum_column(AccessLevel))
