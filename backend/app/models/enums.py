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


class Budget(StrEnum):
    """One of the three 50/30/20 budgets. A guideline, not a rule.

    Investments are **not** a budget of their own — they count towards `WANTS`.
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
    the enum — a category without a budget cannot be written down:

        value   what goes into the database, `group.name` where there is a group
        group   the heading it appears under, `None` if it stands on its own
        label   the English name, shown as is
        budget  the **suggested** 50/30/20 budget, see `BUDGET_SUGGESTION`

    The dot in the value carries the hierarchy, so grouping needs no second
    column: `LIKE 'housing.%'` in SQL, `.group` in Python.
    """

    group: str | None
    label: str
    budget: Budget

    def __new__(cls, value: str, group: str | None, label: str, budget: Budget) -> "Category":
        member = str.__new__(cls, value)
        member._value_ = value
        member.group = group
        member.label = label
        member.budget = budget
        return member

    # -- Household ---------------------------------------------------------
    HOUSEHOLD_GROCERIES = ("household.groceries", "Household", "Groceries", Budget.NEEDS)
    HOUSEHOLD_CLOTHING = ("household.clothing", "Household", "Clothing", Budget.NEEDS)
    HOUSEHOLD_HEALTHCARE = ("household.healthcare", "Household", "Healthcare", Budget.NEEDS)
    HOUSEHOLD_PERSONAL_CARE = (
        "household.personal_care",
        "Household",
        "Personal Care",
        Budget.NEEDS,
    )
    HOUSEHOLD_CLEANING = ("household.cleaning", "Household", "Cleaning", Budget.NEEDS)
    #: Food, vet, insurance for an animal. A need once the animal is there — the
    #: decision was a want, the upkeep no longer is. Kept apart from
    #: `household.healthcare`, which is about people.
    HOUSEHOLD_PETS = ("household.pets", "Household", "Pets", Budget.NEEDS)

    # -- Housing -----------------------------------------------------------
    HOUSING_RENT = ("housing.rent", "Housing", "Rent", Budget.NEEDS)
    HOUSING_UTILITIES = ("housing.utilities", "Housing", "Utilities", Budget.NEEDS)
    HOUSING_REPAIRS = ("housing.repairs", "Housing", "Renovation & Repairs", Budget.NEEDS)
    #: Furniture, lamps, curtains. A deliberate purchase, not a fixed cost —
    #: unlike a repair it can wait, which is what puts it in the wants.
    HOUSING_INTERIOR = ("housing.interior", "Housing", "Interior Furnishings", Budget.WANTS)
    HOUSING_OUTDOOR = ("housing.outdoor", "Housing", "Outdoor Furnishings", Budget.WANTS)
    HOUSING_INSURANCE = ("housing.insurance", "Housing", "Insurance & Taxes", Budget.NEEDS)

    # -- Transportation ----------------------------------------------------
    TRANSPORT_PUBLIC = ("transport.public", "Transportation", "Public Transport", Budget.NEEDS)
    TRANSPORT_FUEL = ("transport.fuel", "Transportation", "Fuel", Budget.NEEDS)
    TRANSPORT_REPAIRS = ("transport.repairs", "Transportation", "Repairs", Budget.NEEDS)
    #: Parking tickets, speeding fines, registration charges. A need in the sense
    #: that it has to be paid — nobody plans it, but it is not a want either.
    TRANSPORT_FINES = ("transport.fines", "Transportation", "Fines & Fees", Budget.NEEDS)
    TRANSPORT_PURCHASE = ("transport.purchase", "Transportation", "Vehicle Purchase", Budget.WANTS)
    TRANSPORT_INSURANCE = (
        "transport.insurance",
        "Transportation",
        "Insurance & Taxes",
        Budget.NEEDS,
    )

    # -- Children ----------------------------------------------------------
    CHILDREN_CARE = ("children.care", "Children", "Childcare", Budget.NEEDS)
    CHILDREN_SCHOOL = ("children.school", "Children", "School Supplies", Budget.NEEDS)
    CHILDREN_ALLOWANCE = ("children.allowance", "Children", "Allowance", Budget.WANTS)

    # -- Leisure -----------------------------------------------------------
    LEISURE_VACATION = ("leisure.vacation", "Leisure", "Vacation", Budget.WANTS)
    LEISURE_HOBBIES = ("leisure.hobbies", "Leisure", "Hobbies", Budget.WANTS)
    LEISURE_ENTERTAINMENT = (
        "leisure.entertainment",
        "Leisure",
        "Entertainment & Games",
        Budget.WANTS,
    )
    LEISURE_MEMBERSHIPS = ("leisure.memberships", "Leisure", "Memberships", Budget.WANTS)
    LEISURE_DINING = ("leisure.dining", "Leisure", "Dining Out", Budget.WANTS)
    #: Streaming, games, newspapers. Told apart from `leisure.memberships` by what
    #: is being paid for: a service here, belonging to something there.
    LEISURE_SUBSCRIPTIONS = ("leisure.subscriptions", "Leisure", "Subscriptions", Budget.WANTS)
    #: Tobacco and alcohol. Its own entry rather than part of the groceries, and
    #: the reason is the **budget**: groceries are a need, and booking indulgences
    #: there moves them into the 50 % — which makes the quota look better than
    #: the household is. The official classification (COICOP) keeps them as a
    #: division of their own next to food for the same reason.
    LEISURE_INDULGENCES = ("leisure.indulgences", "Leisure", "Tobacco & Alcohol", Budget.WANTS)

    # -- Personal ----------------------------------------------------------
    # The counterpart to Housing and Transportation: what hangs on the **person**
    # rather than on a flat or a car. Whoever moves out or sells the car keeps all
    # of this.
    #: Health, liability, life. The two grouped insurance entries cover what belongs
    #: to a flat or a vehicle — this is the rest.
    PERSONAL_INSURANCE = ("personal.insurance", "Personal", "Insurance", Budget.NEEDS)
    PERSONAL_COMMUNICATION = ("personal.communication", "Personal", "Communication", Budget.NEEDS)
    #: Work-related expenses paid from private money: work clothes, office
    #: supplies, professional literature, union fees. Explicitly **not** business
    #: expenses — business income does not belong in Duofy at all, it would skew
    #: the 50/30/20 quotas.
    PERSONAL_WORK = ("personal.work", "Personal", "Work", Budget.NEEDS)
    PERSONAL_LEGAL = ("personal.legal", "Personal", "Legal", Budget.NEEDS)
    #: Presents for other people, and what a celebration costs besides them —
    #: decorations, candles, paper plates. Both together on purpose: a single
    #: birthday shop holds the present and the trimmings, and splitting that
    #: receipt asks a question with no good answer.
    #:
    #: A want, unlike the rest of this group — the group says what something
    #: hangs on, not which budget it lands in, the same way `housing.interior`
    #: sits in the wants among fixed housing costs.
    PERSONAL_GIFTS = ("personal.gifts", "Personal", "Gifts & Celebrations", Budget.WANTS)
    #: Given away freely: association, church, relief organisation. Not
    #: `leisure.memberships` — that one buys belonging, this one buys nothing.
    PERSONAL_DONATIONS = ("personal.donations", "Personal", "Donations", Budget.WANTS)
    #: Courses, tuition, tutoring, exam fees. Separate from `personal.work`,
    #: which covers what an existing job costs — this is what the next one does.
    PERSONAL_EDUCATION = ("personal.education", "Personal", "Education", Budget.NEEDS)
    #: Income tax, church tax, back payments. The two grouped "Insurance & Taxes"
    #: entries cover what belongs to a flat or a vehicle; this is what belongs to
    #: the person and arrives as its own demand.
    PERSONAL_TAXES = ("personal.taxes", "Personal", "Taxes", Budget.NEEDS)

    # -- Income ------------------------------------------------------------
    INCOME_EARNED = ("income.earned", "Income", "Salary & Wages", Budget.INCOME)
    #: Child benefit, care allowance, housing benefit. Kept apart from earned income
    #: because a benefit belongs to the **household**, not to the person whose
    #: account it happens to land on — that is what `pass_through` marks.
    INCOME_BENEFITS = ("income.benefits", "Income", "Benefits", Budget.INCOME)
    #: Interest and capital gains. Separate because it does not come from work, and
    #: because it is the thing one does **not** plan: it simply accrues.
    INCOME_INTEREST = ("income.interest", "Income", "Interest", Budget.INCOME)
    #: Deposit refunds, second-hand sales, gifts. The one-offs that fit nowhere else.
    INCOME_OTHER = ("income.other", "Income", "Other Income", Budget.INCOME)

    # -- Finance -----------------------------------------------------------
    # Not consumption. The money is not gone — it sits somewhere else, or a debt got
    # smaller. A different kind of event from buying groceries, which is why it gets
    # a heading of its own instead of hiding among the expenses.
    FINANCE_SAVINGS = ("finance.savings", "Finance", "Reserves", Budget.SAVINGS)
    FINANCE_DEBT = ("finance.debt", "Finance", "Debt Repayment", Budget.SAVINGS)
    #: Investments are deliberate purchases — a list of their own, but part of the
    #: wants when it comes to the arithmetic.
    FINANCE_INVESTMENT = ("finance.investment", "Finance", "Investment", Budget.WANTS)
    #: Account fees, transfer charges, card fees. Recurring every month and a fit
    #: for none of the others — it is not a legal matter.
    FINANCE_FEES = ("finance.fees", "Finance", "Fees", Budget.NEEDS)
    #: Money going to another household member because they paid for something.
    #: Not an expense in economic terms: the purchase was already booked on their
    #: side. Household-wide evaluations have to exclude it, otherwise the same
    #: purchase counts twice. See issue #4.
    FINANCE_SETTLEMENT = ("finance.settlement", "Finance", "Settlement", Budget.NEEDS)


