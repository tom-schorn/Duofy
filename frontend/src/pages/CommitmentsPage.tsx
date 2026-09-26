import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Plus } from 'lucide-react'

import { CommitmentDialog } from '@/components/CommitmentDialog'
import { QueryState } from '@/components/QueryState'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { ListRow } from '@/components/ListRow'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { useActiveMember } from '@/hooks/use-active-member'
import {
  useCommitments,
  useDeleteCommitment,
  useHouseholds,
  useSaveCommitment,
} from '@/lib/queries'
import {
  BUDGET_DOT,
  budgetLabel,
  BUDGET_ORDER,
  categoryLabel,
  intervalLabel,
  atLeast,
  nextDueDates,
  dueDateLabel,
  dueDayOf,
  euro,
  monthlyEquivalent,
  COMMITMENT_STATUSES,
  endMonthLabel,
  hasEnded,
  type Commitment,
  type CommitmentStatus,
} from '@/lib/domain'
import { i18n, locale } from '@/lib/i18n'

/**
 * Every commitment on one page, grouped by budget.
 *
 * Savings plans and loans are commitments too — in the model it is one table
 * (`Commitment`), and `type` only says whether the thing has an end. Grouping is by
 * **budget**, because that is the axis that matters when planning: a commitment can
 * sit in any budget.
 *
 * The same grouping as in the plan — one structure, two pages.
 */

/**
 * Monthly needs no addition — the rest shows the next three due dates from today.
 * A month list would only cover one year, and with an interval that does not divide
 * 12 (or a start in the future) that list is short, shifting or empty.
 */
function intervalText(commitment: Commitment) {
  const now = new Date()
  const dates = nextDueDates(
    commitment.intervalMonths,
    commitment.firstDueDate,
    { year: now.getFullYear(), month: now.getMonth() + 1 },
    3
  )
  if (dates.length === 0) return intervalLabel(commitment.intervalMonths)
  return `${intervalLabel(commitment.intervalMonths)} · ${i18n.t('commitments.nextDue', {
    dates: dates.map(dueDateLabel).join(', '),
  })}`
}

/** What follows from the type — a target, nothing else. */
function typeDetail(commitment: Commitment) {
  if (commitment.type === 'savings_goal' && commitment.targetAmount) {
    const date = commitment.targetDate
      ? new Date(commitment.targetDate).toLocaleDateString(locale(), {
          month: '2-digit',
          year: 'numeric',
        })
      : null
    const target = euro.format(Number(commitment.targetAmount))
    return date
      ? i18n.t('commitments.targetUntil', { target, date })
      : i18n.t('commitments.target', { target })
  }
  return null
}

