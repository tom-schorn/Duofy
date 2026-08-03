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
 * Also zeigt das Buch die andere Seite. „Ungeplant" ist dabei die
 * interessanteste Zahl: Ausgaben, die zu keinem Posten gehören. Sie sind der
 * Unterschied zwischen dem, was man sich vorgenommen hat, und dem, was
 * passiert ist.
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
  const spending = rows.filter((row) => row.block !== 'income')
  const unplanned = spending.filter((row) => row.positionId === null)

  return (
    <QueryState
      isPending={transactions.isPending}
      error={transactions.error}
      rows={1}
    >
      <section className="grid gap-3 sm:grid-cols-3">
        <Metric label="Eingegangen" value={income} hint="tatsächlich da" />
        <Metric
          label="Ausgegeben"
          value={sum(spending)}
          hint="ohne Umbuchungen"
        />
        <Metric
          label="Ungeplant"
          value={sum(unplanned)}
          hint={
            unplanned.length === 1
              ? '1 Buchung ohne Posten'
              : `${unplanned.length} Buchungen ohne Posten`
          }
          tone={sum(unplanned) > 0 ? 'over' : 'neutral'}
        />
      </section>
    </QueryState>
  )
}
