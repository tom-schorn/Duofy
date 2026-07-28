from enum import StrEnum


class Role(StrEnum):
    OWNER = "owner"
    MEMBER = "member"


class CommitmentType(StrEnum):
    CONTRACT = "contract"
    SAVINGS_GOAL = "savings_goal"
    DEBT = "debt"


class Rhythm(StrEnum):
    MONTHLY = "monthly"
    QUARTERLY = "quarterly"
    BIANNUAL = "biannual"
    ANNUAL = "annual"

    @property
    def interval(self) -> int:
        """Abstand in Monaten — für die Plan-Generierung."""
        return {"monthly": 1, "quarterly": 3, "biannual": 6, "annual": 12}[self.value]


class PlanStatus(StrEnum):
    DRAFT = "draft"
    CONFIRMED = "confirmed"


class Block(StrEnum):
    """Der 50/30/20-Block. Richtwert, keine Regel.

    Investitionen sind **kein** eigener Block — sie fließen in `WANTS`.
    Die getrennte Darstellung im Investitionsplan läuft über
    `Category.INVESTMENT`, nicht über eine vierte Quote.
    """

    INCOME = "income"
    NEEDS = "needs"
    WANTS = "wants"
    SAVINGS = "savings"


class Category(StrEnum):
    """Wofür das Geld sachlich ist.

    Systemweit vorgegeben, nicht pro Haushalt erweiterbar — sonst sind
    Auswertungen über Haushalte hinweg wertlos.
    """

    INCOME = "income"
    HOUSING = "housing"
    INSURANCE = "insurance"
    GROCERIES = "groceries"
    HEALTH = "health"
    MOBILITY = "mobility"
    COMMUNICATION = "communication"
    CHILDREN = "children"
    SUBSCRIPTIONS = "subscriptions"
    LEISURE = "leisure"
    VACATION = "vacation"
    POCKET_MONEY = "pocket_money"
    RESERVES = "reserves"
    DEBT_REPAYMENT = "debt_repayment"
    INVESTMENT = "investment"
    LEGAL = "legal"


#: **Nur ein Vorschlag fürs Frontend.** Stellt das Auswahlfeld auf den
#: naheliegenden Block, mehr nicht — der Nutzer entscheidet.
#:
#: Bewusst keine Datenlogik: ob Sprit Bedarf oder Wunsch ist, hängt vom
#: Haushalt ab und lässt sich nicht allgemeingültig festlegen.
BLOCK_SUGGESTION: dict[Category, Block] = {
    Category.INCOME: Block.INCOME,
    Category.HOUSING: Block.NEEDS,
    Category.INSURANCE: Block.NEEDS,
    Category.GROCERIES: Block.NEEDS,
    Category.HEALTH: Block.NEEDS,
    Category.MOBILITY: Block.NEEDS,
    Category.COMMUNICATION: Block.NEEDS,
    Category.CHILDREN: Block.NEEDS,
    Category.SUBSCRIPTIONS: Block.WANTS,
    Category.LEISURE: Block.WANTS,
    Category.VACATION: Block.WANTS,
    Category.POCKET_MONEY: Block.WANTS,
    Category.RESERVES: Block.SAVINGS,
    Category.DEBT_REPAYMENT: Block.SAVINGS,
    # Investitionen sind bewusste Anschaffungen — eine eigene Liste,
    # aber rechnerisch Teil der Wünsche.
    Category.INVESTMENT: Block.WANTS,
    Category.LEGAL: Block.NEEDS,
}


def resolve_block(chosen: Block, commitment_type: "CommitmentType | None" = None) -> Block:
    """Welcher 50/30/20-Block gilt für diesen Posten?

    Zwei Typen sind rechnerisch festgelegt und überstimmen die Wahl:

        debt          Tilgung ist gebundenes Geld  →  SAVINGS
        savings_goal  Sparen ist Sparen            →  SAVINGS

    Alles andere wählt der Nutzer selbst. Ob Sprit Bedarf oder Wunsch ist,
    hängt vom Haushalt ab — Tom fährt zur Arbeit, jemand anderes fährt zum
    Vergnügen.

    Ergebnis wird auf der Position **gespeichert**, nicht bei jedem Lesen
    neu berechnet.
    """
    if commitment_type in (CommitmentType.SAVINGS_GOAL, CommitmentType.DEBT):
        return Block.SAVINGS
    return chosen


class PaymentMethod(StrEnum):
    WITHDRAWAL = "withdrawal"
    TRANSFER = "transfer"
    STANDING_ORDER = "standing_order"
    DIRECT_DEBIT = "direct_debit"
    SPECIAL = "special"