export function CommitmentsPage() {
  const { t } = useTranslation()
  // `?member=` shows somebody else's commitments — see `MemberSwitcher`. They are
  // private by default: whoever shares nothing appears in no switcher, and the
  // endpoint refuses the list anyway.
  const active = useActiveMember()
  const [status, setStatus] = useState<CommitmentStatus>('active')
  const commitments = useCommitments(active.id, status)
  const mayEdit = atLeast(active.levelFor('commitments'), 'edit')
  // Your own you may always delete — as long as it is unused; another's needs `delete`.
  const mayDelete = active.member === null || atLeast(active.levelFor('commitments'), 'delete')
  const households = useHouseholds()
  const save = useSaveCommitment()
  const remove = useDeleteCommitment()

  const [editing, setEditing] = useState<Commitment | null>(null)
  const [dialogOpen, setDialogOpen] = useState(false)
  // An error from the last try must not greet the next opening.
  const resetSave = save.reset
  useEffect(() => {
    if (dialogOpen) resetSave()
  }, [dialogOpen, resetSave])
  const [pendingDelete, setPendingDelete] = useState<Commitment | null>(null)
  // After a delete the row is gone; the focus goes to the page heading (rule 13).
  const heading = useRef<HTMLHeadingElement>(null)
  const deleted = useRef(false)

  const householdNames = Object.fromEntries(
    (households.data ?? []).map((household) => [household.id, household.name])
  )
  const rowsAll = commitments.data ?? []

  const groups = BUDGET_ORDER.map((budget) => {
    const rows = rowsAll
      .filter((commitment) => commitment.budget === budget)
      .sort(
        (a, b) =>
          monthlyEquivalent(b.amount, b.intervalMonths) -
          monthlyEquivalent(a.amount, a.intervalMonths)
      )
    // Ended ones do not count — they generate no positions.
    const total = rows
      .filter((commitment) => !hasEnded(commitment.endsOn))
      .reduce(
        (sum, commitment) =>
          sum + monthlyEquivalent(commitment.amount, commitment.intervalMonths),
        0
      )
    return { budget, rows, total }
  }).filter((group) => group.rows.length > 0)

  function handleAdd() {
    deleted.current = false
    setEditing(null)
    setDialogOpen(true)
  }

  function handleEdit(commitment: Commitment) {
    deleted.current = false
    setEditing(commitment)
    setDialogOpen(true)
  }

  function handleDelete() {
    if (!pendingDelete) return
    // Positions already generated stay — the model sets `commitment_id` to NULL
    // (ON DELETE SET NULL).
    deleted.current = true
    remove.mutate(pendingDelete.id)
    setPendingDelete(null)
    setDialogOpen(false)
  }

  return (
    <div className="flex flex-col gap-8">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-col gap-2">
          <h1
            ref={heading}
            tabIndex={-1}
            className="font-heading text-3xl font-semibold outline-none"
          >
            {t('commitments.title')}
          </h1>
          <p className="text-muted-foreground">
            {active.member === null
              ? t('commitments.lead')
              : mayEdit
                ? t('commitments.leadMemberEdit', { name: active.member.firstName })
                : t('commitments.leadMemberView', { name: active.member.firstName })}
          </p>
        </div>
        {mayEdit && (
          <Button onClick={handleAdd}>
            <Plus className="size-4" />
            {t('commitments.create')}
          </Button>
        )}
      </header>

      <div className="flex items-center gap-2">
        <span id="commitment-status-label" className="text-muted-foreground text-sm">
          {t('commitments.filterLabel')}
        </span>
        <Select value={status} onValueChange={(value) => setStatus(value as CommitmentStatus)}>
          <SelectTrigger className="w-40" aria-labelledby="commitment-status-label">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {COMMITMENT_STATUSES.map((option) => (
              <SelectItem key={option} value={option}>
                {t(`commitments.status.${option}`)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <QueryState isPending={commitments.isPending} error={commitments.error}>
      {groups.length === 0 ? (
        <p className="text-muted-foreground border-border rounded-lg border border-dashed p-10 text-center text-sm">
          {status === 'ended' ? t('commitments.emptyEnded') : t('commitments.empty')}
        </p>
      ) : (
        <div className="flex flex-col gap-8">
          {groups.map((group) => (
            <section key={group.budget} className="flex flex-col gap-3">
              <div className="flex items-baseline justify-between gap-4 border-b pb-2">
                <h2 className="flex items-center gap-2 text-sm font-semibold tracking-wide uppercase">
                  <span
                    className={`size-2.5 rounded-sm ${BUDGET_DOT[group.budget]}`}
                  />
                  {budgetLabel(group.budget)}
                </h2>
                <span className="text-muted-foreground text-sm tabular-nums">
                  {euro.format(group.total)}
                  <span className="ml-1 text-xs">{t('commitments.perMonth')}</span>
                </span>
              </div>

              <ul className="flex flex-col">
                {group.rows.map((commitment) => {
                  const detail = typeDetail(commitment)
                  return (
                    <ListRow
                      key={commitment.id}
                      onOpen={mayEdit ? () => handleEdit(commitment) : undefined}
                      className={hasEnded(commitment.endsOn) ? 'opacity-55' : undefined}
                      trailing={
                        <span className="font-medium">
                          {euro.format(Number(commitment.amount))}
                        </span>
                      }
                    >
                      <span className="flex items-center gap-2 font-medium">
                        {commitment.name}
                        {commitment.endsOn !== null && hasEnded(commitment.endsOn) && (
                          <Badge variant="outline" className="font-normal">
                            {t('commitments.endedSince', {
                              month: endMonthLabel(commitment.endsOn),
                            })}
                          </Badge>
                        )}
                      </span>
                      <span className="text-muted-foreground truncate text-xs">
                        {categoryLabel(commitment.category)} ·{' '}
                        {intervalText(commitment)} ·{' '}
                        {t('common.dueDay', { day: dueDayOf(commitment.firstDueDate) })}
                        {commitment.householdId
                          ? ` · ${householdNames[commitment.householdId] ?? t('plans.household')}`
                          : ` · ${t('commitments.private')}`}
                        {commitment.isLimit ? ` · ${t('budget.limit')}` : ''}
                        {detail ? ` · ${detail}` : ''}
                      </span>
                    </ListRow>
                  )
                })}
              </ul>
            </section>
          ))}
        </div>
      )}
      </QueryState>

      <CommitmentDialog
        commitment={editing}
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        pending={save.isPending}
        error={save.error}
        onSave={(saved) =>
          save.mutate(saved, { onSuccess: () => setDialogOpen(false) })
        }
        onDelete={
          mayDelete && editing?.deletable ? (commitment) => setPendingDelete(commitment) : null
        }
        returnFocus={() => (deleted.current ? heading.current : null)}
      />

      <AlertDialog
        open={pendingDelete !== null}
        onOpenChange={(open) => !open && setPendingDelete(null)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle className="font-heading">
              {t('commitments.deleteTitle', { name: pendingDelete?.name })}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {t('commitments.deleteText')}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t('common.cancel')}</AlertDialogCancel>
            <AlertDialogAction onClick={handleDelete}>
              {t('common.delete')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}
