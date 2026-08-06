import { Plus, User, Users } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Progress } from '@/components/ui/progress'
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
 * One block with target against actual, and its positions.
 *
 * Deliberately **one** component for your own plan and the household: the rules are
 * the same, only the data source changes.
 *
 * **No subgroups.** Wants is a flat list — investments are ordinary positions of
 * the investment category, nothing more.
 */

/**
 * Bar colour per block — **written out**, never composed.
 *
 * Tailwind reads the classes out of the source text. A `[&_...]:${BLOCK_DOT[block]}`
 * never appears there as a finished class and would therefore not be generated: the
 * bar would stay grey. The `Progress` indicator is fixed to `bg-primary`, so it has
 * to be overridden.
 */
const BAR: Record<Block, string> = {
  income: '[&_[data-slot=progress-indicator]]:bg-chart-3',
  needs: '[&_[data-slot=progress-indicator]]:bg-chart-1',
  wants: '[&_[data-slot=progress-indicator]]:bg-chart-2',
  savings: '[&_[data-slot=progress-indicator]]:bg-chart-4',
}

const BAR_OVER = '[&_[data-slot=progress-indicator]]:bg-destructive'

/** How many percent over the quota turns the bar red. */
const OVER_QUOTA = 100

type Props = {
  block: Block
  /** Target from the quota — null for income, which has none. */
  target: number | null
  positions: PlanPosition[]
  householdNames: Record<string, string>
  onEdit: (position: PlanPosition) => void
  onAdd: (block: Block) => void
  onTogglePaid: (position: PlanPosition) => void
  /** Shared view: other people positions are shown but not changed. */
  readOnly?: boolean
  /**
   * Separate from `readOnly`, because the "may change" level allows exactly that
   * and no more: **creating** a position in somebody else plan would not be acting
   * on their behalf, it would be a new commitment in their name.
   */
  canAdd?: boolean
  /** Returns the first name of the person behind the position, otherwise null. */
  ownerName?: (position: PlanPosition) => string | null
}

export function BudgetSection({
  block,
  target,
  positions,
  householdNames,
  onEdit,
  onAdd,
  onTogglePaid,
  readOnly = false,
  canAdd = true,
  ownerName,
}: Props) {
  // Pass-through positions appear in the list but not in the total: they are not
  // part of the budget, so the quota must not see them.
  const total = positions.reduce(
    (sum, position) =>
      position.passThrough ? sum : sum + Number(position.amountPlanned),
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
          // The `Progress` indicator is fixed to `bg-primary`, but here the block
          // colour carries identity, so it is overridden.
          <Progress
            value={Math.min(percent, 100)}
            aria-label={`${BLOCK_LABEL[block]}: ${Math.round(percent)} % der Quote`}
            className={`h-1.5 ${isOver ? BAR_OVER : BAR[block]}`}
          />
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
            readOnly={readOnly}
            ownerName={ownerName?.(position) ?? null}
          />
        ))}
      </ul>

      {/* Anlegen direkt am Budget — dann stimmt die Zuordnung schon, ohne
          dass man sie im Formular suchen muss. */}
      {!readOnly && canAdd && (
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
      )}
    </section>
  )
}

