import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { ArrowLeft, Plus, Users } from 'lucide-react'

import { BudgetSection } from '@/components/BudgetSection'
import { PositionDialog } from '@/components/PositionDialog'
import { QueryState } from '@/components/QueryState'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import {
  useConfirmPlan,
  useDeletePosition,
  useHouseholds,
  usePlan,
  useSavePosition,
  useTogglePaid,
} from '@/lib/queries'
import {
  BLOCK_DOT,
  BLOCK_LABEL,
  BUDGETS,
  MONTH_LABEL,
  PLAN_STATUS_LABEL,
  QUOTA_KEY,
  euro,
  isPaid,
  type Block,
  type PlanDetail,
  type PlanPosition,
} from '@/lib/domain'

/**
 * Ein Monatsplan im Detail — das Herzstück.
 *
 * Der Ablauf folgt dem Ritual: Einnahmen erwarten, Puffer abziehen, den Rest
 * auf die drei Budgets verteilen, prüfen ob es aufgeht, bestätigen.
 *
 * Die Quoten sind **Richtwerte**, keine Regel. Es gibt ein Soll, daneben steht
 * das Ist, und man schaut dass es passt.
 */
export function PlanDetailPage() {
  const { year, month } = useParams()
  const plan = usePlan(Number(year), Number(month))
  const households = useHouseholds()

  const names = Object.fromEntries(
    (households.data ?? []).map((household) => [household.id, household.name])
  )

  return (
    <div className="flex flex-col gap-8">
      <Link
        to="/plan"
        className="text-muted-foreground hover:text-foreground flex w-fit items-center gap-1.5 text-sm"
      >
        <ArrowLeft className="size-4" />
        Alle Pläne
      </Link>

      <QueryState isPending={plan.isPending} error={plan.error} rows={4}>
        {plan.data && <PlanBody plan={plan.data} householdNames={names} />}
      </QueryState>
    </div>
  )
}

