import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { ArrowLeft, Plus, Users } from 'lucide-react'

import { BudgetSection } from '@/components/BudgetSection'
import { MonthFlow } from '@/components/MonthFlow'
import { PositionDialog } from '@/components/PositionDialog'
import { QueryState } from '@/components/QueryState'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import {
  useConfirmPlan,
  useDeletePosition,
  useHouseholdPlan,
  useHouseholds,
  usePlan,
  useSavePosition,
  useTogglePaid,
} from '@/lib/queries'
import {
  BUDGETS,
  MONTH_LABEL,
  PLAN_STATUS_LABEL,
  QUOTA_KEY,
  euro,
  isPaid,
  type Block,
  type HouseholdPlanDetail,
  type HouseholdPosition,
  type PlanDetail,
  type PlanPosition,
} from '@/lib/domain'
import { useScope } from '@/lib/scope'

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
  const { householdId } = useScope()
  const shared = householdId !== null

  // Beide Hooks stehen immer da — React erlaubt keine bedingten Hooks. Der
  // jeweils ungenutzte ist über `enabled` abgeschaltet und lädt nichts.
  const ownPlan = usePlan(Number(year), Number(month), !shared)
  const householdPlan = useHouseholdPlan(
    householdId,
    Number(year),
    Number(month)
  )
  const query = shared ? householdPlan : ownPlan

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

      <QueryState isPending={query.isPending} error={query.error} rows={4}>
        {shared
          ? householdPlan.data && (
              <HouseholdPlanBody
                plan={householdPlan.data}
                householdNames={names}
              />
            )
          : ownPlan.data && (
              <PlanBody plan={ownPlan.data} householdNames={names} />
            )}
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

  // Einnahmen stehen bewusst außerhalb von `groups`: sie haben keine Quote und
  // dürfen nicht in `allocated` einfließen, sonst stimmt „Verplanbar" nicht.
  const incomeRows = plan.positions.filter((row) => row.block === 'income')

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

  // Wie viele Posten in welchen Haushalt laufen. Die Zahl steht im Badge, weil
  // ein blanker Name wie ein Besitzverhältnis aussieht — der Plan gehört aber
  // dir, es ist nur ein Teil davon gemeinsam.
  const householdCounts = plan.positions.reduce<Record<string, number>>(
    (counts, row) =>
      row.householdId === null
        ? counts
        : { ...counts, [row.householdId]: (counts[row.householdId] ?? 0) + 1 },
    {}
  )

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
            {Object.entries(householdCounts).map(([id, count]) => (
              <Badge key={id} variant="secondary" className="gap-1 font-normal">
                <Users className="size-3" />
                {count} Posten aus {householdNames[id] ?? 'Haushalt'}
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

      <MonthFlow positions={plan.positions} year={plan.year} month={plan.month} />

      <div className="flex flex-col gap-8">
        {/* Einnahmen zuerst — sie sind die Grundlage für alles darunter.
            target={null}, weil es für Einnahmen keine Quote gibt. */}
        <BudgetSection
          block="income"
          target={null}
          positions={incomeRows}
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

/**
 * Der gemeinsame Plan — zusammengesetzt, nicht gespeichert.
 *
 * Deshalb fehlen hier zwei Dinge, die es im eigenen Plan gibt: „Monat anlegen"
 * und „Monat bestätigen". Es existiert kein Objekt, das man anlegen oder
 * bestätigen könnte — die Ansicht entsteht aus den Posten aller Mitglieder.
 *
 * Geändert wird deshalb im eigenen Plan. Hier steht nur, wer was trägt.
 */
function HouseholdPlanBody({
  plan,
  householdNames,
}: {
  plan: HouseholdPlanDetail
  householdNames: Record<string, string>
}) {
  const groups = BUDGETS.map((block) => {
    const key = block as keyof typeof QUOTA_KEY
    return {
      block,
      rows: plan.positions.filter((row) => row.block === block),
      target: Number(plan.budget) * (Number(plan[QUOTA_KEY[key]]) / 100),
    }
  })

  const incomeRows = plan.positions.filter((row) => row.block === 'income')

  const allocated = groups.reduce(
    (total, group) =>
      total + group.rows.reduce((sum, row) => sum + Number(row.amountPlanned), 0),
    0
  )
  const free = Number(plan.budget) - allocated

  const unpaid = plan.positions
    .filter((row) => row.block !== 'income' && !isPaid(row))
    .reduce((sum, row) => sum + Number(row.amountPlanned), 0)

  // Steht in der Zeile hinter der Kategorie: „Miete · 1. · Jasmin".
  const ownerName = (position: PlanPosition) =>
    (position as HouseholdPosition).ownerName ?? null

  const noPositions = plan.positions.length === 0

  return (
    <>
      <header className="flex flex-col gap-2">
        <div className="flex flex-wrap items-center gap-2">
          <h1 className="font-heading text-3xl font-semibold">
            {MONTH_LABEL[plan.month - 1]} {plan.year}
          </h1>
          <Badge variant="secondary" className="gap-1 font-normal">
            <Users className="size-3" />
            {plan.householdName}
          </Badge>
        </div>
        <p className="text-muted-foreground">
          Alle Posten, die ihr gemeinsam tragt. Zusammengesetzt aus den Plänen
          aller Mitglieder — geändert wird im eigenen Plan.
        </p>
      </header>

      {noPositions ? (
        <p className="text-muted-foreground bg-card border-border rounded-lg border p-6 text-sm">
          Für diesen Monat ist noch nichts als gemeinsam markiert. Setz im
          eigenen Plan bei einem Posten den Haushalt — dann taucht er hier auf.
        </p>
      ) : (
        <>
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

          <div className="flex flex-col gap-8">
            <BudgetSection
              block="income"
              target={null}
              positions={incomeRows}
              householdNames={householdNames}
              onEdit={() => {}}
              onAdd={() => {}}
              onTogglePaid={() => {}}
              readOnly
              ownerName={ownerName}
            />

            {groups.map((group) => (
              <BudgetSection
                key={group.block}
                block={group.block}
                target={group.target}
                positions={group.rows}
                householdNames={householdNames}
                onEdit={() => {}}
                onAdd={() => {}}
                onTogglePaid={() => {}}
                readOnly
                ownerName={ownerName}
              />
            ))}
          </div>
        </>
      )}
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
