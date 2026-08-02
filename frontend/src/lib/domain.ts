/**
 * Spiegel der Backend-Enums aus `backend/app/models/enums.py`, plus die
 * deutschen Beschriftungen fürs UI.
 *
 * TODO: Sobald i18n dran ist, wandern die Labels in die Übersetzungsdateien
 * (`de`, `en`) — hier bleiben dann nur die Typen stehen.
 *
 * Die Schlüssel entsprechen exakt den Enum-Werten des Backends — weicht einer
 * ab, fällt es erst zur Laufzeit auf.
 */

/**
 * Im UI heißt das **Budget**.
 *
 * TODO: Auch im Code zu `Budget` umbenennen. Das geht nur zusammen mit dem
 * Backend — dort heißt das Enum `Block` (`app/models/enums.py`). Solange
 * beide auseinanderlaufen, bräuchte es eine Übersetzungsschicht bei jedem
 * API-Aufruf. Deshalb: erst Backend umbenennen, dann hier nachziehen.
 */
export type Block = 'income' | 'needs' | 'wants' | 'savings'

/** „Fixkosten" ist noch nicht endgültig — „Muss" steht als Alternative im Raum. */
export const BLOCK_LABEL: Record<Block, string> = {
  income: 'Einnahmen',
  needs: 'Fixkosten',
  wants: 'Wünsche',
  savings: 'Sparen',
}

/**
 * Budgetfarben. Einnahmen tragen keine eigene — sie stehen über der
 * Aufteilung, nicht darin. `--chart-3` bleibt ungenutzt.
 */
export const BLOCK_DOT: Record<Block, string> = {
  income: 'bg-muted-foreground',
  needs: 'bg-chart-1',
  wants: 'bg-chart-2',
  savings: 'bg-chart-4',
}


/** Die drei Budgets — 50 · 30 · 20, in dieser Reihenfolge. */
export const BUDGETS: Block[] = ['needs', 'wants', 'savings']

/** Reihenfolge in Listen — Einnahmen stehen über der Aufteilung, nicht darin. */
export const BUDGET_ORDER: Block[] = ['income', ...BUDGETS]

export type Category =
  | 'income'
  | 'housing'
  | 'insurance'
  | 'groceries'
  | 'health'
  | 'mobility'
  | 'communication'
  | 'children'
  | 'subscriptions'
  | 'leisure'
  | 'vacation'
  | 'pocket_money'
  | 'reserves'
  | 'debt_repayment'
  | 'investment'
  | 'legal'
  | 'work'

export const CATEGORY_LABEL: Record<Category, string> = {
  income: 'Einnahmen',
  housing: 'Wohnen',
  insurance: 'Versicherung',
  groceries: 'Lebensmittel',
  health: 'Gesundheit',
  mobility: 'Mobilität',
  communication: 'Kommunikation',
  children: 'Kinder',
  subscriptions: 'Abos',
  leisure: 'Freizeit',
  vacation: 'Urlaub',
  pocket_money: 'Taschengeld',
  reserves: 'Rücklagen',
  debt_repayment: 'Tilgung',
  investment: 'Investition',
  legal: 'Rechtliches',
  work: 'Beruf',
}

/**
 * **Nur ein Vorschlag.** Gespiegelt aus `BLOCK_SUGGESTION` im Backend.
 *
 * Stellt das Budgetfeld im Formular auf den naheliegenden Wert, mehr nicht —
 * der Nutzer entscheidet. Bewusst keine Datenlogik: ob Sprit Fixkosten oder
 * Wunsch ist, hängt vom Haushalt ab.
 */
export const BLOCK_SUGGESTION: Record<Category, Block> = {
  income: 'income',
  housing: 'needs',
  insurance: 'needs',
  groceries: 'needs',
  health: 'needs',
  mobility: 'needs',
  communication: 'needs',
  children: 'needs',
  subscriptions: 'wants',
  leisure: 'wants',
  vacation: 'wants',
  pocket_money: 'wants',
  reserves: 'savings',
  debt_repayment: 'savings',
  // Investitionen sind ganz normale Wünsche — keine Sonderbehandlung,
  // keine eigene Gruppe, keine eigene Quote.
  investment: 'wants',
  legal: 'needs',
  // Werbungskosten: beruflich veranlasst, aus privatem Geld bezahlt.
  work: 'needs',
}

export type Rhythm = 'monthly' | 'quarterly' | 'biannual' | 'annual'

export const RHYTHM_LABEL: Record<Rhythm, string> = {
  monthly: 'monatlich',
  quarterly: 'quartalsweise',
  biannual: 'halbjährlich',
  annual: 'jährlich',
}