#: Longest value plus room to grow — `enum_column(Category, length=CATEGORY_LENGTH)`.
#: The default of 20 is not enough once the group sits in front of the dot.
CATEGORY_LENGTH = 40


#: **A suggestion for the frontend only.** It preselects the obvious budget in the
#: form, nothing more — the user decides.
#:
#: Deliberately not data logic: whether fuel is a need or a want depends on the
#: household and cannot be settled in general. Derived from the members, so it can
#: never fall out of step with them.
BUDGET_SUGGESTION: dict[Category, Budget] = {category: category.budget for category in Category}


def resolve_budget(chosen: Budget, commitment_type: "CommitmentType | None" = None) -> Budget:
    """Which 50/30/20 budget applies to this position?

    Three types are settled by arithmetic and override the choice:

        debt          repayment is committed money  →  SAVINGS
        savings_goal  saving is saving              →  SAVINGS
        income        money coming in is no budget  →  INCOME

    Everything else is the user's call. Whether fuel is a need or a want depends
    on the household — one person drives to work, another drives for fun.

    The result is **stored** on the position rather than computed on every read,
    so changing this function never rewrites plans that already exist.
    """
    if commitment_type in (CommitmentType.SAVINGS_GOAL, CommitmentType.DEBT):
        return Budget.SAVINGS
    elif commitment_type is CommitmentType.INCOME:
        return Budget.INCOME
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
