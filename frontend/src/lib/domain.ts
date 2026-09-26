/**
 * A mirror of the backend enums in `backend/app/models/enums.py`, plus the
 * functions that turn them into words for the UI.
 *
 * The words themselves live in the catalog (`src/locales/de.json`, under
 * `enums.*`) — only the types, orders and rules stay here.
 *
 * The keys match the backend enum values exactly — a divergence only shows up at
 * runtime.
 */

import { i18n, locale } from '@/lib/i18n'

/** Called **Budget** in the UI — one of the three 50/30/20 pots, or income. */
export type Budget = 'income' | 'needs' | 'wants' | 'savings'

/** The wording for `needs` is not final yet — a shorter word is under discussion. */
export function budgetLabel(budget: Budget): string {
  return i18n.t(`enums.budget.${budget}`)
}

/**
 * Budget colours. Income sits above the split rather than inside it, so it uses a
 * muted tone instead of one of the three chart colours.
 */
export const BUDGET_DOT: Record<Budget, string> = {
  income: 'bg-muted-foreground',
  needs: 'bg-chart-1',
  wants: 'bg-chart-2',
  savings: 'bg-chart-4',
}


/** The three budgets — 50 · 30 · 20, in that order. */
export const BUDGETS: Budget[] = ['needs', 'wants', 'savings']

/** Order in lists — income sits above the split, not inside it. */
export const BUDGET_ORDER: Budget[] = ['income', ...BUDGETS]

/**
 * Mirrored from `Category` in the backend.
 *
 * The dot carries the hierarchy: everything before it is the group, and a value
 * without a dot stands on its own. Nothing else reads the dot — it is the same
 * single string the API sends and the database stores.
 */
export type Category =
  // Haushalt
  | 'household.groceries'
  | 'household.clothing'
  | 'household.healthcare'
  | 'household.personal_care'
  | 'household.cleaning'
  | 'household.pets'
  // Wohnen
  | 'housing.rent'
  | 'housing.utilities'
  | 'housing.repairs'
  | 'housing.interior'
  | 'housing.outdoor'
  | 'housing.insurance'
  // Mobilität
  | 'transport.public'
  | 'transport.fuel'
  | 'transport.repairs'
  | 'transport.fines'
  | 'transport.purchase'
  | 'transport.insurance'
  // Kinder
  | 'children.care'
  | 'children.school'
  | 'children.allowance'
  // Freizeit
  | 'leisure.vacation'
  | 'leisure.hobbies'
  | 'leisure.entertainment'
  | 'leisure.memberships'
  | 'leisure.dining'
  | 'leisure.subscriptions'
  | 'leisure.indulgences'
  // Persönlich
  | 'personal.insurance'
  | 'personal.communication'
  | 'personal.work'
  | 'personal.legal'
  | 'personal.gifts'
  | 'personal.donations'
  | 'personal.education'
  | 'personal.taxes'
  // Einnahmen
  | 'income.earned'
  | 'income.benefits'
  | 'income.interest'
  | 'income.other'
  // Finanzen
  | 'finance.savings'
  | 'finance.debt'
  | 'finance.investment'
  | 'finance.fees'
  | 'finance.settlement'

/** The order here is the order in every dropdown — grouped entries first. */
export const CATEGORIES: Category[] = [
  'household.groceries',
  'household.clothing',
  'household.healthcare',
  'household.personal_care',
  'household.cleaning',
  'household.pets',

  'housing.rent',
  'housing.utilities',
  'housing.repairs',
  'housing.interior',
  'housing.outdoor',
  'housing.insurance',

  'transport.public',
  'transport.fuel',
  'transport.repairs',
  'transport.fines',
  'transport.purchase',
  'transport.insurance',

  'children.care',
  'children.school',
  'children.allowance',

  'leisure.vacation',
  'leisure.hobbies',
  'leisure.entertainment',
  'leisure.memberships',
  'leisure.dining',
  'leisure.subscriptions',
  'leisure.indulgences',

  'personal.insurance',
  'personal.communication',
  'personal.work',
  'personal.legal',
  'personal.gifts',
  'personal.donations',
  'personal.education',
  'personal.taxes',

  'income.earned',
  'income.benefits',
  'income.interest',
  'income.other',

  'finance.savings',
  'finance.debt',
  'finance.investment',
  'finance.fees',
  'finance.settlement',
]

