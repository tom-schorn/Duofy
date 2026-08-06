from enum import StrEnum

from sqlalchemy import Enum as SAEnum


def enum_column(enum_class: type[StrEnum], length: int = 20) -> SAEnum:
    """An enum column that stores the **value**, not the member name.

    Without this, SQLAlchemy writes `MONTHLY` although the enum carries `monthly`
    as its value. That backfires in three places:

    * **CHECK constraints** compare against the value (`rhythm = \'monthly\'`) and
      either fail or — worse — silently never match.
    * **Raw SQL** and any reporting would have to know about the difference.
    * The **API** sends and receives values. Two spellings for the same thing in
      one system is a source of errors with nothing to show for it.

    Use this helper everywhere instead of `SAEnum(...)` directly.
    """
    return SAEnum(
        enum_class,
        native_enum=False,
        length=length,
        values_callable=lambda enum: [member.value for member in enum],
    )
