from pydantic import BaseModel, ConfigDict
from pydantic.alias_generators import to_camel


class Schema(BaseModel):
    """Base class for every schema in this API.

    **camelCase** on the wire, snake_case inside. That way the frontend needs no
    translation layer — `first_due_date` arrives as `firstDueDate` and maps
    directly onto the TypeScript types.

    `populate_by_name` accepts both when reading, so tests and internal calls can
    keep using snake_case.
    """

    model_config = ConfigDict(
        alias_generator=to_camel,
        populate_by_name=True,
        from_attributes=True,
    )