function PositionRow({
  position,
  householdNames,
  onEdit,
  onTogglePaid,
  readOnly,
  ownerName,
}: {
  position: PlanPosition
  householdNames: Record<string, string>
  onEdit: (position: PlanPosition) => void
  onTogglePaid: (position: PlanPosition) => void
  readOnly: boolean
  ownerName: string | null
}) {
  const planned = Number(position.amountPlanned)
  const actual =
    position.amountActual === null ? null : Number(position.amountActual)
  // Only flag real overruns — staying below the quota is not a problem.
  const overspent = actual !== null && actual > planned
  const paid = isPaid(position)

  return (
    <li className="border-border/60 grid grid-cols-[auto_1fr_auto] items-center gap-3 border-b py-2.5 last:border-b-0">
      {/* Ticking off is the everyday work after planning — hence a control of its
          own rather than something hidden in a form.

          Income can be ticked off too; there it means "it arrived" rather than
          "paid". It does not affect what is still open: that figure excludes income
          anyway. */}
      {position.isBudget ? (
        /* Budget positions are not ticked off — they fill up over the month from
           individual bookings. A tick would mean nothing here, so instead of a dead
           box there is nothing. */
        <span className="size-6" aria-hidden />
      ) : (
        /* `Checkbox` from the theme rather than a hand-rolled button. "Done" is a
           state, not a tool toggle — the checkbox announces it by itself, and shows
           as disabled in the read-only view. What stood here before was a button
           with `aria-pressed` next to a `role="img"` workaround.

           Green rather than the primary colour: "paid" belongs with saving and
           completion, not with the brand. The class is written out because Tailwind
           does not generate composed ones.

           The frame is size-6 and the box size-4: WCAG 2.2 SC 2.5.8 asks for a
           24 × 24 px target, and the checkbox brings its own through
           `after:-inset`. */
        <span className="flex size-6 items-center justify-center">
          <Checkbox
            checked={paid}
            disabled={readOnly}
            onCheckedChange={() => onTogglePaid(position)}
            aria-label={
              paid
                ? `${position.label} wieder öffnen`
                : position.block === 'income'
                  ? `${position.label} als erhalten markieren`
                  : `${position.label} abhaken`
            }
            className="data-checked:border-chart-4 data-checked:bg-chart-4 data-checked:text-background"
          />
        </span>
      )}

      <button
        type="button"
        onClick={() => onEdit(position)}
        disabled={readOnly}
        className={`flex min-w-0 flex-col items-start gap-0.5 text-left ${paid ? 'opacity-60' : ''} ${readOnly ? 'cursor-default' : ''}`}
      >
        <span className="flex flex-wrap items-center gap-2">
          <span className="font-medium">{position.label}</span>
          {/* In der gemeinsamen Sicht steht hier die Person, nicht der
              Haushalt — der ist dort in jeder Zeile derselbe und sagt nichts.
              Im eigenen Plan umgekehrt: dort ist die Person klar, und der
              Badge zeigt, dass der Posten zusätzlich in einen Haushaltsplan
              läuft. */}
          {ownerName ? (
            <Badge variant="secondary" className="gap-1 font-normal">
              <User className="size-3" />
              {ownerName}
            </Badge>
          ) : (
            position.householdId && (
              <Badge variant="secondary" className="gap-1 font-normal">
                <Users className="size-3" />
                {householdNames[position.householdId]}
              </Badge>
            )
          )}
        </span>
        <span className="text-muted-foreground truncate text-xs">
          {CATEGORY_LABEL[position.category]} · {position.dueDay}.
          {position.paymentMethod
            ? ` · ${PAYMENT_LABEL[position.paymentMethod]}`
            : ''}
          {position.commitmentId ? ' · aus Vertrag' : ''}
          {position.isBudget ? ' · Budget' : ''}
          {position.passThrough ? ' · durchlaufend' : ''}
          {position.counterAccountId ? ' · Umbuchung' : ''}
        </span>
      </button>

      <span className="flex flex-col items-end gap-1 tabular-nums">
        {position.isBudget ? (
          <>
            <span className="text-sm">
              <span
                className={`font-medium ${overspent ? 'text-destructive' : ''}`}
              >
                {euro.format(actual ?? 0)}
              </span>
              <span className="text-muted-foreground">
                {' '}
                von {euro.format(planned)}
              </span>
            </span>
            {/* Füllstand statt Haken: die Frage ist „wie viel ist weg", nicht
                „ist es erledigt". */}
            <Progress
              value={Math.min(((actual ?? 0) / (planned || 1)) * 100, 100)}
              aria-label={`${position.label}: ${euro.format(actual ?? 0)} von ${euro.format(planned)}`}
              className={`w-24 ${overspent ? BAR_OVER : BAR.needs}`}
            />
          </>
        ) : (
          <>
            <span className="font-medium">{euro.format(planned)}</span>
            {actual !== null && actual !== planned && (
              <span
                className={`text-xs ${overspent ? 'text-destructive' : 'text-muted-foreground'}`}
              >
                ist {euro.format(actual)}
              </span>
            )}
          </>
        )}
      </span>
    </li>
  )
}
