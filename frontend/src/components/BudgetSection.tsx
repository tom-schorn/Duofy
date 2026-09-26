import { CircleCheck, Plus, User, Users } from 'lucide-react'
import { useTranslation } from 'react-i18next'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { ListRow } from '@/components/ListRow'
import { Progress } from '@/components/ui/progress'
import {
  BUDGET_DOT,
  budgetHeadingId,
  budgetLabel,
  categoryLabel,
  paymentLabel,
  euro,
  isPaid,
  type Budget,
  type PlanPosition,
} from '@/lib/domain'

/**
 * One budget with target against actual, and its positions.
 *
 * Deliberately **one** component for your own plan and the household: the rules are
 * the same, only the data source changes.
 *
 * **No subgroups.** Wants is a flat list — investments are ordinary positions of
 * the investment category, nothing more.
 */

/**
 * Bar colour per budget — **written out**, never composed.
 *
 * Tailwind reads the classes out of the source text. A `[&_...]:${BUDGET_DOT[budget]}`
 * never appears there as a finished class and would therefore not be generated: the
 * bar would stay grey. The `Progress` indicator is fixed to `bg-primary`, so it has
 * to be overridden.
 */
const BAR: Record<Budget, string> = {
  income: '[&_[data-slot=progress-indicator]]:bg-chart-3',
  needs: '[&_[data-slot=progress-indicator]]:bg-chart-1',
  wants: '[&_[data-slot=progress-indicator]]:bg-chart-2',
  savings: '[&_[data-slot=progress-indicator]]:bg-chart-4',
}

const BAR_OVER = '[&_[data-slot=progress-indicator]]:bg-destructive'

/** How many percent over the quota turns the bar red. */
const OVER_QUOTA = 100

type Props = {
  budget: Budget
  /** Target from the quota — null for income, which has none. */
  target: number | null
  positions: PlanPosition[]
  householdNames: Record<string, string>
  onEdit: (position: PlanPosition) => void
  onAdd: (budget: Budget) => void
  onTogglePaid: (position: PlanPosition) => void
  /** Shared view: other people positions are shown but not changed. */
  readOnly?: boolean
  /**
   * Separate from `readOnly`, because the two are not the same question: a
   * household plan is read-only for everyone and offers no adding either, while
   * standing in for a member at level `edit` allows both.
   */
  canAdd?: boolean
  /** Returns the first name of the person behind the position, otherwise null. */
  ownerName?: (position: PlanPosition) => string | null
}

export function BudgetSection({
  budget,
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
  const { t } = useTranslation()
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
          {/* tabIndex -1: the focus lands here after a position was deleted. */}
          <h2
            id={budgetHeadingId(budget)}
            tabIndex={-1}
            className="flex items-center gap-2 text-sm font-semibold tracking-wide uppercase outline-none"
          >
            <span className={`size-2.5 rounded-sm ${BUDGET_DOT[budget]}`} />
            {budgetLabel(budget)}
          </h2>
          <span className="text-sm tabular-nums">
            <span className={isOver ? 'text-destructive font-semibold' : 'font-semibold'}>
              {euro.format(total)}
            </span>
            {target !== null && (
              <span className="text-muted-foreground">
                {' '}
                {t('budget.of', { amount: euro.format(target) })}
              </span>
            )}
          </span>
        </div>

        {target !== null && (
          // The `Progress` indicator is fixed to `bg-primary`, but here the budget
          // colour carries identity, so it is overridden.
          <Progress
            value={Math.min(percent, 100)}
            aria-label={t('budget.quotaLabel', {
              budget: budgetLabel(budget),
              percent: Math.round(percent),
            })}
            className={`h-1.5 ${isOver ? BAR_OVER : BAR[budget]}`}
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
        onClick={() => onAdd(budget)}
        className="text-muted-foreground hover:text-foreground w-fit"
      >
        <Plus className="size-4" />
        {t('budget.addPosition', { budget: budgetLabel(budget) })}
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
  const { t } = useTranslation()
  const actual =
    position.amountActual === null ? null : Number(position.amountActual)
  // Only flag real overruns — staying below the quota is not a problem.
  const overspent = actual !== null && actual > planned
  const paid = isPaid(position)

  const leading = (
    <>
      {/* Ticking off is the everyday work after planning — hence a control of its
          own rather than something hidden in a form.

          Income can be ticked off too; there it means "it arrived" rather than
          "paid". It does not affect what is still open: that figure excludes income
          anyway. */}
      {position.isLimit ? (
        /* Limit positions are not ticked off — they fill up over the month from
           individual bookings. A tick would mean nothing here, so instead of a dead
           box there is nothing. */
        <span className="size-6" aria-hidden />
      ) : readOnly ? (
        /* Rule 3: no right, no control. The state stays visible as a plain symbol,
           not as a greyed-out box that asks "why not?". */
        paid ? (
          <span className="flex size-6 items-center justify-center">
            <CircleCheck
              className="text-chart-4 size-5"
              role="img"
              aria-label={
                position.budget === 'income'
                  ? t('budget.statusReceived')
                  : t('budget.statusPaid')
              }
            />
          </span>
        ) : (
          <span className="size-6" aria-hidden />
        )
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
            onCheckedChange={() => onTogglePaid(position)}
            aria-label={
              paid
                ? t('budget.reopen', { label: position.label })
                : position.budget === 'income'
                  ? t('budget.markReceived', { label: position.label })
                  : t('budget.tick', { label: position.label })
            }
            className="data-checked:border-chart-4 data-checked:bg-chart-4 data-checked:text-background"
          />
        </span>
      )}
    </>
  )

  const amount = (
    <>
        {position.isLimit ? (
          <>
            <span className="text-sm">
              <span
                className={`font-medium ${overspent ? 'text-destructive' : ''}`}
              >
                {euro.format(actual ?? 0)}
              </span>
              <span className="text-muted-foreground">
                {' '}
                {t('budget.of', { amount: euro.format(planned) })}
              </span>
            </span>
            {/* A fill level instead of a tick: the question is how much is used
                up, not whether it is done. */}
            <Progress
              value={Math.min(((actual ?? 0) / (planned || 1)) * 100, 100)}
              aria-label={t('budget.fillLabel', {
                label: position.label,
                actual: euro.format(actual ?? 0),
                planned: euro.format(planned),
              })}
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
                {t('budget.actual', { amount: euro.format(actual) })}
              </span>
            )}
          </>
        )}
    </>
  )

  return (
    <ListRow
      onOpen={readOnly ? undefined : () => onEdit(position)}
      leading={leading}
      trailing={amount}
      className={paid ? '[&>button]:opacity-60' : undefined}
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
          {categoryLabel(position.category)} ·{' '}
          {t('common.dueDay', { day: position.dueDay })}
          {position.paymentMethod
            ? ` · ${paymentLabel(position.paymentMethod)}`
            : ''}
          {position.commitmentId ? ` · ${t('budget.fromCommitment')}` : ''}
          {position.isLimit ? ` · ${t('budget.limit')}` : ''}
          {position.passThrough ? ` · ${t('budget.passThrough')}` : ''}
          {position.counterAccountId ? ` · ${t('budget.transfer')}` : ''}
        </span>
    </ListRow>
  )
}
