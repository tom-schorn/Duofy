from enum import StrEnum

from sqlalchemy import Enum as SAEnum


def enum_column(enum_class: type[StrEnum], length: int = 20) -> SAEnum:
    """Enum-Spalte, die den **Wert** speichert — nicht den Namen.

    SQLAlchemy legt sonst `MONTHLY` ab, obwohl das Enum `monthly` als Wert
    trägt. Das fällt an drei Stellen auf die Füße:

    * **CHECK-Constraints** vergleichen mit dem Wert (`rhythm = 'monthly'`)
      und schlagen fehl oder — schlimmer — greifen stillschweigend nie.
    * **Rohes SQL** und Auswertungen müssten den Unterschied kennen.
    * Die **API** sendet und empfängt Werte. Zwei Schreibweisen für dasselbe
      im selben System sind eine Fehlerquelle ohne Gegenwert.

    Deshalb überall diese Hilfe statt `SAEnum(...)` direkt.
    """
    return SAEnum(
        enum_class,
        native_enum=False,
        length=length,
        values_callable=lambda enum: [member.value for member in enum],
    )
