import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ArrowRight } from 'lucide-react'
import { Link } from 'react-router'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { BookingDialog } from '@/components/BookingDialog'
import { ListRow } from '@/components/ListRow'
import { EmptyState } from '@/components/EmptyState'
import { QueryState } from '@/components/QueryState'
import { scopeSearch, useActiveMember } from '@/hooks/use-active-member'
import {
  OWN_SCOPE,
  categoryLabel,
  euro,
  monthShort,
  type Account,
  type PlanPosition,
  type Transaction,
  type BookScope,
} from '@/lib/domain'
import {
  useAccounts,
  useDeleteTransaction,
  useSaveTransaction,
  useTransactions,
} from '@/lib/queries'

/**
 * The book of one plan month — what actually happened, as one plain list by date.
 *
 * It is a tab of the plan page (#241). The plan says how the month was meant to go,
 * the book says how it went. They are connected at exactly one point: a booking
 * **can** be assigned to a position, but it does not have to be. An unplanned
 * purchase belongs in the book all the same, and "zuordnen" is where it can still
 * be given one.
 *
 * Adding a booking is a button on the plan page (`AddBookingButton`), not a form in
 * here: one does it from any tab.
 */

/** Everything the plan month holds, or only what hangs on no position. */
export type BookFilter = 'all' | 'unplanned'

type Props = {
  positions: PlanPosition[]
  year: number
  month: number
  /** Whose book: your own, one person, or the household. */
  scope?: BookScope
  /**
   * View only. False at the "may change" level: whoever may tick off somebody else
   * position may also book in their book — ticking off creates exactly such a
   * booking.
   */
  readOnly?: boolean
  filter: BookFilter
  onFilterChange: (filter: BookFilter) => void
}

export function MonthBook({
  positions,
  year,
  month,
  scope = OWN_SCOPE,
  readOnly = false,
  filter,
  onFilterChange,
}: Props) {
  const { t } = useTranslation()
  const transactions = useTransactions(year, month, scope)
  const accounts = useAccounts(scope).data ?? []
  const active = useActiveMember()
  const saveEdit = useSaveTransaction(year, month, scope)
  const remove = useDeleteTransaction(year, month, scope)
  const [editing, setEditing] = useState<{
    transaction: Transaction
    startWord: string | null
  } | null>(null)
  // After a delete the row is gone; the focus goes to the (hidden) list heading.
  const heading = useRef<HTMLHeadingElement>(null)
  const deleted = useRef(false)

  const usable = accounts.filter((account) => account.active)

  if (usable.length === 0 && !readOnly) {
    return (
      <EmptyState
        action={
          <Button asChild>
            {/* The accounts page knows no household: only the person travels. */}
            <Link
              to={{
                pathname: '/accounts',
                search: scopeSearch({ id: active.id, householdId: null }),
              }}
            >
              {t('accounts.create')}
            </Link>
          </Button>
        }
      >
        {t('monthBook.noAccountText')}
      </EmptyState>
    )
  }

  // By the day the money moved, oldest first — the plain list "nach Datum". The
  // server sends newest first; a stable sort keeps its order within a day.
  const all = [...(transactions.data ?? [])].sort((a, b) =>
    a.occurredOn < b.occurredOn ? -1 : a.occurredOn > b.occurredOn ? 1 : 0
  )
  const unplanned = all.filter((transaction) => transaction.unplanned)
  const shown = filter === 'unplanned' ? unplanned : all

  function open(transaction: Transaction, startWord: string | null) {
    saveEdit.reset()
    deleted.current = false
    setEditing({ transaction, startWord })
  }

  return (
    <section className="flex flex-col gap-4">
      <h2 ref={heading} tabIndex={-1} className="sr-only">
        {t('monthBook.title')}
      </h2>

      <div className="flex flex-wrap items-center gap-2">
        <FilterChip
          pressed={filter === 'all'}
          onClick={() => onFilterChange('all')}
        >
          {t('monthBook.filterAll')}
        </FilterChip>
        <FilterChip
          pressed={filter === 'unplanned'}
          onClick={() => onFilterChange('unplanned')}
        >
          {t('monthBook.filterUnplanned', { count: unplanned.length })}
        </FilterChip>
      </div>

      {/* Said in words, so nobody wonders where the rest of the book went. */}
      {filter === 'unplanned' && (
        <p role="status" className="text-muted-foreground flex flex-wrap items-center gap-2 text-sm">
          {t('monthBook.onlyUnplanned')}
          <Button
            type="button"
            variant="link"
            size="sm"
            className="h-auto p-0"
            onClick={() => onFilterChange('all')}
          >
            {t('monthBook.clearFilter')}
          </Button>
        </p>
      )}

      <QueryState
        isPending={transactions.isPending}
        error={transactions.error}
        onRetry={() => void transactions.refetch()}
        rows={3}
      >
        {shown.length === 0 ? (
          <EmptyState>
            {t(filter === 'unplanned' ? 'monthBook.noUnplanned' : 'monthBook.empty')}
          </EmptyState>
        ) : (
          <ul className="flex flex-col">
            {shown.map((transaction) => {
              // A carry-over is changed in the flow tab, where it is used.
              const editable = !readOnly && transaction.kind !== 'carry_over'
              return (
                <Row
                  key={transaction.id}
                  transaction={transaction}
                  accounts={accounts}
                  positions={positions}
                  onEdit={editable ? () => open(transaction, null) : null}
                  onAssign={
                    editable && transaction.unplanned
                      ? () => open(transaction, 'position')
                      : null
                  }
                />
              )
            })}
          </ul>
        )}
      </QueryState>

      {editing !== null && (
        <BookingDialog
          key={editing.transaction.id}
          accounts={accounts}
          positions={[{ year, month, positions }]}
          viewedMonth={{ year, month }}
          onClose={() => setEditing(null)}
          start={{
            kind: 'edit',
            transaction: editing.transaction,
            startWord: editing.startWord,
            onSave: (draft) => saveEdit.mutate(draft, { onSuccess: () => setEditing(null) }),
            pending: saveEdit.isPending,
            error: saveEdit.error,
            onDelete: readOnly
              ? null
              : () => {
                  deleted.current = true
                  remove(editing.transaction)
                  setEditing(null)
                },
            returnFocus: () => (deleted.current ? heading.current : null),
          }}
        />
      )}
    </section>
  )
}