export function categoryLabel(category: Category): string {
  return i18n.t(`enums.category.${category}`)
}

/** The heading of a category group — `household`, `housing`, … */
export function categoryGroupLabel(group: string): string {
  return i18n.t(`enums.categoryGroup.${group}`)
}

/** Everything before the dot — mirrors `Category.group` in the backend. */
export function categoryGroup(category: Category): string | null {
  const dot = category.indexOf('.')
  return dot === -1 ? null : category.slice(0, dot)
}

/**
 * The categories in dropdown order, cut into their groups.
 *
 * Every category currently sits under a heading. A `group` of `null` is what an
 * entry without a group would produce — it renders without a heading rather than
 * disappearing, so adding an ungrouped value later cannot break the list.
 *
 * Carries the group **key**, not its label: the label depends on the language
 * and is looked up when rendering (`categoryGroupLabel`).
 */
export const CATEGORY_GROUPS: { group: string | null; categories: Category[] }[] = (() => {
  const groups: { group: string | null; categories: Category[] }[] = []

  for (const category of CATEGORIES) {
    const group = categoryGroup(category)
    const previous = groups[groups.length - 1]

    if (previous && previous.group === group) previous.categories.push(category)
    else groups.push({ group, categories: [category] })
  }

  return groups
})()

/**
 * **A suggestion only.** Mirrored from `BUDGET_SUGGESTION` in the backend, which
 * derives it from the categories themselves.
 *
 * It preselects the obvious budget in the form, nothing more — the user decides.
 * Deliberately not data logic: whether fuel is a need or a want depends on the
 * household.
 */
export const BUDGET_SUGGESTION: Record<Category, Budget> = {
  'household.groceries': 'needs',
  'household.clothing': 'needs',
  'household.healthcare': 'needs',
  'household.personal_care': 'needs',
  'household.cleaning': 'needs',
  'household.pets': 'needs',

  'housing.rent': 'needs',
  'housing.utilities': 'needs',
  'housing.repairs': 'needs',
  // Furnishing is a deliberate purchase — unlike a repair it can wait.
  'housing.interior': 'wants',
  'housing.outdoor': 'wants',
  'housing.insurance': 'needs',

  'transport.public': 'needs',
  'transport.fuel': 'needs',
  'transport.repairs': 'needs',
  // Nobody plans a fine, but it is not a want either.
  'transport.fines': 'needs',
  'transport.purchase': 'wants',
  'transport.insurance': 'needs',

  'children.care': 'needs',
  'children.school': 'needs',
  'children.allowance': 'wants',

  'leisure.vacation': 'wants',
  'leisure.hobbies': 'wants',
  'leisure.entertainment': 'wants',
  'leisure.memberships': 'wants',
  'leisure.dining': 'wants',
  'leisure.subscriptions': 'wants',
  'leisure.indulgences': 'wants',

  'personal.insurance': 'needs',
  'personal.communication': 'needs',
  // Work expenses: caused by the job, paid from private money.
  'personal.work': 'needs',
  'personal.legal': 'needs',
  'personal.gifts': 'wants',
  'personal.donations': 'wants',
  'personal.education': 'needs',
  'personal.taxes': 'needs',

  'income.earned': 'income',
  'income.benefits': 'income',
  'income.interest': 'income',
  'income.other': 'income',

  'finance.savings': 'savings',
  'finance.debt': 'savings',
  // Investments are ordinary wants — no special treatment, no group of their own,
  // no quota of their own.
  'finance.investment': 'wants',
  // Account and card fees — they cannot be cancelled.
  'finance.fees': 'needs',
  // Reimbursing a household member. Not an expense in economic terms — exclude it
  // from household-wide evaluations, see issue #4.
  'finance.settlement': 'needs',
}

