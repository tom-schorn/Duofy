import { Metric } from '@/components/Metric'
import { QueryState } from '@/components/QueryState'
import {
  OWN_SCOPE,
  euro,
  stillDue,
  type PlanPosition,
  type Transaction,
  type BookScope,
} from '@/lib/domain'
import { useAccounts, useTransactions } from '@/lib/queries'

/**
 * The figures of the book — what actually moved.
 *
 * Deliberately **not** the same cards as in the plan. There the figure is what is
 * left to allocate, which is a pure planning value: budget minus what has been
 * distributed onto positions. A booking without a position never appears in it, and
 * that is intended: it is the number one plans with at the start of the month, and
 * it must not wander all month long.
 *
 * So the book shows the other side. The first two cards are monthly figures from
 * the same list shown below them — otherwise the cards would disagree with what one
 * can see.
 *
 * ## "Available" is deliberately **not** a monthly figure
 *
 * It is the balance of the accounts that count as spendable. A monthly calculation
 * cannot hit the real balance at all: income for August arrives at the end of July,
 * and the July expenses were paid from the same money. Adding August income minus
 * August expenses would give more than is actually there, because July already
 * consumed part of it.
 *
 * So this card claims nothing, it looks. That is the number telling you whether you
 * can afford something today.
 *
 * Which accounts count is decided by `countsAsAvailable` on the account: current
 * accounts and wallets yes, savings and securities no — that money is earmarked.
 *
 * ## "Free after deductions"
 *
 * Available minus what still has to go out this month. Only that number answers the
 * everyday question: can I afford this now without a direct debit bouncing at the
 * end of the month.
 *
 * It may go negative, and that is not a bug but the warning.
 */

type Props = {
  year: number
  month: number
  /** From the plan — for what still has to go out this month. */
  positions: PlanPosition[]
  /** Whose book: your own, one person, or the household. */
  scope?: BookScope
}

export function BookMetrics({
  year,
  month,
  positions,
  scope = OWN_SCOPE,
}: Props) {
  const transactions = useTransactions(year, month, scope)
  const accounts = useAccounts(scope)

  const open = (accounts.data ?? []).filter((account) => account.active)
  // Accounts whose money no longer counts as spendable. A transfer to one of them
  // is the only transfer that costs anything in the book.
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

  // Not income - spending: see above, that would be a monthly figure and would sit
  // above what is really on the accounts.
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
