from decimal import Decimal

from fastapi_users.db import SQLAlchemyBaseUserTableUUID
from sqlalchemy import CheckConstraint, Numeric, String
from sqlalchemy.orm import Mapped, mapped_column

from app.db.base import Base
from app.db.types import enum_column
from app.models.enums import FlowLimitsBy


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
    __table_args__ = (
        CheckConstraint(
            "target_needs + target_wants + target_savings = 100",
            name="ck_user_targets_sum_100",
        ),
    )

    first_name: Mapped[str] = mapped_column(String(100))
    last_name: Mapped[str] = mapped_column(String(100))

    #: The personal default for every month created from now on. A snapshot: it is
    #: copied into `Plan` when a month is made, so changing it rewrites no old month.
    target_needs: Mapped[Decimal] = mapped_column(
        Numeric(5, 2), default=Decimal("50.00"), server_default="50.00"
    )
    target_wants: Mapped[Decimal] = mapped_column(
        Numeric(5, 2), default=Decimal("30.00"), server_default="30.00"
    )
    target_savings: Mapped[Decimal] = mapped_column(
        Numeric(5, 2), default=Decimal("20.00"), server_default="20.00"
    )
    #: What the flow chart counts for limits, read from the **viewer** — also in the
    #: household plan and in other people's plans. Stored on the server, not in the
    #: browser: whoever tracks groceries does so every month and on every device.
    flow_limits_by: Mapped[FlowLimitsBy] = mapped_column(
        enum_column(FlowLimitsBy),
        default=FlowLimitsBy.PLAN,
        server_default=FlowLimitsBy.PLAN.value,
    )
