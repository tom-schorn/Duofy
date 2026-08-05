from pydantic import BaseModel, ConfigDict
from pydantic.alias_generators import to_camel


class Schema(BaseModel):
    """Basis für alle Schemas dieser API.

    Nach außen **camelCase**, innen bleibt alles snake_case. Damit muss das
    Frontend keine Übersetzungsschicht bauen — `first_due_date` kommt als
    `firstDueDate` an und passt direkt auf die TypeScript-Typen.

    `populate_by_name` erlaubt beides beim Lesen, damit Tests und interne
    Aufrufe weiter mit snake_case arbeiten können.
    """

    model_config = ConfigDict(
        alias_generator=to_camel,
        populate_by_name=True,
        from_attributes=True,
    )
