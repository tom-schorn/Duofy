from enum import StrEnum


class Role(StrEnum):
    OWNER = "owner"
    MEMBER = "member"


class InvitationStatus(StrEnum):
    """Life cycle of a household invitation."""

    PENDING = "pending"
    ACCEPTED = "accepted"
    DECLINED = "declined"
    REVOKED = "revoked"


class AccessLevel(StrEnum):
    """What a member allows the **others** to see about themselves.

    The level sits on the member's own membership, not on the other person's:
    whoever owns the data decides. Nobody can grant themselves the right to look
    into somebody else's book.

    The levels build on each other:

        plan   only the shared positions, as they appear in the household plan
        view   plus the own book, the accounts and the private positions
        edit   plus the right to change and tick off those positions
    """

    PLAN = "plan"
    VIEW = "view"
    EDIT = "edit"

    @property
    def rank(self) -> int:
        """For comparisons — `level.rank >= AccessLevel.VIEW.rank`."""
        return {"plan": 0, "view": 1, "edit": 2}[self.value]


class CommitmentType(StrEnum):
    CONTRACT = "contract"
    SAVINGS_GOAL = "savings_goal"
    DEBT = "debt"
    #: A recurring amount with no contract behind it — fuel, groceries, pocket
    #: money. Behaves like a contract but is not one, and nobody sends a bill.
    BUDGET = "budget"


class Rhythm(StrEnum):
    MONTHLY = "monthly"
    QUARTERLY = "quarterly"
    BIANNUAL = "biannual"
    ANNUAL = "annual"

    @property
    def interval(self) -> int:
        """Distance in months — used when generating a plan."""
        return {"monthly": 1, "quarterly": 3, "biannual": 6, "annual": 12}[self.value]


class Block(StrEnum):
    """One of the three 50/30/20 blocks. A guideline, not a rule.

    Investments are **not** a block of their own — they count towards `WANTS`.
    Showing them separately runs through `Category.INVESTMENT`, not through a
    fourth quota.
    """

    INCOME = "income"
    NEEDS = "needs"
    WANTS = "wants"
    SAVINGS = "savings"


class Category(StrEnum):
    """What the money is for, factually.

    Defined system-wide and deliberately not extensible per household: comparing
    across households would be worthless if everyone invented their own.
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
    #: Work-related expenses paid from private money: work clothes, office
    #: supplies, professional literature, union fees. Explicitly **not** business
    #: expenses — business income does not belong in Duofy at all, it would skew
    #: the 50/30/20 quotas.
    WORK = "work"
    #: Account fees, transfer charges, card fees. Recurring every month and a fit
    #: for none of the others — it is not a legal matter.
    FEES = "fees"
    #: Money going to another household member because they paid for something.
    #: Not an expense in economic terms: the purchase was already booked on their
    #: side. Household-wide evaluations have to exclude it, otherwise the same
    #: purchase counts twice. See issue #4.
    SETTLEMENT = "settlement"
    #: Interest and capital gains. A category of its own rather than plain income,
    #: because it does not come from work and one wants to see it separately — and
    #: because it is the thing one does **not** plan: it simply accrues.
    INTEREST = "interest"


#: **A suggestion for the frontend only.** It preselects the obvious block in the
#: form, nothing more — the user decides.
#:
#: Deliberately not data logic: whether fuel is a need or a want depends on the
#: household and cannot be settled in general.
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
    # Investments are deliberate purchases — a list of their own, but part of the
    # wants when it comes to the arithmetic.
    Category.INVESTMENT: Block.WANTS,
    Category.LEGAL: Block.NEEDS,
    Category.WORK: Block.NEEDS,
    Category.FEES: Block.NEEDS,
    Category.SETTLEMENT: Block.NEEDS,
    Category.INTEREST: Block.INCOME,
}


def resolve_block(chosen: Block, commitment_type: "CommitmentType | None" = None) -> Block:
    """Which 50/30/20 block applies to this position?

    Two types are settled by arithmetic and override the choice:

        debt          repayment is committed money  →  SAVINGS
        savings_goal  saving is saving              →  SAVINGS

    Everything else is the user's call. Whether fuel is a need or a want depends
    on the household — one person drives to work, another drives for fun.

    The result is **stored** on the position rather than computed on every read,
    so changing this function never rewrites plans that already exist.
    """
    if commitment_type in (CommitmentType.SAVINGS_GOAL, CommitmentType.DEBT):
        return Block.SAVINGS
    return chosen


class AccountType(StrEnum):
    """**Payment accounts only** — things with a balance that follows from bookings.

    A securities account explicitly does **not** belong here: its value changes
    with market prices, not with transactions, so a balance from opening amount
    plus bookings would be permanently wrong. Only the settlement account appears
    in the book; buying securities is a transfer to it.
    """

    CHECKING = "checking"
    SAVINGS = "savings"
    #: Prepaid style — behaves like a normal account with a balance. A real credit
    #: card with a monthly statement would be a different case.
    CREDIT_CARD = "credit_card"
    #: Settlement account belonging to a securities account.
    SETTLEMENT = "settlement"
    #: Online payment services.
    PAYMENT_SERVICE = "payment_service"
    CASH = "cash"


class PaymentMethod(StrEnum):
    WITHDRAWAL = "withdrawal"
    TRANSFER = "transfer"
    STANDING_ORDER = "standing_order"
    DIRECT_DEBIT = "direct_debit"
    SPECIAL = "special"
