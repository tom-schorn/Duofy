import uuid
from decimal import Decimal

from fastapi_users import schemas
from pydantic import ConfigDict, Field, model_validator
from pydantic.alias_generators import to_camel

from app.schemas.quota import check_quotas

#: The same rule as in `app.schemas.base.Schema` — camelCase on the wire. The
#: fastapi-users schemas do not inherit from our base, hence the repetition.
#: Without it, `/users/me` would speak snake_case and the rest of the API
#: camelCase.
_CAMEL = ConfigDict(alias_generator=to_camel, populate_by_name=True)


class UserRead(schemas.BaseUser[uuid.UUID]):
    model_config = _CAMEL

    first_name: str
    last_name: str
    target_needs: Decimal
    target_wants: Decimal
    target_savings: Decimal
    buffer_percent: Decimal


class UserCreate(schemas.BaseUserCreate):
    model_config = _CAMEL

    first_name: str
    last_name: str


class UserUpdate(schemas.BaseUserUpdate):
    model_config = _CAMEL

    first_name: str | None = None
    last_name: str | None = None
    target_needs: Decimal | None = Field(default=None, ge=0, le=100)
    target_wants: Decimal | None = Field(default=None, ge=0, le=100)
    target_savings: Decimal | None = Field(default=None, ge=0, le=100)
    buffer_percent: Decimal | None = Field(default=None, ge=0, le=100)

    @model_validator(mode="after")
    def _quotas_add_up(self) -> "UserUpdate":
        check_quotas(self.target_needs, self.target_wants, self.target_savings)
        return self
