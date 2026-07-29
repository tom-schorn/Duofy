import uuid

from fastapi_users import schemas
from pydantic import ConfigDict
from pydantic.alias_generators import to_camel

#: Dieselbe Regel wie in `app.schemas.base.Schema` — nach außen camelCase.
#: Die fastapi-users-Schemas erben nicht von unserer Basis, deshalb hier
#: nochmal. Ohne das spräche `/users/me` snake_case und der Rest der API
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
