import { Check, Plus, Users } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  BLOCK_DOT,
  BLOCK_LABEL,
  CATEGORY_LABEL,
  PAYMENT_LABEL,
  euro,
  isPaid,
  type Block,
  type PlanPosition,
} from '@/lib/domain'

/**
 * Ein Budget mit Soll gegen Ist und seinen Posten.
 *
 * Bewusst **eine** Komponente für „Mein Plan" und den Haushalt: die Regeln
 * sind dieselben, nur die Datenquelle wechselt. Welchen Plan man sieht,
 * entscheidet der Umschalter in der Sidebar.
 *
 * **Keine Untergruppen.** Wünsche ist eine flache Liste — Investitionen sind
 * ganz normale Posten der Kategorie „Investition", nicht mehr.
 */

/** Ab wieviel Prozent über der Quote es rot wird. */
const OVER_QUOTA = 100

type Props = {
  block: Block
  /** Soll aus der Quote — null bei Einnahmen, die haben keine. */
  target: number | null
  positions: PlanPosition[]
  householdNames: Record<string, string>
  onEdit: (position: PlanPosition) => void
  onAdd: (block: Block) => void
  onTogglePaid: (position: PlanPosition) => void
}

export function BudgetSection({
  block,
  target,
  positions,
  householdNames,
  onEdit,
  onAdd,
  onTogglePaid,
}: Props) {
  const total = positions.reduce(
    (sum, position) => sum + Number(position.amountPlanned),
    0
  )
  const percent = target && target > 0 ? (total / target) * 100 : 0
  const isOver = percent > OVER_QUOTA

  return (
    <section className="flex flex-col gap-3">
      <header className="flex flex-col gap-2">
        <div className="flex items-baseline justify-between gap-4">
          <h2 className="flex items-center gap-2 text-sm font-semibold tracking-wide uppercase">
            <span className={`size-2.5 rounded-sm ${BLOCK_DOT[block]}`} />
            {BLOCK_LABEL[block]}
          </h2>
          <span className="text-sm tabular-nums">
            <span className={isOver ? 'text-destructive font-semibold' : 'font-semibold'}>
              {euro.format(total)}
            </span>
            {target !== null && (
              <span className="text-muted-foreground">
                {' '}
                von {euro.format(target)}
              </span>
            )}
          </span>
        </div>

        {target !== null && (
          <div className="bg-muted h-1.5 overflow-hidden rounded-full">
            <div
              className={`h-full rounded-full ${isOver ? 'bg-destructive' : BLOCK_DOT[block]}`}
              style={{ width: `${Math.min(percent, 100)}%` }}
            />
          </div>
        )}
      </header>

      <ul className="flex flex-col">
        {positions.map((position) => (
          <PositionRow
            key={position.id}
            position={position}
            householdNames={householdNames}
            onEdit={onEdit}
            onTogglePaid={onTogglePaid}
          />
        ))}
      </ul>

      {/* Anlegen direkt am Budget — dann stimmt die Zuordnung schon, ohne
          dass man sie im Formular suchen muss. */}
      <Button
        type="button"
        variant="ghost"
        size="sm"
        onClick={() => onAdd(block)}
        className="text-muted-foreground hover:text-foreground w-fit"
      >
        <Plus className="size-4" />
        Posten in {BLOCK_LABEL[block]}
      </Button>
    </section>
  )
}

function PositionRow({
  position,
  householdNames,
  onEdit,
  onTogglePaid,
}: {
  position: PlanPosition
  householdNames: Record<string, string>
  onEdit: (position: PlanPosition) => void
  onTogglePaid: (position: PlanPosition) => void
}) {
  const planned = Number(position.amountPlanned)
  const actual =
    position.amountActual === null ? null : Number(position.amountActual)
  // Nur echte Überschreitungen markieren — Unterschreitungen sind kein Problem.
  const overspent = actual !== null && actual > planned
  const paid = isPaid(position)

  return (
    <li className="border-border/60 grid grid-cols-[auto_1fr_auto] items-center gap-3 border-b py-2.5 last:border-b-0">
      {/* Abhaken ist der Alltag nach dem Planen — deshalb ein eigener Knopf,
          nicht im Formular versteckt. Einnahmen hakt man nicht ab. */}
      {position.block === 'income' ? (
        <span className="size-5" />
      ) : (
        <button
          type="button"
          onClick={() => onTogglePaid(position)}
          aria-label={paid ? `${position.label} wieder öffnen` : `${position.label} abhaken`}
          aria-pressed={paid}
          className={`flex size-5 items-center justify-center rounded border transition-colors ${
            paid
              ? 'bg-chart-4 border-chart-4 text-background'
              : 'border-border hover:border-ring'
          }`}
        >
          {paid && <Check className="size-3.5" strokeWidth={3} />}
        </button>
      )}

      <button
        type="button"
        onClick={() => onEdit(position)}
        className={`flex min-w-0 flex-col items-start gap-0.5 text-left ${paid ? 'opacity-60' : ''}`}
      >
        <span className="flex flex-wrap items-center gap-2">
          <span className="font-medium">{position.label}</span>
          {/* Dieser Posten läuft zusätzlich in einen Haushaltsplan. Er bleibt
              trotzdem hier — der Haushaltsplan ist keine eigene Tabelle,
              sondern entsteht aus genau solchen Posten. */}
          {position.householdId && (
            <Badge variant="secondary" className="gap-1 font-normal">
              <Users className="size-3" />
              {householdNames[position.householdId]}
            </Badge>
          )}
        </span>
        <span className="text-muted-foreground truncate text-xs">
          {CATEGORY_LABEL[position.category]} · {position.dueDay}.
          {position.paymentMethod
            ? ` · ${PAYMENT_LABEL[position.paymentMethod]}`
            : ''}
          {position.commitmentId ? ' · aus Vertrag' : ''}
        </span>
      </button>

      <span className="flex flex-col items-end tabular-nums">
        <span className="font-medium">{euro.format(planned)}</span>
        {actual !== null && actual !== planned && (
          <span
            className={`text-xs ${overspent ? 'text-destructive' : 'text-muted-foreground'}`}
          >
            ist {euro.format(actual)}
          </span>
        )}
      </span>
    </li>
  )
}