/** Abstand in Monaten — spiegelt `Rhythm.interval`. */
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
 * In welchen Monaten fällt das an? Spiegelt `Commitment.is_due_in()`.
 *
 * Wichtig: der Rhythmus läuft über den Jahreswechsel weiter. Quartalsweise ab
 * Juli heißt Jan, Apr, Jul, Okt — nicht nur Jul und Okt. Deshalb wird jeder
 * Monat einzeln geprüft statt vom Startmonat hochgezählt.
 *
 * `%` liefert in JavaScript bei negativen Zahlen ein negatives Ergebnis
 * (anders als in Python) — der Aufschlag von `interval` gleicht das aus.
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

/** Tag 0 des Folgemonats ist der letzte Tag des gesuchten — inklusive Schaltjahr. */
export function daysInMonth(year: number, month: number): number {
  return new Date(year, month, 0).getDate()
}

/**
 * Der Tag, an dem es tatsächlich fällig wird.
 *
 * Ein Fälligkeitstag 31 existiert nur in sieben Monaten. Statt den Posten
 * ausfallen zu lassen, rutscht er auf den letzten Tag des Monats — im Februar
 * also auf den 28. bzw. 29.
 *
 * Dieselbe Regel greift im Backend beim Erzeugen der Posten
 * (`Commitment.effective_due_day`).
 */
export function effectiveDueDay(
  dueDay: number,
  year: number,
  month: number
): number {
  return Math.min(dueDay, daysInMonth(year, month))
}

/** Ab dem 29. kann der Tag verrutschen — nur dann muss man es erklären. */
export const DUE_DAY_MAY_SHIFT = 29

/**
 * Was das Ding **pro Monat** kostet.
 *
 * Nötig, weil man einen Jahresbetrag nicht mit Monatsbeträgen addieren darf:
 * 108,40 € jährlich sind 9,03 € im Monat, nicht 108,40 €. Ohne diese
 * Umrechnung wäre jede Budgetsumme falsch.
 */
export function monthlyEquivalent(amount: string, rhythm: Rhythm): number {
  return Number(amount) / RHYTHM_INTERVAL[rhythm]
}

export type CommitmentType =
  | 'contract'
  | 'savings_goal'
  | 'debt'
  /** Wiederkehrender Betrag ohne Vertrag: Sprit, Lebensmittel, Taschengeld. */
  | 'budget'

/** Spiegel von `Commitment` — eine Tabelle für alle drei Typen. */
export type Commitment = {
  id: string
  ownerId?: string
  type: CommitmentType
  name: string
  /** Als String gehalten, damit beim Tippen nichts gerundet wird. */
  amount: string
  category: Category
  block: Block
  /** null = privat. Gesetzt = erzeugte Posten wandern in diesen Haushalt. */
  householdId: string | null
  rhythm: Rhythm
  /**
   * Wann es das erste Mal fällig wird — Tag, Monat und Jahr.
   * Bei nicht-monatlichem Rhythmus Pflicht. Der Monat gibt den Takt vor,
   * das Jahr den Beginn.
   */
  firstDueDate: string | null
  dueDay: number
  active: boolean
  /** nur bei savings_goal */
  targetAmount: string | null
  targetDate: string | null
  /** nur bei debt */
  remainingDebt: string | null
  /** Wird in die erzeugten Posten kopiert, dort je Monat überschreibbar. */
  paymentMethod: PaymentMethod | null
  /** Von welchem Konto es abgeht. null = Standardkonto. */
  accountId: string | null
}

/** Der Monat, in dem der Takt beginnt — steckt im Startdatum. */
export function firstMonthOf(commitment: {
  firstDueDate: string | null
}): number | null {
  return commitment.firstDueDate
    ? Number(commitment.firstDueDate.slice(5, 7))
    : null
}

/**
 * Nur **Zahlungskonten** — Dinge mit einem Stand, der sich aus Buchungen
 * ergibt. Ein Depot gehört bewusst nicht dazu: sein Wert kommt von Kursen.
 * Im Buch steht deshalb nur das Verrechnungskonto.
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
  /** Als String gehalten, damit beim Tippen nichts gerundet wird. */
  openingBalance: string
  /** Stichtag des Anfangsbestands — ohne ihn wäre kein Stand berechenbar. */
  openingDate: string
  /** Höchstens eines je Person. Wird bei der Schnelleingabe vorausgewählt. */
  isDefault: boolean
  active: boolean
  externalRef: string | null
}

/**
 * Eine Buchung im Haushaltsbuch.
 *
 * Kontowirkung und Budgetwirkung sind unabhängig: `counterAccountId` bestimmt
 * die Stände, `positionId` das Budget. Geld aufs Tagesgeld legen ist beides
 * zugleich — eine Umbuchung, die die Sparquote erfüllt.
 */
