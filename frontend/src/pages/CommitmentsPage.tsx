import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { MoreHorizontal, Pencil, Plus, Trash2 } from 'lucide-react'

import { CommitmentDialog } from '@/components/CommitmentDialog'
import { QueryState } from '@/components/QueryState'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
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
import { useActiveMember } from '@/hooks/use-active-member'
import {
  useCommitments,
  useDeleteCommitment,
  useHouseholds,
  useSaveCommitment,
} from '@/lib/queries'
import {
  BLOCK_DOT,
  blockLabel,
  BUDGET_ORDER,
  categoryLabel,
  monthLabel,
  rhythmLabel,
  atLeast,
  dueMonths,
  euro,
  firstMonthOf,
  monthlyEquivalent,
  type Commitment,
} from '@/lib/domain'
import { i18n, locale } from '@/lib/i18n'

/**
 * Every commitment on one page, grouped by block.
 *
 * Savings plans and loans are commitments too — in the model it is one table
 * (`Commitment`), and `type` only says whether the thing has an end. Grouping is by
 * **block**, because that is the axis that matters when planning: a commitment can
 * sit in any block.
 *
 * The same grouping as in the plan — one structure, two pages.
 */

/** Monthly needs no addition — the rest shows when it actually falls due. */
function rhythmText(commitment: Commitment) {
  const months = dueMonths(commitment.rhythm, firstMonthOf(commitment))
  if (months.length === 0) return rhythmLabel(commitment.rhythm)
  const short = months.map((month) => monthLabel(month).slice(0, 3))
  return `${rhythmLabel(commitment.rhythm)} · ${short.join(', ')}`
}

/** What follows from the type — a target or a remaining debt, nothing else. */
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
  if (commitment.type === 'debt' && commitment.remainingDebt) {
    return i18n.t('commitments.remaining', {
      amount: euro.format(Number(commitment.remainingDebt)),
    })
  }
  return null
}

export function CommitmentsPage() {
  const { t } = useTranslation()
  // `?member=` shows somebody else's commitments — see `MemberSwitcher`. They are
  // private by default: whoever shares nothing appears in no switcher, and the
  // endpoint refuses the list anyway.
  const active = useActiveMember()
  const commitments = useCommitments(active.id)
  const mayEdit = atLeast(active.levelFor('commitments'), 'edit')
  const mayDelete = atLeast(active.levelFor('commitments'), 'delete')
  const households = useHouseholds()
  const save = useSaveCommitment()
  const remove = useDeleteCommitment()

  const [editing, setEditing] = useState<Commitment | null>(null)
  const [dialogOpen, setDialogOpen] = useState(false)
  const [pendingDelete, setPendingDelete] = useState<Commitment | null>(null)

  const householdNames = Object.fromEntries(
    (households.data ?? []).map((household) => [household.id, household.name])
  )
  const rowsAll = commitments.data ?? []

  const groups = BUDGET_ORDER.map((block) => {
    const rows = rowsAll
      .filter((commitment) => commitment.block === block)
      .sort(
        (a, b) =>
          monthlyEquivalent(b.amount, b.rhythm) -
          monthlyEquivalent(a.amount, a.rhythm)
      )
    // Inactive ones do not count — they generate no positions.
    const total = rows
      .filter((commitment) => commitment.active)
      .reduce(
        (sum, commitment) =>
          sum + monthlyEquivalent(commitment.amount, commitment.rhythm),
        0
      )
    return { block, rows, total }
  }).filter((group) => group.rows.length > 0)

  function handleAdd() {
    setEditing(null)
    setDialogOpen(true)
  }

  function handleEdit(commitment: Commitment) {
    setEditing(commitment)
    setDialogOpen(true)
  }

  function handleDelete() {
    if (!pendingDelete) return
    // Positions already generated stay — the model sets `commitment_id` to NULL
    // (ON DELETE SET NULL).
    remove.mutate(pendingDelete.id)
    setPendingDelete(null)
  }

  return (
    <div className="flex flex-col gap-8">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-col gap-2">
          <h1 className="font-heading text-3xl font-semibold">{t('commitments.title')}</h1>
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

      <QueryState isPending={commitments.isPending} error={commitments.error}>
      {groups.length === 0 ? (
        <p className="text-muted-foreground border-border rounded-lg border border-dashed p-10 text-center text-sm">
          {t('commitments.empty')}
        </p>
      ) : (
        <div className="flex flex-col gap-8">
          {groups.map((group) => (
            <section key={group.block} className="flex flex-col gap-3">
              <div className="flex items-baseline justify-between gap-4 border-b pb-2">
                <h2 className="flex items-center gap-2 text-sm font-semibold tracking-wide uppercase">
                  <span
                    className={`size-2.5 rounded-sm ${BLOCK_DOT[group.block]}`}
                  />
                  {blockLabel(group.block)}
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
                    <li
                      key={commitment.id}
                      className={`border-border/60 grid grid-cols-[1fr_auto_auto] items-center gap-4 border-b py-2.5 last:border-b-0 ${
                        commitment.active ? '' : 'opacity-55'
                      }`}
                    >
                      <div className="flex min-w-0 flex-col">
                        <span className="flex items-center gap-2 font-medium">
                          {commitment.name}
                          {!commitment.active && (
                            <Badge variant="outline" className="font-normal">
                              {t('commitments.inactive')}
                            </Badge>
                          )}
                        </span>
                        <span className="text-muted-foreground truncate text-xs">
                          {categoryLabel(commitment.category)} ·{' '}
                          {rhythmText(commitment)} ·{' '}
                          {t('common.dueDay', { day: commitment.dueDay })}
                          {commitment.householdId
                            ? ` · ${householdNames[commitment.householdId] ?? t('plans.household')}`
                            : ` · ${t('commitments.private')}`}
                          {detail ? ` · ${detail}` : ''}
                        </span>
                      </div>

                      <span className="text-right font-medium tabular-nums">
                        {euro.format(Number(commitment.amount))}
                      </span>

                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button
                            variant="ghost"
                            size="icon"
                            className="size-8"
                            disabled={!mayEdit}
                            aria-label={t('commitments.menuLabel', { name: commitment.name })}
                          >
                            <MoreHorizontal className="size-4" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          <DropdownMenuItem
                            onSelect={() => handleEdit(commitment)}
                            className="gap-2"
                          >
                            <Pencil className="size-4" />
                            {t('common.edit')}
                          </DropdownMenuItem>
                          {mayDelete && (
                            <DropdownMenuItem
                              onSelect={() => setPendingDelete(commitment)}
                              variant="destructive"
                              className="gap-2"
                            >
                              <Trash2 className="size-4" />
                              {t('common.delete')}
                            </DropdownMenuItem>
                          )}
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </li>
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
        onSave={(saved) => save.mutate(saved)}
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
