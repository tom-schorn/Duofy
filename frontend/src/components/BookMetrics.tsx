import { Metric } from '@/components/Metric'
import { QueryState } from '@/components/QueryState'
import type { Transaction } from '@/lib/domain'
import { useTransactions } from '@/lib/queries'

/**
 * Die Kennzahlen des Buchs — was wirklich geflossen ist.
 *
 * Bewusst **nicht** dieselben Karten wie im Plan. Dort steht „Verplanbar", und
 * das ist ein reiner Planwert: Budget minus dem, was auf Posten verteilt ist.
 * Eine Buchung ohne Posten — der Kiosk, der Media Markt — taucht darin nie
 * auf, und das soll auch so bleiben: mit dieser Zahl plant man am
 * Monatsanfang, sie darf nicht den ganzen Monat über wandern.
 *
 * Also zeigt das Buch die andere Seite. „Verfügbar" ist dabei die Zahl, um die
 * es geht: was von dem Geld, das diesen Monat wirklich angekommen ist, noch
 * nicht ausgegeben wurde.
 *
 * Eine Einnahme, die noch aussteht, zählt **nicht** mit — anders als im Plan,
 * wo sie das Budget von Anfang an mitträgt. Genau das ist der Unterschied
 * zwischen den beiden Kartensätzen: der Plan rechnet mit dem ganzen Monat, das
 * Buch nur mit dem, was schon passiert ist.
 *
 * Gerechnet wird über **dieselbe Liste**, die darunter steht. Sonst stimmten
 * die Karten nicht mit dem überein, was man sieht.
 */

type Props = {
  year: number
  month: number
}

/** Umbuchungen sind weder Einnahme noch Ausgabe — sie schieben nur. */
function isTransfer(transaction: Transaction): boolean {
  return transaction.counterAccountId !== null
}

export function BookMetrics({ year, month }: Props) {
  const transactions = useTransactions(year, month)
  const rows = (transactions.data ?? []).filter((row) => !isTransfer(row))

  const sum = (list: Transaction[]) =>
    list.reduce((total, row) => total + Number(row.amount), 0)

  const income = sum(rows.filter((row) => row.block === 'income'))
  const spending = sum(rows.filter((row) => row.block !== 'income'))
  const available = income - spending

  return (
    <QueryState
      isPending={transactions.isPending}
      error={transactions.error}
      rows={1}
    >
      <section className="grid gap-3 sm:grid-cols-3">
        <Metric label="Eingegangen" value={income} hint="schon angekommen" />
        <Metric label="Ausgegeben" value={spending} hint="ohne Umbuchungen" />
        {/* Die Kopfzahl des Buchs — das Gegenstück zu „Verplanbar" im Plan.
            Wird sie negativ, ist mehr rausgegangen als hereingekommen. */}
        <Metric
          label="Verfügbar"
          value={available}
          hint="eingegangen minus ausgegeben"
          strong
          tone={available < 0 ? 'over' : 'neutral'}
        />
      </section>
    </QueryState>
  )
}