/** Allowed distance between two due dates, in months. Mirrors the backend range check. */
export const INTERVAL_MIN = 1
export const INTERVAL_MAX = 120

/** The distances the dialog offers directly; everything else is "anderer Abstand". */
export const INTERVAL_PRESETS: number[] = [1, 3, 6, 12]

/** Is this a whole number the backend accepts? */
export function isValidInterval(value: number): boolean {
  return Number.isInteger(value) && value >= INTERVAL_MIN && value <= INTERVAL_MAX
}

/** `monatlich` for 1, `vierteljährlich` for 3, otherwise `alle N Monate`. */
export function intervalLabel(intervalMonths: number): string {
  if (intervalMonths === 1) return i18n.t('enums.interval.monthly')
  if (intervalMonths === 3) return i18n.t('enums.interval.quarterly')
  return i18n.t('enums.interval.every', { number: intervalMonths })
}

/** The months, 1 to 12. */
export const MONTHS: number[] = Array.from({ length: 12 }, (_, index) => index + 1)

/** `März` for 3 — the name comes from `Intl` in the active language. */
export function monthLabel(month: number): string {
  return new Intl.DateTimeFormat(locale(), { month: 'long' }).format(
    new Date(2000, month - 1, 1)
  )
}

/**
 * Is a commitment due in this month? Mirrors `Commitment.is_due_in()`.
 *
 * Counts in absolute months from the start, so the cadence runs across the turn of
 * the year (every 3 months from July 2026 hits January 2027) and an interval that
 * does not divide 12 keeps its own cadence (every 5 months from November 2026 hits
 * April 2027, then September 2027). Without a start date only a monthly commitment
 * is due, in every month.
 */
export function isDueIn(
  intervalMonths: number,
  firstDueDate: string | null,
  year: number,
  month: number
): boolean {
  if (firstDueDate === null) return intervalMonths === 1
  const start = Number(firstDueDate.slice(0, 4)) * 12 + Number(firstDueDate.slice(5, 7))
  const total = year * 12 + month
  return total >= start && (total - start) % intervalMonths === 0
}

/** A month in a year — what the due-date list works with. */
export type YearMonth = { year: number; month: number }

/**
 * The next due dates from a month on (that month included), earliest first.
 *
 * Built on `isDueIn`, so it follows the same absolute-month rule as the backend:
 * every 5 months from November 2026, seen from January 2027, gives April 2027,
 * September 2027, February 2028. Empty for a monthly commitment — "every month"
 * needs no list — and without a start date.
 */
export function nextDueDates(
  intervalMonths: number,
  firstDueDate: string | null,
  from: YearMonth,
  count: number
): YearMonth[] {
  if (firstDueDate === null || !isValidInterval(intervalMonths) || intervalMonths === 1) {
    return []
  }
  const start = Number(firstDueDate.slice(0, 4)) * 12 + Number(firstDueDate.slice(5, 7))
  const begin = from.year * 12 + from.month
  // Far enough to reach the start and then `count` steps beyond it.
  const end = Math.max(begin, start) + count * intervalMonths
  const dates: YearMonth[] = []
  for (let total = begin; total <= end && dates.length < count; total++) {
    const year = Math.floor((total - 1) / 12)
    const month = total - year * 12
    if (isDueIn(intervalMonths, firstDueDate, year, month)) dates.push({ year, month })
  }
  return dates
}

/** `Apr 2027` — the short month name from `Intl`, with the year. */
export function dueDateLabel({ year, month }: YearMonth): string {
  return `${monthLabel(month).slice(0, 3)} ${year}`
}

/**
 * The text of the distance field as a value: a whole number from 1 to 120, or 0
 * when the field is empty or holds anything else. 0 is what the range check
 * rejects, so saving stays blocked until the field is right.
 */
export function parseIntervalText(text: string): number {
  if (text.trim() === '') return 0
  const parsed = Number(text)
  return isValidInterval(parsed) ? parsed : 0
}

/**
 * Amounts in euro, formatted for the active language.
 *
 * Built per call rather than once at load time: a constant would freeze the
 * language the app happened to start with.
 */
