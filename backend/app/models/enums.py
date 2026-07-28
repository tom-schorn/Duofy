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
    """Der 50/30/20-Block. Richtwert, keine Regel."""

    INCOME = "income"
    NEEDS = "needs"
    WANTS = "wants"
    INVESTMENT = "investment"
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


#: Standard-Zuordnung Kategorie → Block nach der 50/30/20-Regel.
#: Wird beim Anlegen einer Position vorbelegt und dort **gespeichert** —
#: eine spätere Änderung hier verändert keine bestehenden Pläne.
CATEGORY_BLOCK: dict[Category, Block] = {
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
    Category.INVESTMENT: Block.INVESTMENT,
}


def resolve_block(category: Category, commitment_type: "CommitmentType | None" = None) -> Block:
    """Welcher 50/30/20-Block gilt für diesen Posten?

    Der Typ der Verpflichtung schlägt die Kategorie — sonst wäre
    „Urlaub sparen" ein Wunsch statt Sparen:

        Urlaub sparen   savings_goal + vacation  →  SAVINGS
        Urlaub buchen   kein commitment          →  WANTS

    Ergebnis wird auf der Position **gespeichert**, nicht bei jedem Lesen
    neu berechnet.
    """
    if commitment_type in (CommitmentType.SAVINGS_GOAL, CommitmentType.DEBT):
        return Block.SAVINGS
    return CATEGORY_BLOCK[category]


class PaymentMethod(StrEnum):
    WITHDRAWAL = "withdrawal"
    TRANSFER = "transfer"
    STANDING_ORDER = "standing_order"
    DIRECT_DEBIT = "direct_debit"
    SPECIAL = "special"