function FilterChip({
  pressed,
  onClick,
  children,
}: {
  pressed: boolean
  onClick: () => void
  children: React.ReactNode
}) {
  return (
    <Button
      type="button"
      size="sm"
      variant={pressed ? 'default' : 'outline'}
      aria-pressed={pressed}
      onClick={onClick}
      className="rounded-full"
    >
      {children}
    </Button>
  )
}

function Row({
  transaction,
  accounts,
  positions,
  onEdit,
  onAssign,
}: {
  transaction: Transaction
  accounts: Account[]
  positions: PlanPosition[]
  /** null means read only: the row is not clickable. */
  onEdit: (() => void) | null
  /** Only for a booking on no position; null where it cannot be assigned. */
  onAssign: (() => void) | null
}) {
  const account = accounts.find((item) => item.id === transaction.accountId)
  const counter = accounts.find(
    (item) => item.id === transaction.counterAccountId
  )
  const position = positions.find((item) => item.id === transaction.positionId)
  const isTransfer = transaction.counterAccountId !== null
  const isCarryOver = transaction.kind === 'carry_over'
  const { t } = useTranslation()

  return (
    <ListRow
      onOpen={onEdit ?? undefined}
      trailing={
        <>
          <span className="font-mono font-medium">
            {euro.format(Number(transaction.amount))}
          </span>
          {/* Above the row's stretched click layer, like the tick box of a
              position: it has a job of its own. */}
          {onAssign && (
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={onAssign}
              className="text-primary relative z-10 h-auto px-1 py-0 text-xs font-normal"
            >
              {t('monthBook.assign')}
            </Button>
          )}
        </>
      }
    >
      <span className="grid w-full grid-cols-[auto_1fr] items-center gap-3">
        <span className="text-muted-foreground w-12 text-sm tabular-nums">
          {t('common.dueDay', { day: new Date(transaction.occurredOn).getDate() })}
        </span>

        <span className="flex min-w-0 flex-col gap-0.5">
          <span className="flex flex-wrap items-center gap-2">
            <span className="font-medium">
              {isCarryOver
                ? t('monthBook.carryOverName')
                : (transaction.note ?? position?.label ?? t('monthBook.noNote'))}
            </span>
            {isCarryOver && (
              <Badge variant="outline" className="font-normal">
                {t('monthBook.carryOver')}
              </Badge>
            )}
            {transaction.autoBooked && (
              <Badge variant="outline" className="font-normal">
                {t('monthBook.autoBooked')}
              </Badge>
            )}
            {/* Booked in one month, counting in another: say which (a salary
                paid on the 25th for the month after). */}
            {transaction.countsElsewhere && (
              <Badge variant="secondary" className="font-normal">
                {t('monthBook.forMonth', { month: monthShort(transaction.planMonth) })}
              </Badge>
            )}
          </span>
          <span className="text-muted-foreground truncate text-xs">
            {isCarryOver ? (
              <>
                {account?.name} · {t('monthBook.carryOverHint')}
              </>
            ) : isTransfer ? (
              <>
                {account?.name} <ArrowRight className="inline size-3" />{' '}
                {counter?.name} · {t('budget.transfer')}
                {transaction.ownerName ? ` · ${transaction.ownerName}` : ''}
              </>
            ) : (
              <>
                {account?.name}
                {transaction.category
                  ? ` · ${categoryLabel(transaction.category)}`
                  : ''}
                {position ? ` · ${position.label}` : ''}
                {/* Nur im gemeinsamen Buch gesetzt — dort ist der Name der
                    Unterschied zwischen zwei sonst gleichen Zeilen. */}
                {transaction.ownerName ? ` · ${transaction.ownerName}` : ''}
              </>
            )}
          </span>
        </span>
      </span>
    </ListRow>
  )
}