export const euro = {
  format(value: number | bigint): string {
    return new Intl.NumberFormat(locale(), {
      style: 'currency',
      currency: 'EUR',
    }).format(value)
  },
}

/** Day 0 of the next month is the last day of this one — leap years included. */
export function daysInMonth(year: number, month: number): number {
  return new Date(year, month, 0).getDate()
}

/**
 * The day something actually falls due.
 *
 * A due day of 31 exists in seven months only. Rather than dropping the position,
 * it moves to the last day of the month — the 28th or 29th in February.
 *
 * The backend applies the same rule when generating positions
 * (`Commitment.effective_due_day`).
 */
export function effectiveDueDay(
  dueDay: number,
  year: number,
  month: number
): number {
  return Math.min(dueDay, daysInMonth(year, month))
}

/** From the 29th on the day can shift — only then does it need explaining. */
export const DUE_DAY_MAY_SHIFT = 29

/**
 * What the thing costs **per month**.
 *
 * Needed because a yearly amount must not be added to monthly ones: 108.40 a year
 * is 9.03 a month, not 108.40. Without this conversion every budget total would be
 * wrong.
 */
export function monthlyEquivalent(amount: string, intervalMonths: number): number {
  return Number(amount) / intervalMonths
}

export type CommitmentType =
  | 'contract'
  | 'savings_goal'
  | 'debt'
  /** Money coming in: salary, benefits, interest. Nobody signs a contract for it. */
  | 'income'

/** Mirror of `Commitment` — one table for every type. */
export type Commitment = {
  id: string
  ownerId?: string
  /** No month refers to it yet, so it may still be deleted. Comes from the server. */
  deletable: boolean
  type: CommitmentType
  name: string
  /** Kept as a string so nothing gets rounded while typing. */
  amount: string
  category: Category
  budget: Budget
  /**
   * No fixed sum that gets ticked off — Lebensmittel, Sprit, Taschengeld. The
   * generated position fills up over the month from bookings instead, and shows as
   * running ("laufend") rather than with a checkbox. Sits on the commitment, not
   * only on the position, because it is decided once and copied in every month.
   */
  isLimit: boolean
  /** null means private. Set means generated positions go into that household. */
  householdId: string | null
  /** Months between two due dates, 1 to 120. */
  intervalMonths: number
  /**
   * When it first falls due — day, month and year, for every commitment. The month
   * sets the cadence, the year the start, the day the due day: it is stored nowhere
   * else.
   */
  firstDueDate: string
  /**
   * The last month in which it falls due, or null for "runs indefinitely". Only year
   * and month count; the day is whatever was picked and is ignored.
   */
  endsOn: string | null
  /** only for savings_goal */
  targetAmount: string | null
  targetDate: string | null
  /** Copied into the generated positions, overridable per month there. */
  paymentMethod: PaymentMethod | null
  /** Which account it is paid from. null means the default account. */
  accountId: string | null
  /**
   * Where the money is saved to. Set means ticking off books a **transfer** rather
   * than an expense — needed as soon as the money moves to another own account.
   * null for everything that really leaves.
   */
  counterAccountId: string | null
  /**
   * A pass-through position — money that was never there to be spent.
   *
   * Earmarked benefits and refunds arrive and move straight on. They stay visible
   * and they move the account balance, but they count towards no budget and no
   * quota.
   */
  passThrough: boolean
}

/** Which commitments the list asks for — computed by the backend against today. */
export type CommitmentStatus = 'active' | 'ended' | 'all'

export const COMMITMENT_STATUSES: CommitmentStatus[] = ['active', 'ended', 'all']

/** Has a commitment with this end month stopped? Mirrors the backend's `ended` filter. */
export function hasEnded(endsOn: string | null, today: Date = new Date()): boolean {
  if (endsOn === null) return false
  const ends = Number(endsOn.slice(0, 4)) * 12 + Number(endsOn.slice(5, 7))
  return ends < today.getFullYear() * 12 + today.getMonth() + 1
}

/** `März 2026` for `2026-03-15` — the day does not matter. */
export function endMonthLabel(endsOn: string): string {
  return `${monthLabel(Number(endsOn.slice(5, 7)))} ${endsOn.slice(0, 4)}`
}

