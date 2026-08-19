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
        edit   plus the right to change, add and tick off
        delete plus the right to remove things for good

    `delete` is a step of its own rather than part of `edit` because the two
    differ in what they cost when they go wrong. A wrong change is visible in
    `plan_position_changes` and can be changed back; a deletion is neither
    recorded nor reversible. Somebody helping to fill things in needs `edit`,
    almost never `delete`.

    **Always compare with `rank`, never with `is`.** A check written as
    `level is AccessLevel.EDIT` stops being true the moment a higher level
    exists, and takes the right to edit away from the very people who were
    trusted most.
    """

    PLAN = "plan"
    VIEW = "view"
    EDIT = "edit"
    DELETE = "delete"

    @property
    def rank(self) -> int:
        """For comparisons — `level.rank >= AccessLevel.VIEW.rank`."""
        return {"plan": 0, "view": 1, "edit": 2, "delete": 3}[self.value]


class CommitmentType(StrEnum):
    CONTRACT = "contract"
    SAVINGS_GOAL = "savings_goal"
    DEBT = "debt"
    BUDGET = "budget"
    #: Money coming in — salary, child benefit, interest. Nobody signs a contract
    #: to receive their own wage, so it is not one.
    INCOME = "income"


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

    Every member carries its own metadata instead of a second lookup table beside
    the enum — a category without a block cannot be written down:

        value   what goes into the database, `group.name` where there is a group
        group   the heading it appears under, `None` if it stands on its own
        label   the English name, shown as is
        block   the **suggested** 50/30/20 block, see `BLOCK_SUGGESTION`

    The dot in the value carries the hierarchy, so grouping needs no second
    column: `LIKE 'housing.%'` in SQL, `.group` in Python.
    """

    group: str | None
    label: str
    block: Block

    def __new__(cls, value: str, group: str | None, label: str, block: Block) -> "Category":
        member = str.__new__(cls, value)
        member._value_ = value
        member.group = group
        member.label = label
        member.block = block
        return member

    # -- Household ---------------------------------------------------------
    HOUSEHOLD_GROCERIES = ("household.groceries", "Household", "Groceries", Block.NEEDS)
    HOUSEHOLD_CLOTHING = ("household.clothing", "Household", "Clothing", Block.NEEDS)
    HOUSEHOLD_HEALTHCARE = ("household.healthcare", "Household", "Healthcare", Block.NEEDS)
    HOUSEHOLD_PERSONAL_CARE = ("household.personal_care", "Household", "Personal Care", Block.NEEDS)
    HOUSEHOLD_CLEANING = ("household.cleaning", "Household", "Cleaning", Block.NEEDS)
    #: Food, vet, insurance for an animal. A need once the animal is there — the
    #: decision was a want, the upkeep no longer is. Kept apart from
    #: `household.healthcare`, which is about people.
    HOUSEHOLD_PETS = ("household.pets", "Household", "Pets", Block.NEEDS)

    # -- Housing -----------------------------------------------------------
    HOUSING_RENT = ("housing.rent", "Housing", "Rent", Block.NEEDS)
    HOUSING_UTILITIES = ("housing.utilities", "Housing", "Utilities", Block.NEEDS)
    HOUSING_REPAIRS = ("housing.repairs", "Housing", "Renovation & Repairs", Block.NEEDS)
    #: Furniture, lamps, curtains. A deliberate purchase, not a fixed cost —
    #: unlike a repair it can wait, which is what puts it in the wants.
    HOUSING_INTERIOR = ("housing.interior", "Housing", "Interior Furnishings", Block.WANTS)
    HOUSING_OUTDOOR = ("housing.outdoor", "Housing", "Outdoor Furnishings", Block.WANTS)
    HOUSING_INSURANCE = ("housing.insurance", "Housing", "Insurance & Taxes", Block.NEEDS)

    # -- Transportation ----------------------------------------------------
    TRANSPORT_PUBLIC = ("transport.public", "Transportation", "Public Transport", Block.NEEDS)
    TRANSPORT_FUEL = ("transport.fuel", "Transportation", "Fuel", Block.NEEDS)
    TRANSPORT_REPAIRS = ("transport.repairs", "Transportation", "Repairs", Block.NEEDS)
    #: Parking tickets, speeding fines, registration charges. A need in the sense
    #: that it has to be paid — nobody plans it, but it is not a want either.
    TRANSPORT_FINES = ("transport.fines", "Transportation", "Fines & Fees", Block.NEEDS)
    TRANSPORT_PURCHASE = ("transport.purchase", "Transportation", "Vehicle Purchase", Block.WANTS)
    TRANSPORT_INSURANCE = (
        "transport.insurance",
        "Transportation",
        "Insurance & Taxes",
        Block.NEEDS,
    )

    # -- Children ----------------------------------------------------------
    CHILDREN_CARE = ("children.care", "Children", "Childcare", Block.NEEDS)
    CHILDREN_SCHOOL = ("children.school", "Children", "School Supplies", Block.NEEDS)
    CHILDREN_ALLOWANCE = ("children.allowance", "Children", "Allowance", Block.WANTS)

    # -- Leisure -----------------------------------------------------------
    LEISURE_VACATION = ("leisure.vacation", "Leisure", "Vacation", Block.WANTS)
    LEISURE_HOBBIES = ("leisure.hobbies", "Leisure", "Hobbies", Block.WANTS)
    LEISURE_ENTERTAINMENT = (
        "leisure.entertainment",
        "Leisure",
        "Entertainment & Games",
        Block.WANTS,
    )
    LEISURE_MEMBERSHIPS = ("leisure.memberships", "Leisure", "Memberships", Block.WANTS)
    LEISURE_DINING = ("leisure.dining", "Leisure", "Dining Out", Block.WANTS)
    #: Streaming, games, newspapers. Told apart from `leisure.memberships` by what
    #: is being paid for: a service here, belonging to something there.
    LEISURE_SUBSCRIPTIONS = ("leisure.subscriptions", "Leisure", "Subscriptions", Block.WANTS)
    #: Tobacco and alcohol. Its own entry rather than part of the groceries, and
    #: the reason is the **block**: groceries are a need, and booking indulgences
    #: there moves them into the 50 % — which makes the quota look better than
    #: the household is. The official classification (COICOP) keeps them as a
    #: division of their own next to food for the same reason.
    LEISURE_INDULGENCES = ("leisure.indulgences", "Leisure", "Tobacco & Alcohol", Block.WANTS)

    # -- Personal ----------------------------------------------------------
    # The counterpart to Housing and Transportation: what hangs on the **person**
    # rather than on a flat or a car. Whoever moves out or sells the car keeps all
    # of this.
    #: Health, liability, life. The two grouped insurance entries cover what belongs
    #: to a flat or a vehicle — this is the rest.
    PERSONAL_INSURANCE = ("personal.insurance", "Personal", "Insurance", Block.NEEDS)
    PERSONAL_COMMUNICATION = ("personal.communication", "Personal", "Communication", Block.NEEDS)
    #: Work-related expenses paid from private money: work clothes, office
    #: supplies, professional literature, union fees. Explicitly **not** business
    #: expenses — business income does not belong in Duofy at all, it would skew
    #: the 50/30/20 quotas.
    PERSONAL_WORK = ("personal.work", "Personal", "Work", Block.NEEDS)
    PERSONAL_LEGAL = ("personal.legal", "Personal", "Legal", Block.NEEDS)
    #: Presents for other people, and what a celebration costs besides them —
    #: decorations, candles, paper plates. Both together on purpose: a single
    #: birthday shop holds the present and the trimmings, and splitting that
    #: receipt asks a question with no good answer.
    #:
    #: A want, unlike the rest of this group — the group says what something
    #: hangs on, not which block it lands in, the same way `housing.interior`
    #: sits in the wants among fixed housing costs.
    PERSONAL_GIFTS = ("personal.gifts", "Personal", "Gifts & Celebrations", Block.WANTS)
    #: Given away freely: association, church, relief organisation. Not
    #: `leisure.memberships` — that one buys belonging, this one buys nothing.
    PERSONAL_DONATIONS = ("personal.donations", "Personal", "Donations", Block.WANTS)
    #: Courses, tuition, tutoring, exam fees. Separate from `personal.work`,
    #: which covers what an existing job costs — this is what the next one does.
    PERSONAL_EDUCATION = ("personal.education", "Personal", "Education", Block.NEEDS)
    #: Income tax, church tax, back payments. The two grouped "Insurance & Taxes"
    #: entries cover what belongs to a flat or a vehicle; this is what belongs to
    #: the person and arrives as its own demand.
    PERSONAL_TAXES = ("personal.taxes", "Personal", "Taxes", Block.NEEDS)

    # -- Income ------------------------------------------------------------
    INCOME_EARNED = ("income.earned", "Income", "Salary & Wages", Block.INCOME)
    #: Child benefit, care allowance, housing benefit. Kept apart from earned income
    #: because a benefit belongs to the **household**, not to the person whose
    #: account it happens to land on — that is what `pass_through` marks.
    INCOME_BENEFITS = ("income.benefits", "Income", "Benefits", Block.INCOME)
    #: Interest and capital gains. Separate because it does not come from work, and
    #: because it is the thing one does **not** plan: it simply accrues.
    INCOME_INTEREST = ("income.interest", "Income", "Interest", Block.INCOME)
    #: Deposit refunds, second-hand sales, gifts. The one-offs that fit nowhere else.
    INCOME_OTHER = ("income.other", "Income", "Other Income", Block.INCOME)

    # -- Finance -----------------------------------------------------------
    # Not consumption. The money is not gone — it sits somewhere else, or a debt got
    # smaller. A different kind of event from buying groceries, which is why it gets
    # a heading of its own instead of hiding among the expenses.
    FINANCE_SAVINGS = ("finance.savings", "Finance", "Reserves", Block.SAVINGS)
    FINANCE_DEBT = ("finance.debt", "Finance", "Debt Repayment", Block.SAVINGS)
    #: Investments are deliberate purchases — a list of their own, but part of the
    #: wants when it comes to the arithmetic.
    FINANCE_INVESTMENT = ("finance.investment", "Finance", "Investment", Block.WANTS)
    #: Account fees, transfer charges, card fees. Recurring every month and a fit
    #: for none of the others — it is not a legal matter.
    FINANCE_FEES = ("finance.fees", "Finance", "Fees", Block.NEEDS)
    #: Money going to another household member because they paid for something.
    #: Not an expense in economic terms: the purchase was already booked on their
    #: side. Household-wide evaluations have to exclude it, otherwise the same
    #: purchase counts twice. See issue #4.
    FINANCE_SETTLEMENT = ("finance.settlement", "Finance", "Settlement", Block.NEEDS)


