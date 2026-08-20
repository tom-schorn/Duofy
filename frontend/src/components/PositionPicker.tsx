import { useEffect, useState } from 'react'
import { Check, ChevronDown, ChevronLeft } from 'lucide-react'

import { Button } from '@/components/ui/button'
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover'
import {
  BLOCK_DOT,
  CATEGORY_LABEL,
  MONTH_LABEL,
  euro,
  type PlanPosition,
} from '@/lib/domain'
import { cn } from '@/lib/utils'

/** One month's worth of positions, as the picker offers them. */
export type PositionMonth = {
  year: number
  month: number
  positions: PlanPosition[]
}

/**
 * Choosing a position in two steps: first the month, then the position.
 *
 * ## Why the month is a step of its own
 *
 * A planning month is not a calendar month. Rent leaves the account on the 28th
 * for the month that starts on the 1st, and salary arrives before the month it
 * pays for. So a booking from late August belongs to August's plan about as
 * often as to September's, and one flat list mixing both would show two
 * positions called "Miete" with nothing to tell them apart.
 *
 * Naming the month first makes that the **first** decision instead of a detail
 * hidden in a label — and it keeps each list short enough to aim at.
 *
 * Built like `CategoryPicker`, on purpose: the import screen asks both questions
 * on every row, and two pickers that behave differently on one row are two
 * things to learn.
 */
export function PositionPicker({
  months,
  value,
  onChange,
  disabled = false,
  className,
}: {
  months: PositionMonth[]
  value: string | null
  onChange: (positionId: string | null) => void
  disabled?: boolean
  className?: string
}) {
  const [open, setOpen] = useState(false)
  const [chosenMonth, setChosenMonth] = useState<string | null>(null)

  // Reopening starts at the months again — the same reasoning as in
  // `CategoryPicker`: remembering helps whoever assigns ten rows in a row and
  // misleads everybody else.
  useEffect(() => {
    if (!open) setChosenMonth(null)
  }, [open])

  const all = months.flatMap((entry) => entry.positions)
  const current = all.find((position) => position.id === value)
  const chosen = months.find(
    (entry) => `${entry.year}-${entry.month}` === chosenMonth
  )

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="outline"
          disabled={disabled}
          className={cn(
            'w-full justify-between font-normal',
            current === undefined && 'text-muted-foreground',
            className
          )}
        >
          <span className="truncate">
            {current === undefined ? 'kein Posten' : current.label}
          </span>
          <ChevronDown className="size-4 shrink-0 opacity-50" />
        </Button>
      </PopoverTrigger>

      <PopoverContent align="start" className="w-72 p-0">
        {chosen === undefined ? (
          <ul className="py-1">
            {months.map((entry) => (
              <li key={`${entry.year}-${entry.month}`}>
                <button
                  type="button"
                  disabled={entry.positions.length === 0}
                  onClick={() => setChosenMonth(`${entry.year}-${entry.month}`)}
                  className="hover:bg-accent flex w-full items-center justify-between px-3 py-2 text-sm disabled:opacity-50"
                >
                  <span>
                    {MONTH_LABEL[entry.month - 1]} {entry.year}
                  </span>
                  <span className="text-muted-foreground text-xs tabular-nums">
                    {/* Nothing planned reads as an empty month, not as an
                        error — most people plan one month at a time. */}
                    {entry.positions.length === 0
                      ? 'kein Plan'
                      : entry.positions.length}
                  </span>
                </button>
              </li>
            ))}
            {value !== null && (
              <li className="border-border mt-1 border-t pt-1">
                <button
                  type="button"
                  onClick={() => {
                    onChange(null)
                    setOpen(false)
                  }}
                  className="hover:bg-accent text-muted-foreground w-full px-3 py-2 text-left text-sm"
                >
                  kein Posten
                </button>
              </li>
            )}
          </ul>
        ) : (
          <>
            <button
              type="button"
              onClick={() => setChosenMonth(null)}
              className="hover:bg-accent border-border flex w-full items-center gap-1.5 border-b px-3 py-2 text-sm font-medium"
            >
              <ChevronLeft className="size-4" />
              {MONTH_LABEL[chosen.month - 1]} {chosen.year}
            </button>
            <ul className="max-h-72 overflow-y-auto py-1">
              {chosen.positions.map((position) => (
                <li key={position.id}>
                  <button
                    type="button"
                    onClick={() => {
                      onChange(position.id)
                      setOpen(false)
                    }}
                    className="hover:bg-accent flex w-full items-center gap-2 px-3 py-2 text-left text-sm"
                  >
                    <span
                      className={cn(
                        'size-2 shrink-0 rounded-full',
                        BLOCK_DOT[position.block]
                      )}
                    />
                    <span className="flex min-w-0 flex-col">
                      <span className="truncate">
                        {position.label}
                        {position.isBudget && ' · Budget'}
                      </span>
                      {/* Der Betrag steht dabei, weil zwei Posten derselben
                          Kategorie sich sonst nur im Namen unterscheiden —
                          und genau dann ist der Betrag die Antwort. */}
                      <span className="text-muted-foreground text-xs">
                        {CATEGORY_LABEL[position.category]} ·{' '}
                        {euro.format(Number(position.amountPlanned))}
                      </span>
                    </span>
                    {position.id === value && (
                      <Check className="ml-auto size-4 shrink-0" />
                    )}
                  </button>
                </li>
              ))}
            </ul>
          </>
        )}
      </PopoverContent>
    </Popover>
  )
}