/** The due day of a commitment — the day of its first due date (`2026-03-31` → 31). */
export function dueDayOf(firstDueDate: string): number {
  return Number(firstDueDate.slice(8, 10))
}

/**
 * **Payment accounts only** — things with a balance that follows from bookings. A
 * securities account is deliberately not one of them: its value comes from market
 * prices. Only its settlement account appears in the book.
 */
export type AccountType =
  | 'checking'
  | 'savings'
  | 'credit_card'
  | 'settlement'
  | 'payment_service'
  | 'cash'

/** Every account type, in the order of the dropdown. */
export const ACCOUNT_TYPES: AccountType[] = [
  'checking',
  'savings',
  'credit_card',
  'settlement',
  'payment_service',
  'cash',
]

export function accountTypeLabel(type: AccountType): string {
  return i18n.t(`enums.accountType.${type}`)
}

export type Account = {
  id: string
  ownerId?: string
  /** Nothing is booked on it yet, so it may still be deleted. Comes from the server. */
  deletable: boolean
  name: string
  type: AccountType
  /** Kept as a string so nothing gets rounded while typing. */
  openingBalance: string
  /** The date the opening balance applied — without it no balance is computable. */
  openingDate: string
  /** At most one per person. Preselected in the quick entry. */
  isDefault: boolean
  active: boolean
  externalRef: string | null
  /**
   * Does money here still count as spendable?
   *
   * True for a current account, false for savings, where the money is earmarked. A
   * transfer to an account with `false` counts as an expense in the book.
   */
  countsAsAvailable: boolean
  /** Opening balance plus every booking. Comes from the server, never sent. */
  balance?: string
  /** Only set in the household view: who owns the account. */
  ownerName?: string | null
}

/**
 * Whose book is currently shown.
 *
 * One concept instead of three optional props: accounts, bookings and the balance
 * chart all need the same information, and it has to reach the query key too —
 * otherwise the household view would overwrite your own list in the cache.
 */
export type BookScope =
  | { kind: 'own' }
  | { kind: 'member'; ownerId: string }
  | { kind: 'household'; householdId: string }

export const OWN_SCOPE: BookScope = { kind: 'own' }

/** Query suffix for the API. Starts with `&`, so it fits after `?year=…`. */
export function scopeQuery(scope: BookScope): string {
  if (scope.kind === 'member') return `&owner=${scope.ownerId}`
  if (scope.kind === 'household') return `&household=${scope.householdId}`
  return ''
}

/** The stable part of the query key. */
export function scopeKey(scope: BookScope): string {
  if (scope.kind === 'member') return `member:${scope.ownerId}`
  if (scope.kind === 'household') return `household:${scope.householdId}`
  return 'own'
}

/**
 * One day of movement, broken down — every figure a positive amount.
 *
 * `change` is `income − needs − wants − savings`. Transfers leaving the spendable
 * pot count under `savings`: they carry no budget, but the money has been put aside.
 */
export type BalanceMoves = {
  income: string
  needs: string
  wants: string
  savings: string
}

/** A day with movement, together with the balance at its end. */
export type BalancePoint = {
  day: string
  balance: string
  change: string
  moves: BalanceMoves
}

/**
 * The overall balance across one calendar month.
 *
 * `openingBalance` is the balance **before** the first day — without it the curve
 * would start at zero and every month would look like a fresh start.
 */
export type BalanceHistory = {
  openingBalance: string
  closingBalance: string
  points: BalancePoint[]
}

/**
 * A booking in the household book.
 *
 * The effect on balances and the effect on the budget are independent:
 * `counterAccountId` decides the balances, `positionId` decides the budget. Moving
 * money to savings is both at once — a transfer that fulfils the savings quota.
 */
/**
 * `carry_over` states the balance an account goes into a month with (#94). It moves
 * no money and counts in no sum. Mirror of the backend `TransactionKind`.
 */
export type TransactionKind = 'booking' | 'carry_over'