#: Longest value plus room to grow — `enum_column(Category, length=CATEGORY_LENGTH)`.
#: The default of 20 is not enough once the group sits in front of the dot.
CATEGORY_LENGTH = 40


#: **A suggestion for the frontend only.** It preselects the obvious block in the
#: form, nothing more — the user decides.
#:
#: Deliberately not data logic: whether fuel is a need or a want depends on the
#: household and cannot be settled in general. Derived from the members, so it can
#: never fall out of step with them.
BLOCK_SUGGESTION: dict[Category, Block] = {category: category.block for category in Category}


def resolve_block(chosen: Block, commitment_type: "CommitmentType | None" = None) -> Block:
    """Which 50/30/20 block applies to this position?

    Three types are settled by arithmetic and override the choice:

        debt          repayment is committed money  →  SAVINGS
        savings_goal  saving is saving              →  SAVINGS
        income        money coming in is no block   →  INCOME

    Everything else is the user's call. Whether fuel is a need or a want depends
    on the household — one person drives to work, another drives for fun.

    The result is **stored** on the position rather than computed on every read,
    so changing this function never rewrites plans that already exist.
    """
    if commitment_type in (CommitmentType.SAVINGS_GOAL, CommitmentType.DEBT):
        return Block.SAVINGS
    elif commitment_type is CommitmentType.INCOME:
        return Block.INCOME
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