export type Transaction = {
  id: string
  ownerId?: string
  accountId: string
  /** Gesetzt = Umbuchung auf ein eigenes Konto. */
  counterAccountId: string | null
  occurredOn: string
  /** Immer positiv — die Richtung kommt aus `block`. */
  amount: string
  note: string | null
  /** Nur bei einer reinen Umbuchung leer. */
  category: Category | null
  block: Block | null
  positionId: string | null
  /** Vom Abhaken erzeugt — das Enthaken nimmt genau diese wieder mit. */
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

/** Spiegel von `Plan` — gehört immer einer Person, nie einem Haushalt. */
export type Plan = {
  year: number
  month: number
  status: 'draft' | 'confirmed'
  /** Quoten in Prozent. Richtwerte, keine Regel. */
  targetNeeds: string
  targetWants: string
  targetSavings: string
  /** Wieviel Prozent der Einnahmen bewusst unverplant bleiben. */
  bufferPercent: string
}

/** Spiegel von `PlanPosition` — ein Posten in genau einem Monatsplan. */
export type PlanPosition = {
  id: string
  label: string
  amountPlanned: string
  /** Trägt der Nutzer im Lauf des Monats nach. */
  amountActual: string | null
  category: Category
  /** Beim Anlegen eingefroren — spätere Änderungen wirken nicht rückwirkend. */
  block: Block
  dueDay: number
  /** Vom Vertrag kopiert, je Monat überschreibbar. null = Standardkonto. */
  accountId: string | null
  paymentMethod: PaymentMethod | null
  /**
   * Ein Budget statt einer Einzelzahlung — Lebensmittel, Sprit, Taschengeld.
   *
   * Wird **nicht abgehakt**: solche Posten füllen sich über den Monat aus
   * einzelnen Buchungen. Statt eines Hakens zeigt die Zeile den Füllstand.
   */
  isBudget: boolean
  /** NULL = privat. Gesetzt = wandert in diesen Haushaltsplan. */
  householdId: string | null
  /** Leer bei Einmal-Posten, die nicht aus einer Verpflichtung stammen. */
  commitmentId: string | null
  /** Wann abgehakt wurde. NULL = steht noch offen. */
  paidAt: string | null
}

export type Me = {
  id: string
  email: string
  firstName: string
  lastName: string
}

export type Role = 'owner' | 'member'

export type Member = {
  userId: string
  firstName: string
  lastName: string
  email: string
  role: Role
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
 * Eine offene Einladung an die eigene Adresse — der Posteingang.
 *
 * Damit braucht es weder E-Mail-Versand noch einen weitergereichten Link: wer
 * sich mit der eingeladenen Adresse anmeldet, findet die Einladung im Portal.
 */
export type MyInvitation = {
  token: string
  householdId: string
  householdName: string
  invitedBy: string
  expiresAt: string
}

export const PLAN_STATUS_LABEL: Record<Plan['status'], string> = {
  draft: 'Entwurf',
  confirmed: 'Bestätigt',
}

/**
 * Zeile in der Planübersicht — die Summen kommen fertig vom Backend, damit
 * die Übersicht nicht alle Posten aller Monate laden muss.
 *
 * Beträge kommen als String, weil Decimal in JSON so übertragen wird.
 */
export type PlanSummary = Plan & {
  income: string
  /**
   * Einnahmen minus Puffer — die Grundlage, auf die sich die Quoten beziehen.
   * **Nicht** dasselbe wie „Verplanbar": das ist der noch freie Rest davon.
   */
  budget: string
  /** Verplant je Budget. */
  spent: Record<'needs' | 'wants' | 'savings', string>
  /** Summe der Posten, die noch nicht abgehakt sind. */
  unpaid: string
  /** Haushalte, in die Posten dieses Plans einfließen. Leer = rein privat. */
  householdIds: string[]
}

/** Ein Plan samt seinen Posten. */
export type PlanDetail = PlanSummary & {
  id: string
  confirmedAt: string | null
  positions: PlanPosition[]
}

/**
 * Der gemeinsame Plan. Zusammengesetzt, nicht gespeichert — deshalb ohne `id`
 * und ohne `confirmedAt`: es gibt kein Objekt, das man bestätigen könnte.
 */
export type HouseholdPlanDetail = PlanSummary & {
  householdId: string
  householdName: string
  positions: HouseholdPosition[]
}

/** Wie ein Posten, plus die Person dahinter — der Sinn der gemeinsamen Sicht. */
export type HouseholdPosition = PlanPosition & {
  ownerId: string
  ownerName: string
}

/** Was vom Budget noch nicht auf die drei Budgets verteilt ist. */
export function unallocated(plan: PlanSummary): number {
  const spent =
    Number(plan.spent.needs) + Number(plan.spent.wants) + Number(plan.spent.savings)
  return Number(plan.budget) - spent
}

/** Ist der Posten schon abgehakt? */
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