export type Transaction = {
  id: string
  kind: TransactionKind
  ownerId?: string
  accountId: string
  /** Set means a transfer to another own account. */
  counterAccountId: string | null
  occurredOn: string
  /** Positive — the direction comes from `budget`. Only a carry-over has a sign. */
  amount: string
  note: string | null
  /** Empty on a pure transfer only. */
  category: Category | null
  budget: Budget | null
  positionId: string | null
  /** Only set in the household view: who booked it. */
  ownerName?: string | null
  /** Created by ticking off — un-ticking removes exactly these again. */
  autoBooked: boolean
  externalRef: string | null
}

export type PaymentMethod =
  | 'withdrawal'
  | 'transfer'
  | 'standing_order'
  | 'direct_debit'
  | 'special'

/** Every payment method, in the order of the dropdown. */
export const PAYMENT_METHODS: PaymentMethod[] = [
  'withdrawal',
  'transfer',
  'standing_order',
  'direct_debit',
  'special',
]

export function paymentLabel(method: PaymentMethod): string {
  return i18n.t(`enums.paymentMethod.${method}`)
}

/** Mirror of `Plan` — always belongs to a person, never to a household. */
export type Plan = {
  year: number
  month: number
  /** Quotas in percent. Guidelines, not rules. */
  targetNeeds: string
  targetWants: string
  targetSavings: string
  /** How many percent of the income is deliberately left unplanned. */
  bufferPercent: string
}

/** Mirror of `PlanPosition` — one item in exactly one monthly plan. */
export type PlanPosition = {
  id: string
  label: string
  amountPlanned: string
  /** Filled in over the course of the month. */
  amountActual: string | null
  category: Category
  /** Frozen on creation — later changes do not act retroactively. */
  budget: Budget
  dueDay: number
  /** Copied from the commitment, overridable per month. null means the default. */
  accountId: string | null
  /**
   * Where the money is saved to. Set means ticking off books a **transfer** rather
   * than an expense — needed as soon as the money moves to another own account.
   * null for everything that really leaves.
   */
  counterAccountId: string | null
  paymentMethod: PaymentMethod | null
  /**
   * No fixed sum, no single payment — groceries, fuel, pocket money. Copied from
   * the commitment's `isLimit` when the position is generated.
   *
   * **Not ticked off**: such positions fill up over the month from individual
   * bookings. Instead of a tick box the row shows a fill level, and counts towards
   * no "Anstehend".
   */
  isLimit: boolean
  /**
   * A pass-through position — money that was never there to be spent.
   *
   * Earmarked benefits and refunds arrive and move straight on. They stay visible
   * and they move the account balance, but they count towards no budget and no
   * quota.
   */
  passThrough: boolean
  /** null means private. Set means it appears in that household plan. */
  householdId: string | null
  /** Empty on one-off positions that do not come from a commitment. */
  commitmentId: string | null
  /** When it was ticked off. null means still open. */
  paidAt: string | null
}

export type Me = {
  id: string
  email: string
  firstName: string
  lastName: string
}

/**
 * What the import thinks an entry is, and why.
 *
 * Worked out on the server while the list is read, never stored — a suggestion
 * written into the row would go stale the moment somebody assigns something.
 */
export type Suggestion = {
  /**
   * Which of three questions this answers — they lead to three different
   * buttons, and a row that offered "Übernehmen" for all of them would book a
   * duplicate on the third.
   *
   * * `category` — what this was for. The everyday case
   * * `transfer` — money moving to another own account. Not spending
   * * `alreadyBooked` — the other side of a movement the book already holds
   */
  kind: 'category' | 'transfer' | 'already_booked'
  /** Empty on a transfer: a movement between own accounts has no category. */
  category: Category | null
  /** The position it fits, from the month of the booking or the one after. */
  positionId: string | null
  /** On a transfer: the own account the money goes to or comes from. */
  counterAccountId: string | null
  counterAccountName: string | null
  /**
   * Was the other side **named** or only inferred?
   *
   * `true` means the bank supplied an IBAN belonging to one of your accounts —
   * nothing to doubt. `false` means amount, direction and date alone, which is
   * all that is left when the bank names nobody. The row phrases a guess as a
   * question and a fact as a statement; showing both the same way would make
   * the reliable case look as shaky as the other one.
   */
  certain: boolean
  reason: string
}