function PlanBody({
  plan,
  householdNames,
}: {
  plan: PlanDetail
  householdNames: Record<string, string>
}) {
  const savePosition = useSavePosition()
  const deletePosition = useDeletePosition()
  const togglePaid = useTogglePaid()
  const confirmPlan = useConfirmPlan()

  const [editing, setEditing] = useState<PlanPosition | null>(null)
  const [addingTo, setAddingTo] = useState<Block>('wants')
  const [dialogOpen, setDialogOpen] = useState(false)

  const groups = BUDGETS.map((block) => {
    const key = block as keyof typeof QUOTA_KEY
    return {
      block,
      rows: plan.positions.filter((row) => row.block === block),
      quota: Number(plan[QUOTA_KEY[key]]),
      target: Number(plan.budget) * (Number(plan[QUOTA_KEY[key]]) / 100),
    }
  })

  // „Verplanbar" = was vom Budget noch frei ist, nicht das Budget selbst.
  const allocated = groups.reduce(
    (total, group) =>
      total + group.rows.reduce((sum, row) => sum + Number(row.amountPlanned), 0),
    0
  )
  const free = Number(plan.budget) - allocated

  // „Noch offen" = was diesen Monat noch bezahlt werden muss.
  const unpaid = plan.positions
    .filter((row) => row.block !== 'income' && !isPaid(row))
    .reduce((sum, row) => sum + Number(row.amountPlanned), 0)

  const householdIds = [
    ...new Set(
      plan.positions
        .map((row) => row.householdId)
        .filter((id): id is string => id !== null)
    ),
  ]

  function handleAdd(block: Block) {
    setEditing(null)
    setAddingTo(block)
    setDialogOpen(true)
  }

  return (
    <>
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-col gap-2">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="font-heading text-3xl font-semibold">
              {MONTH_LABEL[plan.month - 1]} {plan.year}
            </h1>
            <Badge variant={plan.status === 'draft' ? 'outline' : 'secondary'}>
              {PLAN_STATUS_LABEL[plan.status]}
            </Badge>
            {householdIds.map((id) => (
              <Badge key={id} variant="secondary" className="gap-1 font-normal">
                <Users className="size-3" />
                {householdNames[id] ?? 'Haushalt'}
              </Badge>
            ))}
          </div>
          <p className="text-muted-foreground">
            Verplane den Monat, bevor er anfängt. Die Quoten sind Richtwerte —
            es zählt, dass es aufgeht.
          </p>
        </div>
        <Button onClick={() => handleAdd('wants')}>
          <Plus className="size-4" />
          Posten hinzufügen
        </Button>
      </header>

      <section className="grid gap-3 sm:grid-cols-3">
        <Metric label="Einnahmen" value={Number(plan.income)} />
        <Metric
          label="Verplanbar"
          value={free}
          hint="noch nicht verteilt"
          strong
          tone={free < 0 ? 'over' : 'neutral'}
        />
        <Metric label="Noch offen" value={unpaid} hint="noch nicht bezahlt" />
      </section>

      {/* Quotenband — zeigt das Soll, nicht das Ist. */}
      <section className="flex flex-col gap-2">
        <div className="flex h-2 gap-0.5">
          {groups.map((group) => (
            <span
              key={group.block}
              className={`rounded-full ${BLOCK_DOT[group.block]}`}
              style={{ flex: group.quota }}
            />
          ))}
        </div>
        <div className="text-muted-foreground flex justify-between text-xs">
          {groups.map((group) => (
            <span key={group.block}>
              {BLOCK_LABEL[group.block]} {group.quota} %
            </span>
          ))}
        </div>
      </section>

      <div className="flex flex-col gap-8">
        {groups.map((group) => (
          <BudgetSection
            key={group.block}
            block={group.block}
            target={group.target}
            positions={group.rows}
            householdNames={householdNames}
            onEdit={(position) => {
              setEditing(position)
              setDialogOpen(true)
            }}
            onAdd={handleAdd}
            onTogglePaid={(position) =>
              togglePaid.mutate({ id: position.id, paid: !isPaid(position) })
            }
          />
        ))}
      </div>

      <footer className="flex flex-wrap items-center justify-between gap-4 border-t pt-6">
        <p className="text-muted-foreground text-sm">
          {plan.status === 'draft'
            ? 'Entwurf — noch nicht bestätigt.'
            : 'Bestätigt. Der Monat läuft.'}
        </p>
        {/* TODO: Im Haushalt müssen beide bestätigen. Dafür fehlt im Backend
            eine Tabelle, die je Mitglied festhält wer zugestimmt hat. */}
        <Button
          onClick={() => confirmPlan.mutate(plan.id)}
          disabled={plan.status === 'confirmed' || confirmPlan.isPending}
        >
          Monat bestätigen
        </Button>
      </footer>

      <PositionDialog
        position={editing}
        block={editing?.block ?? addingTo}
        planId={plan.id}
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        onSave={(position) => savePosition.mutate({ ...position, planId: plan.id })}
        onDelete={(position) => deletePosition.mutate(position.id)}
      />
    </>
  )
}

function Metric({
  label,
  value,
  hint,
  strong = false,
  tone = 'neutral',
}: {
  label: string
  value: number
  hint?: string
  strong?: boolean
  tone?: 'neutral' | 'over'
}) {
  return (
    <div className="bg-card border-border flex flex-col gap-1 rounded-lg border p-4">
      <span className="text-muted-foreground text-[11px] font-semibold tracking-widest uppercase">
        {label}
      </span>
      <span
        className={`font-mono tabular-nums ${strong ? 'text-xl font-semibold' : 'text-lg font-medium'} ${
          tone === 'over' ? 'text-destructive' : ''
        }`}
      >
        {euro.format(value)}
      </span>
      {hint && <span className="text-muted-foreground text-xs">{hint}</span>}
    </div>
  )
}
