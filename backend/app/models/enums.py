from enum import StrEnum


class Role(StrEnum):
    OWNER = "owner"
    MEMBER = "member"


class InvitationStatus(StrEnum):
    """Lebenslauf einer Haushalts-Einladung."""

    PENDING = "pending"
    ACCEPTED = "accepted"
    DECLINED = "declined"
    REVOKED = "revoked"


class AccessLevel(StrEnum):
    """Was ein Mitglied den **anderen** über sich erlaubt.

    Die Stufe steht bewusst an der eigenen Mitgliedschaft, nicht an der des
    anderen: wessen Daten es sind, der entscheidet. Jasmin kann sich nicht
    selbst das Recht geben, Toms Buch zu sehen — Tom gibt es ihr.

    Die Stufen bauen aufeinander auf:

        plan   nur die gemeinsamen Posten, wie sie im Haushaltsplan stehen
        view   dazu das eigene Buch, die Konten und die privaten Posten
        edit   dazu das Recht, diese Posten zu ändern und abzuhaken
    """

    PLAN = "plan"
    VIEW = "view"
    EDIT = "edit"

    @property
    def rank(self) -> int:
        """Für Vergleiche — `level.rank >= AccessLevel.VIEW.rank`."""
        return {"plan": 0, "view": 1, "edit": 2}[self.value]


class CommitmentType(StrEnum):
    CONTRACT = "contract"
    SAVINGS_GOAL = "savings_goal"
    DEBT = "debt"
    #: Wiederkehrender Betrag ohne Vertrag dahinter — Sprit, Lebensmittel,
    #: Taschengeld. Verhält sich wie ein Vertrag, heißt aber nicht so.
    BUDGET = "budget"


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
    #: Werbungskosten — beruflich veranlasst, aus privatem Geld bezahlt:
    #: Arbeitskleidung, Büromaterial, Fachliteratur, Gewerkschaftsbeitrag.
    #: **Keine** Betriebsausgaben — betriebliche Einnahmen gehören nicht in
    #: Duofy, sie würden die 50/30/20-Quoten verzerren.
    WORK = "work"
    #: Kontoführung, Überweisungsentgelte, Kartengebühren. Kommt jeden Monat
    #: und passte in keine der anderen — „Rechtliches" ist es nicht.
    FEES = "fees"
    #: Geld, das an ein Haushaltsmitglied geht, weil es etwas ausgelegt hat.
    #: Keine Ausgabe im wirtschaftlichen Sinn — der Vorgang wurde schon beim
    #: anderen gebucht. Bei Auswertungen über den Haushalt ausklammern,
    #: sonst zählt derselbe Einkauf doppelt. Siehe Issue #4.
    SETTLEMENT = "settlement"
    #: Zinsen und Kapitalerträge. Eigene Kategorie statt „Einnahme", weil sie
    #: nicht aus Arbeit kommen und man sie getrennt sehen will — sie sind
    #: außerdem das, was man **nicht** plant: sie fallen an.
    INTEREST = "interest"


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
    Category.WORK: Block.NEEDS,
    Category.FEES: Block.NEEDS,
    Category.SETTLEMENT: Block.NEEDS,
    Category.INTEREST: Block.INCOME,
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


class AccountType(StrEnum):
    """Nur **Zahlungskonten** — Dinge mit einem Stand, der sich aus Buchungen ergibt.

    Ein Depot gehört ausdrücklich **nicht** dazu: sein Wert ändert sich durch
    Kurse, nicht durch Transaktionen. Ein Saldo aus Anfangsbestand plus
    Buchungen wäre dort dauerhaft falsch. Im Buch steht deshalb nur das
    Verrechnungskonto, Wertpapierkäufe sind Umbuchungen dorthin.
    """

    CHECKING = "checking"
    SAVINGS = "savings"
    #: Guthabenbasis — verhält sich wie ein normales Konto mit Stand. Eine
    #: echte Kreditkarte mit Monatsabrechnung wäre ein anderer Fall.
    CREDIT_CARD = "credit_card"
    #: Verrechnungskonto zum Depot.
    SETTLEMENT = "settlement"
    #: PayPal und Ähnliches.
    PAYMENT_SERVICE = "payment_service"
    CASH = "cash"


class PaymentMethod(StrEnum):
    WITHDRAWAL = "withdrawal"
    TRANSFER = "transfer"
    STANDING_ORDER = "standing_order"
    DIRECT_DEBIT = "direct_debit"
    SPECIAL = "special"