/** One entry read out of a bank file, waiting to be understood. */
export type ImportedEntry = {
  id: string
  accountId: string
  ownerId: string

  /** What the bank reported. None of it is editable. */
  occurredOn: string
  valueOn: string
  amount: string
  incoming: boolean
  counterpartyName: string | null
  counterpartyIban: string | null
  purpose: string | null

  /** The interpretation — empty until somebody assigns it. */
  positionId: string | null
  category: Category | null
  budget: Budget | null

  /** Set means this is a transfer to another own account, not spending. */
  counterAccountId: string | null

  discardedAt: string | null

  /** Only on rows nobody has assigned yet. */
  suggestion: Suggestion | null
}

/** What one upload did. */
export type ImportSummary = {
  accountId: string
  iban: string
  read: number
  parked: number
  known: number
  /**
   * Opening plus every booked entry equals closing. `false` means a page is
   * missing or something was misread — the import cannot tell which.
   */
  balancesMatch: boolean
  /**
   * Set when the file names an IBAN no account carries yet. Not an error: the
   * user is asked once which account it is, then never again.
   */
  unknownIban: string | null
}

export type Role = 'owner' | 'member'

/**
 * What somebody allows the other members to see about themselves.
 *
 * Sits on your **own** membership: whoever owns the data decides. Nobody can grant
 * themselves insight into somebody else accounts.
 */
export type AccessLevel = 'plan' | 'view' | 'edit' | 'delete'

/**
 * The rungs, in order. Mirrors `AccessLevel.rank` in the backend.
 *
 * **Compare with `atLeast`, never with `===`.** A check written as
 * `level === 'edit'` turns false the moment a higher level exists, which takes
 * the right to edit away from exactly the person who was trusted most. That
 * happened once already, in three places at the same time.
 */
const ACCESS_RANK: Record<AccessLevel, number> = {
  plan: 0,
  view: 1,
  edit: 2,
  delete: 3,
}

/** Is `level` at least `needed`? */
export function atLeast(level: AccessLevel, needed: AccessLevel): boolean {
  return ACCESS_RANK[level] >= ACCESS_RANK[needed]
}

/**
 * What a level means, per area — the same word promises different things.
 * "Sehen" on a month is the shared plan plus the private positions; on a contract
 * it is the contract itself, which nobody used to be able to share at all.
 */
export function accessLabel(area: Area, level: AccessLevel): string {
  return i18n.t(`enums.access.${area}.${level}`)
}

/** One sentence on what the level allows, per area. */
export function accessHint(area: Area, level: AccessLevel): string {
  return i18n.t(`enums.accessHint.${area}.${level}`)
}

export const ACCESS_ORDER: AccessLevel[] = ['plan', 'view', 'edit', 'delete']

export type Member = {
  userId: string
  firstName: string
  lastName: string
  email: string
  role: Role
  /**
   * What this person allows the others to see about themselves, one level per
   * area. Sharing the month you plan is a small step; handing over the contracts
   * behind it is a much larger one, so they are answered separately.
   */
  grantsPlan: AccessLevel
  grantsCommitments: AccessLevel
  /** Covers the book too — an account you may see comes with its bookings. */
  grantsAccounts: AccessLevel
}

/** The three areas a grant can be given for. Mirrors `Area` in the backend. */
export type Area = 'plan' | 'commitments' | 'accounts'

export function areaLabel(area: Area): string {
  return i18n.t(`enums.area.${area}`)
}

export const AREA_ORDER: Area[] = ['plan', 'commitments', 'accounts']

/** The `Member` fields the levels live in. */
export type AreaField = 'grantsPlan' | 'grantsCommitments' | 'grantsAccounts'

/** Which field on `Member` carries the level for an area. */
export const AREA_FIELD: Record<Area, AreaField> = {
  plan: 'grantsPlan',
  commitments: 'grantsCommitments',
  accounts: 'grantsAccounts',
}

