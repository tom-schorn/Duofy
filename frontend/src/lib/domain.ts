/**
 * A mirror of the backend enums in `backend/app/models/enums.py`, plus the German
 * labels for the UI.
 *
 * TODO: once i18n is in place the labels move into the translation files (`de`,
 * `en`) and only the types stay here.
 *
 * The keys match the backend enum values exactly — a divergence only shows up at
 * runtime.
 */

/**
 * Called **Budget** in the UI.
 *
 * TODO: rename it to `Budget` in the code as well. That only works together with
 * the backend, where the enum is called `Block` (`app/models/enums.py`). While the
 * two differ, every API call would need a translation layer — so rename the
 * backend first, then follow here.
 */
export type Block = 'income' | 'needs' | 'wants' | 'savings'

/** The wording for `needs` is not final yet — a shorter word is under discussion. */
export const BLOCK_LABEL: Record<Block, string> = {
  income: 'Einnahmen',
  needs: 'Fixkosten',
  wants: 'Wünsche',
  savings: 'Sparen',
}

/**
 * Block colours. Income sits above the split rather than inside it, so it uses a
 * muted tone instead of one of the three chart colours.
 */
export const BLOCK_DOT: Record<Block, string> = {
  income: 'bg-muted-foreground',
  needs: 'bg-chart-1',
  wants: 'bg-chart-2',
  savings: 'bg-chart-4',
}


/** The three budgets — 50 · 30 · 20, in that order. */
export const BUDGETS: Block[] = ['needs', 'wants', 'savings']

/** Order in lists — income sits above the split, not inside it. */
export const BUDGET_ORDER: Block[] = ['income', ...BUDGETS]

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
  // Persönlich
  | 'personal.insurance'
  | 'personal.communication'
  | 'personal.work'
  | 'personal.legal'
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
export const CATEGORY_LABEL: Record<Category, string> = {
  'household.groceries': 'Lebensmittel',
  'household.clothing': 'Kleidung',
  'household.healthcare': 'Gesundheit',
  'household.personal_care': 'Körperpflege',
  'household.cleaning': 'Reinigung',

  'housing.rent': 'Miete',
  'housing.utilities': 'Nebenkosten',
  'housing.repairs': 'Renovierung & Reparatur',
  'housing.interior': 'Einrichtung',
  'housing.outdoor': 'Außenbereich',
  'housing.insurance': 'Versicherung & Steuern',

  'transport.public': 'Öffentlicher Verkehr',
  'transport.fuel': 'Kraftstoff',
  'transport.repairs': 'Reparaturen',
  'transport.fines': 'Bußgelder & Gebühren',
  'transport.purchase': 'Fahrzeugkauf',
  'transport.insurance': 'Versicherung & Steuern',

  'children.care': 'Betreuung',
  'children.school': 'Schulbedarf',
  'children.allowance': 'Taschengeld',

  'leisure.vacation': 'Urlaub',
  'leisure.hobbies': 'Hobbys',
  'leisure.entertainment': 'Unterhaltung & Spiele',
  'leisure.memberships': 'Mitgliedschaften',
  'leisure.dining': 'Essen gehen',
  'leisure.subscriptions': 'Abos',

  'personal.insurance': 'Versicherung',
  'personal.communication': 'Kommunikation',
  'personal.work': 'Beruf',
  'personal.legal': 'Rechtliches',

  'income.earned': 'Gehalt & Lohn',
  'income.benefits': 'Transferleistungen',
  'income.interest': 'Zinsen',
  'income.other': 'Sonstige Einnahmen',

  'finance.savings': 'Rücklagen',
  'finance.debt': 'Tilgung',
  'finance.investment': 'Investition',
  'finance.fees': 'Gebühren',
  'finance.settlement': 'Ausgleich',
}

const CATEGORY_GROUP_LABEL: Record<string, string> = {
  household: 'Haushalt',
  housing: 'Wohnen',
  transport: 'Mobilität',
  children: 'Kinder',
  leisure: 'Freizeit',
  personal: 'Persönlich',
  income: 'Einnahmen',
  finance: 'Finanzen',
}

/** Everything before the dot — mirrors `Category.group` in the backend. */
export function categoryGroup(category: Category): string | null {
  const dot = category.indexOf('.')
  return dot === -1 ? null : category.slice(0, dot)
}

/**
 * The categories in dropdown order, cut into their groups.
 *
 * Every category currently sits under a heading. A `label` of `null` is what an
 * entry without a group would produce — it renders without a heading rather than
 * disappearing, so adding an ungrouped value later cannot break the list.
 */
