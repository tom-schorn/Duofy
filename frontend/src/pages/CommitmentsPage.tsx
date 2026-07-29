import { useState } from 'react'
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
import {
  useCommitments,
  useDeleteCommitment,
  useHouseholds,
  useSaveCommitment,
} from '@/lib/queries'
import {
  BLOCK_DOT,
  BLOCK_LABEL,
  BUDGET_ORDER,
  CATEGORY_LABEL,
  MONTH_LABEL,
  RHYTHM_LABEL,
  dueMonths,
  euro,
  firstMonthOf,
  monthlyEquivalent,
  type Commitment,
} from '@/lib/domain'

/**
 * Alle Verträge auf einer Seite, gruppiert nach Budget.
 *
 * Sparpläne und Kredite sind auch Verträge — im Modell ist es eine Tabelle
 * (`Commitment`), `type` sagt nur, ob das Ding ein Ende hat. Gruppiert wird
 * nach **Budget**, weil das die Achse ist, die beim Planen zählt: ein Vertrag
 * kann in jedem Budget liegen. Im Code heißt das Feld weiter `block`.
 *
 * Dieselbe Gruppierung wie in der Planung — ein Aufbau, zwei Seiten.
 */

/** Monatlich braucht keinen Zusatz — der Rest zeigt, wann es wirklich anfällt. */
function rhythmText(commitment: Commitment) {
  const months = dueMonths(commitment.rhythm, firstMonthOf(commitment))
  if (months.length === 0) return RHYTHM_LABEL[commitment.rhythm]
  const short = months.map((month) => MONTH_LABEL[month - 1].slice(0, 3))
  return `${RHYTHM_LABEL[commitment.rhythm]} · ${short.join(', ')}`
}

/** Was aus dem Typ folgt — Ziel oder Restschuld, sonst nichts. */
function typeDetail(commitment: Commitment) {
  if (commitment.type === 'savings_goal' && commitment.targetAmount) {
    const date = commitment.targetDate
      ? new Date(commitment.targetDate).toLocaleDateString('de-DE', {
          month: '2-digit',
          year: 'numeric',
        })
      : null
    const target = euro.format(Number(commitment.targetAmount))
    return date ? `Ziel ${target} bis ${date}` : `Ziel ${target}`
  }
  if (commitment.type === 'debt' && commitment.remainingDebt) {
    return `Rest ${euro.format(Number(commitment.remainingDebt))}`
  }
  return null
}

export function CommitmentsPage() {
  const commitments = useCommitments()
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
    // Inaktive zählen nicht mit — sie erzeugen keine Posten.
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
    // Bereits erzeugte Posten bleiben stehen — das Modell setzt
    // `commitment_id` auf NULL (ON DELETE SET NULL).
    remove.mutate(pendingDelete.id)
    setPendingDelete(null)
  }

  return (
    <div className="flex flex-col gap-8">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-col gap-2">
          <h1 className="font-heading text-3xl font-semibold">Verträge</h1>
          <p className="text-muted-foreground">
            Alles Wiederkehrende — Miete, Abos, Sparpläne, Kredite. Einmal
            angelegt, erzeugt es seine Posten selbst.
          </p>
        </div>
        <Button onClick={handleAdd}>
          <Plus className="size-4" />
          Vertrag anlegen
        </Button>
      </header>

      <QueryState isPending={commitments.isPending} error={commitments.error}>
      {groups.length === 0 ? (
        <p className="text-muted-foreground border-border rounded-lg border border-dashed p-10 text-center text-sm">
          Noch kein Vertrag angelegt.
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
                  {BLOCK_LABEL[group.block]}
                </h2>
                <span className="text-muted-foreground text-sm tabular-nums">
                  {euro.format(group.total)}
                  <span className="ml-1 text-xs">Ø / Monat</span>
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
                              inaktiv
                            </Badge>
                          )}
                        </span>
                        <span className="text-muted-foreground truncate text-xs">
                          {CATEGORY_LABEL[commitment.category]} ·{' '}
                          {rhythmText(commitment)} · {commitment.dueDay}.
                          {commitment.householdId
                            ? ` · ${householdNames[commitment.householdId] ?? 'Haushalt'}`
                            : ' · privat'}
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
                            aria-label={`${commitment.name} bearbeiten oder löschen`}
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
                            Bearbeiten
                          </DropdownMenuItem>
                          <DropdownMenuItem
                            onSelect={() => setPendingDelete(commitment)}
                            variant="destructive"
                            className="gap-2"
                          >
                            <Trash2 className="size-4" />
                            Löschen
                          </DropdownMenuItem>
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
              „{pendingDelete?.name}" löschen?
            </AlertDialogTitle>
            <AlertDialogDescription>
              Bereits geplante Posten in laufenden Monaten bleiben stehen. Für
              künftige Monate entsteht nichts mehr.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Abbrechen</AlertDialogCancel>
            <AlertDialogAction onClick={handleDelete}>
              Löschen
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}
