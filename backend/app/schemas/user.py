import uuid

from fastapi_users import schemas
from pydantic import ConfigDict
from pydantic.alias_generators import to_camel

#: The same rule as in `app.schemas.base.Schema` — camelCase on the wire. The
#: fastapi-users schemas do not inherit from our base, hence the repetition.
#: Without it, `/users/me` would speak snake_case and the rest of the API
#: camelCase.
_CAMEL = ConfigDict(alias_generator=to_camel, populate_by_name=True)


class UserRead(schemas.BaseUser[uuid.UUID]):
    model_config = _CAMEL

    first_name: str
    last_name: str


class UserCreate(schemas.BaseUserCreate):
    model_config = _CAMEL

    first_name: str
    last_name: str


class UserUpdate(schemas.BaseUserUpdate):
    model_config = _CAMEL

    first_name: str | None = None
    last_name: str | None = None