export const CATEGORY_GROUPS: { label: string | null; categories: Category[] }[] = (() => {
  const groups: { label: string | null; categories: Category[] }[] = []

  for (const category of Object.keys(CATEGORY_LABEL) as Category[]) {
    const key = categoryGroup(category)
    const label = key === null ? null : CATEGORY_GROUP_LABEL[key]
    const previous = groups[groups.length - 1]

    if (previous && previous.label === label) previous.categories.push(category)
    else groups.push({ label, categories: [category] })
  }

  return groups
})()

/**
 * **A suggestion only.** Mirrored from `BLOCK_SUGGESTION` in the backend, which
 * derives it from the categories themselves.
 *
 * It preselects the obvious block in the form, nothing more — the user decides.
 * Deliberately not data logic: whether fuel is a need or a want depends on the
 * household.
 */
export const BLOCK_SUGGESTION: Record<Category, Block> = {
  'household.groceries': 'needs',
  'household.clothing': 'needs',
  'household.healthcare': 'needs',
  'household.personal_care': 'needs',
  'household.cleaning': 'needs',

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

  'personal.insurance': 'needs',
  'personal.communication': 'needs',
  // Work expenses: caused by the job, paid from private money.
  'personal.work': 'needs',
  'personal.legal': 'needs',

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

export type Rhythm = 'monthly' | 'quarterly' | 'biannual' | 'annual'

export const RHYTHM_LABEL: Record<Rhythm, string> = {
  monthly: 'monatlich',
  quarterly: 'quartalsweise',
  biannual: 'halbjährlich',
  annual: 'jährlich',
}

/** Distance in months — mirrors `Rhythm.interval`. */
export const RHYTHM_INTERVAL: Record<Rhythm, number> = {
  monthly: 1,
  quarterly: 3,
  biannual: 6,
  annual: 12,
}

export const MONTH_LABEL = [
  'Januar',
  'Februar',
  'März',
  'April',
  'Mai',
  'Juni',
  'Juli',
  'August',
  'September',
  'Oktober',
  'November',
  'Dezember',
]

/**
 * Which months does this fall due in? Mirrors `Commitment.is_due_in()`.
 *
 * Important: the rhythm continues across the turn of the year. Quarterly from July
 * means Jan, Apr, Jul, Oct — not only Jul and Oct. That is why every month is
 * tested individually instead of counting up from the start month.
 *
 * `%` returns a negative result for negative numbers in JavaScript, unlike Python
 * — adding `interval` before the second modulo compensates for that.
 */
export function dueMonths(rhythm: Rhythm, firstMonth: number | null): number[] {
  if (rhythm === 'monthly') return []
  const start = firstMonth ?? 1
  const interval = RHYTHM_INTERVAL[rhythm]
  const months: number[] = []
  for (let month = 1; month <= 12; month++) {
    if ((((month - start) % interval) + interval) % interval === 0) {
      months.push(month)
    }
  }
  return months
}

export const euro = new Intl.NumberFormat('de-DE', {
  style: 'currency',
  currency: 'EUR',
})

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
export function monthlyEquivalent(amount: string, rhythm: Rhythm): number {
  return Number(amount) / RHYTHM_INTERVAL[rhythm]
}

export type CommitmentType =
  | 'contract'
  | 'savings_goal'
  | 'debt'
  /** Money coming in: salary, benefits, interest. Nobody signs a contract for it. */
  | 'income'
  | 'budget'

/** Mirror of `Commitment` — one table for every type. */
export type Commitment = {
  id: string
  ownerId?: string
  type: CommitmentType
  name: string
  /** Kept as a string so nothing gets rounded while typing. */
  amount: string
  category: Category
  block: Block
  /** null means private. Set means generated positions go into that household. */
  householdId: string | null
  rhythm: Rhythm
  /**
   * When it first falls due — day, month and year. Mandatory for a non-monthly
   * rhythm. The month sets the cadence, the year the start.
   */
  firstDueDate: string | null
  dueDay: number
  active: boolean
  /** only for savings_goal */
  targetAmount: string | null
  targetDate: string | null
  /** only for debt */
  remainingDebt: string | null
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

/** The month the cadence starts in — taken from the start date. */
export function firstMonthOf(commitment: {
  firstDueDate: string | null
}): number | null {
  return commitment.firstDueDate
    ? Number(commitment.firstDueDate.slice(5, 7))
    : null
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

export const ACCOUNT_TYPE_LABEL: Record<AccountType, string> = {
  checking: 'Girokonto',
  savings: 'Tagesgeld',
  credit_card: 'Kreditkarte',
  settlement: 'Verrechnungskonto',
  payment_service: 'Zahlungsdienst',
  cash: 'Bargeld',
}

export type Account = {
  id: string
  ownerId?: string
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
 * pot count under `savings`: they carry no block, but the money has been put aside.
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
export type Transaction = {
  id: string
  ownerId?: string
  accountId: string
  /** Set means a transfer to another own account. */
  counterAccountId: string | null
  occurredOn: string
  /** Always positive — the direction comes from `block`. */
  amount: string
  note: string | null
  /** Empty on a pure transfer only. */
  category: Category | null
  block: Block | null
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

export const PAYMENT_LABEL: Record<PaymentMethod, string> = {
  withdrawal: 'Abhebung',
  transfer: 'Überweisung',
  standing_order: 'Dauerauftrag',
  direct_debit: 'Lastschrift',
  special: 'Besonderheit',
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
  block: Block
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
   * A budget rather than a single payment — groceries, fuel, pocket money.
   *
   * **Not ticked off**: such positions fill up over the month from individual
   * bookings. Instead of a tick box the row shows a fill level.
   */
  isBudget: boolean
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
export const ACCESS_LABEL: Record<Area, Record<AccessLevel, string>> = {
  plan: {
    plan: 'Nur gemeinsame Posten',
    view: 'Ganzer Monat sichtbar',
    edit: 'Darf ändern und anlegen',
    delete: 'Darf auch löschen',
  },
  commitments: {
    plan: 'Nichts',
    view: 'Verträge sichtbar',
    edit: 'Darf ändern und anlegen',
    delete: 'Darf auch löschen',
  },
  accounts: {
    plan: 'Nichts',
    view: 'Buch und Konten sichtbar',
    edit: 'Darf ändern und anlegen',
    delete: 'Darf auch löschen',
  },
}

export const ACCESS_HINT: Record<Area, Record<AccessLevel, string>> = {
  plan: {
    plan: 'Der Partner sieht, was ihr gemeinsam plant — sonst nichts.',
    view: 'Der Partner sieht zusätzlich deine privaten Posten des Monats.',
    edit: 'Der Partner darf Monate anlegen, Posten dazuschreiben, ändern und abhaken.',
    delete: 'Der Partner darf Posten außerdem endgültig löschen. Änderungen stehen im Protokoll, Löschungen nicht.',
  },
  commitments: {
    plan: 'Deine Verträge, Sparziele und Schulden bleiben für sich.',
    view: 'Der Partner sieht, was bei dir fest läuft — Betrag, Rhythmus, Kategorie.',
    edit: 'Der Partner darf Verträge für dich anlegen und ändern — der Weg, wenn dir jemand beim Einrichten hilft.',
    delete: 'Der Partner darf Verträge außerdem löschen. Schon erzeugte Posten bleiben stehen, künftige entstehen nicht mehr.',
  },
  accounts: {
    plan: 'Deine Konten und Buchungen bleiben für sich.',
    view: 'Der Partner sieht deine Kontostände und jede Buchung darauf.',
    edit: 'Der Partner darf Konten anlegen und ändern und auf ihnen buchen.',
    delete: 'Der Partner darf Konten und Buchungen außerdem löschen. Eine gelöschte Buchung hinterlässt eine Lücke im Kontostand, die nichts erklärt.',
  },
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

export const AREA_LABEL: Record<Area, string> = {
  plan: 'Planung',
  commitments: 'Verträge',
  accounts: 'Konten und Buch',
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
   * Income minus buffer — the basis the quotas refer to. **Not** the same as what
   * is left to allocate; that is the remainder of it.
   */
  budget: string
  /** Allocated per block. */
  spent: Record<'needs' | 'wants' | 'savings', string>
  /** Sum of the positions that are not ticked off yet. */
  unpaid: string
  /** Households that positions of this plan feed into. Empty means fully private. */
  householdIds: string[]
}

/** A plan together with its positions. */

export type PlanDetail = PlanSummary & {
  id: string
  positions: PlanPosition[]
}

/**
 * The shared plan. Composed, not stored — hence no `id`: there is no row behind it,
 * only the positions of every member that carry this `householdId`.
 */
export type HouseholdPlanDetail = PlanSummary & {
  householdId: string
  householdName: string
  positions: HouseholdPosition[]
}

/** Like a position, plus the person behind it — the point of the shared view. */
export type HouseholdPosition = PlanPosition & {
  ownerId: string
  ownerName: string
}

/** What of the budget has not been allocated to the three blocks yet. */
export function unallocated(plan: PlanSummary): number {
  const spent =
    Number(plan.spent.needs) + Number(plan.spent.wants) + Number(plan.spent.savings)
  return Number(plan.budget) - spent
}

/**
 * What is still to leave the account for this position.
 *
 * Ticked off means done, and income never leaves. Otherwise what counts is the
 * planned amount minus what the book already records: a 600 budget with 127.50 of
 * purchases booked still expects 472.50, not 600.
 *
 * Never negative — overspending a budget does not leave anything over. Same rule as
 * `remaining()` in the backend, so that both figures mean the same thing.
 */
export function stillDue(position: PlanPosition): number {
  if (position.block === 'income' || isPaid(position)) return 0
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