export type Household = {
  id: string
  name: string
  targetNeeds: string
  targetWants: string
  targetSavings: string
  bufferPercent: string
  members: Member[]
}

export type Invitation = {
  id: string
  householdId: string
  email: string
  status: 'pending' | 'accepted' | 'declined' | 'revoked'
  expiresAt: string
  token: string
}

/**
 * A pending invitation to your own address — the inbox.
 *
 * No email and no forwarded link are needed: whoever signs in with the invited
 * address finds the invitation waiting in the app.
 */
export type MyInvitation = {
  token: string
  householdId: string
  householdName: string
  invitedBy: string
  expiresAt: string
}

/**
 * One row in the plan overview — the totals arrive ready-made from the backend so
 * the overview does not have to load every position of every month.
 *
 * Amounts arrive as strings, which is how Decimal travels through JSON.
 */
export type PlanSummary = Plan & {
  income: string
  /**
   * Income minus buffer — the basis the quotas refer to, shown in the UI as
   * "Verplanbar". **Not** the same as what is left to allocate; that is the
   * remainder of it.
   */
  distributable: string
  /** Allocated per budget. */
  spent: Record<'needs' | 'wants' | 'savings', string>
  /** Sum of the positions that are not ticked off yet. */
  unpaid: string
  /** Households that positions of this plan feed into. Empty means fully private. */
  householdIds: string[]
}

/** Mirror of `Hint` — something the backend wants the plan to point out. */
export type PlanHint = {
  /** Stable identifier; the catalog key is `hints.<code>`. */
  code: string
  severity: 'info' | 'warning'
  /** null for a hint about the whole month. */
  positionId: string | null
  /** The values the text needs, never the text. */
  params: Record<string, string | number>
}

/** A plan together with its positions. */

export type PlanDetail = PlanSummary & {
  id: string
  /** Derived by the backend on every read; the frontend never computes one. */
  hints: PlanHint[]
  positions: PlanPosition[]
}

/**
 * The shared plan. Composed, not stored — hence no `id`: there is no row behind it,
 * only the positions of every member that carry this `householdId`.
 */
export type HouseholdPlanDetail = PlanSummary & {
  householdId: string
  householdName: string
  hints: PlanHint[]
  positions: HouseholdPosition[]
}

/** Like a position, plus the person behind it — the point of the shared view. */
export type HouseholdPosition = PlanPosition & {
  ownerId: string
  ownerName: string
}

/** What of the distributable amount has not been allocated to the three budgets yet. */
export function unallocated(plan: PlanSummary): number {
  const spent =
    Number(plan.spent.needs) + Number(plan.spent.wants) + Number(plan.spent.savings)
  return Number(plan.distributable) - spent
}

/**
 * What is still to leave the account for this position.
 *
 * Ticked off means done, and income never leaves. A limit position never carries
 * a tick either — it has no due amount of its own, only a fill level, so it never
 * counts towards "Anstehend". Otherwise what counts is the planned amount minus
 * what the book already records: a 600 limit with 127.50 of purchases booked still
 * expects 472.50, not 600.
 *
 * Never negative — overspending a limit does not leave anything over. Same rule as
 * `remaining()` in the backend, so that both figures mean the same thing.
 */
export function stillDue(position: PlanPosition): number {
  if (position.budget === 'income' || isPaid(position) || position.isLimit) return 0
  // Stands and falls with its own income — no money of your own is missing.
  if (position.passThrough) return 0
  const booked = Number(position.amountActual ?? 0)
  return Math.max(Number(position.amountPlanned) - booked, 0)
}

/** Has the position been ticked off yet? */
export function isPaid(position: PlanPosition): boolean {
  return position.paidAt !== null
}

export const QUOTA_KEY: Record<
  'needs' | 'wants' | 'savings',
  'targetNeeds' | 'targetWants' | 'targetSavings'
> = {
  needs: 'targetNeeds',
  wants: 'targetWants',
  savings: 'targetSavings',
}

/** The heading of a budget section; the focus lands there after a position was deleted. */
export const budgetHeadingId = (budget: Budget) => `budget-heading-${budget}`
