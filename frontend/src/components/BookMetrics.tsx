import { Metric } from '@/components/Metric'
import { QueryState } from '@/components/QueryState'
import {
  euro,
  stillDue,
  type PlanPosition,
  type Transaction,
} from '@/lib/domain'
import { useAccounts, useTransactions } from '@/lib/queries'

/**
 * Die Kennzahlen des Buchs — was wirklich geflossen ist.
 *
 * Bewusst **nicht** dieselben Karten wie im Plan. Dort steht „Verplanbar", und
 * das ist ein reiner Planwert: Budget minus dem, was auf Posten verteilt ist.
 * Eine Buchung ohne Posten — der Kiosk, der Media Markt — taucht darin nie
 * auf, und das soll auch so bleiben: mit dieser Zahl plant man am
 * Monatsanfang, sie darf nicht den ganzen Monat über wandern.
 *
 * Also zeigt das Buch die andere Seite. Die ersten beiden Karten sind
 * Monatszahlen aus derselben Liste, die darunter steht — sonst stimmten die
 * Karten nicht mit dem überein, was man sieht.
 *
 * ## „Verfügbar" ist bewusst **keine** Monatszahl
 *
 * Es ist der Stand der Konten, die als verfügbar gelten. Eine Monatsrechnung
 * kann den echten Kontostand nämlich gar nicht treffen: die August-Einnahmen
 * kommen Ende Juli, und aus demselben Geld wurden noch die Juli-Ausgaben
 * bezahlt. Zählte man Einnahmen minus Ausgaben des Augusts, käme mehr heraus,
 * als tatsächlich da ist — der Juli hat einen Teil schon verbraucht.
 *
 * Deshalb behauptet diese Karte nichts, sie schaut nach. Das ist die Zahl, an
 * der man ablesen kann, ob man sich heute etwas leisten kann.
 *
 * Welche Konten mitzählen, entscheidet `countsAsAvailable` am Konto: Giro und
 * PayPal ja, Tagesgeld und Depot nein — dort liegt Zweckgebundenes.
 *
 * ## „Frei nach Abzug"
 *
 * Verfügbar minus dem, was diesen Monat noch abgeht. Erst diese Zahl
 * beantwortet die Frage, die man im Alltag hat: kann ich mir das jetzt
 * leisten, ohne dass Ende des Monats eine Lastschrift platzt.
 *
 * Sie darf negativ werden, und das ist kein Fehler, sondern die Warnung.
 */

type Props = {
  year: number
  month: number
  /** Aus dem Plan — für das, was diesen Monat noch abgeht. */
  positions: PlanPosition[]
}

export function BookMetrics({ year, month, positions }: Props) {
  const transactions = useTransactions(year, month)
  const accounts = useAccounts()

  const open = (accounts.data ?? []).filter((account) => account.active)
  // Konten, deren Guthaben nicht mehr als verfügbar gilt. Eine Umbuchung
  // dorthin ist die einzige Umbuchung, die im Buch etwas kostet.
  const locked = new Set(
    open.filter((account) => !account.countsAsAvailable).map((a) => a.id)
  )
  const rows = transactions.data ?? []

  const sum = (list: Transaction[]) =>
    list.reduce((total, row) => total + Number(row.amount), 0)

  const isTransfer = (row: Transaction) => row.counterAccountId !== null
  const putAside = (row: Transaction) =>
    row.counterAccountId !== null && locked.has(row.counterAccountId)

  const income = sum(
    rows.filter((row) => !isTransfer(row) && row.block === 'income')
  )
  const spending = sum(
    rows.filter(
      (row) => putAside(row) || (!isTransfer(row) && row.block !== 'income')
    )
  )

  // Nicht income - spending: siehe oben, das wäre eine Monatszahl und läge
  // über dem, was wirklich auf den Konten liegt.
  const free = open.filter((account) => account.countsAsAvailable)
  const available = free.reduce(
    (total, account) => total + Number(account.balance ?? 0),
    0
  )

  const due = positions.reduce((total, position) => total + stillDue(position), 0)
  const leftover = available - due

  return (
    <QueryState
      isPending={transactions.isPending || accounts.isPending}
      error={transactions.error ?? accounts.error}
      rows={1}
    >
      <section className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <Metric label="Eingegangen" value={income} hint="schon angekommen" />
        <Metric
          label="Ausgegeben"
          value={spending}
          hint="samt Weggespartem"
        />
        {/* Die Kopfzahl des Buchs — das Gegenstück zu „Verplanbar" im Plan.
            Wird sie negativ, ist mehr rausgegangen als hereingekommen. */}
        <Metric
          label="Verfügbar"
          value={available}
          hint={
            free.length === 1
              ? `Stand von ${free[0].name}`
              : `Stand von ${free.length} Konten`
          }
          tone={available < 0 ? 'over' : 'neutral'}
        />
        <Metric
          label="Frei nach Abzug"
          value={leftover}
          hint={`${euro.format(due)} stehen noch aus`}
          strong
          tone={leftover < 0 ? 'over' : 'neutral'}
        />
      </section>
    </QueryState>
  )
}
