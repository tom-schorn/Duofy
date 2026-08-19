import { useState } from 'react'
import { ChevronLeft, ChevronRight } from 'lucide-react'

import { AccountCards } from '@/components/AccountCards'
import { BookFlow } from '@/components/BookFlow'
import { MonthBook } from '@/components/MonthBook'
import { Button } from '@/components/ui/button'
import { useActiveMember } from '@/hooks/use-active-member'
import { MONTH_LABEL, OWN_SCOPE, atLeast, type BookScope } from '@/lib/domain'
import { usePlan } from '@/lib/queries'

/**
 * The book, on its own.
 *
 * It used to live inside a month plan, which tied it to something it does not
 * depend on: a booking belongs to an **account**, not to a plan. The
 * consequence was worse than untidy — bookings in a month nobody had planned
 * were invisible while still moving the balances, which is exactly what an
 * import produces.
 *
 * So: its own page, its own month picker, and a plan that may or may not exist.
 * Without one there is nothing to assign a booking to, and everything else
 * works as before.
 */
export function BookPage() {
  const active = useActiveMember()
  const mayEdit = atLeast(active.levelFor('accounts'), 'edit')

  const today = new Date()
  const [year, setYear] = useState(today.getFullYear())
  const [month, setMonth] = useState(today.getMonth() + 1)

  const scope: BookScope =
    active.id === null ? OWN_SCOPE : { kind: 'member', ownerId: active.id }

  // A month without a plan is a normal answer here, not a failure — the whole
  // point of the page is that the book does not need one.
  const plan = usePlan(year, month, true, active.id)
  const positions = plan.data?.positions ?? []

  function shift(by: number) {
    const date = new Date(year, month - 1 + by, 1)
    setYear(date.getFullYear())
    setMonth(date.getMonth() + 1)
  }

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-col gap-2">
          <h1 className="font-heading text-3xl font-semibold">Buch</h1>
          <p className="text-muted-foreground">
            {active.member === null
              ? 'Was tatsächlich gelaufen ist — Kontostände und jede Buchung darauf.'
              : `Das Buch von ${active.member.firstName}.`}
          </p>
        </div>

        <div className="flex items-center gap-1">
          <Button variant="ghost" size="icon" onClick={() => shift(-1)}>
            <ChevronLeft className="size-4" />
          </Button>
          <span className="min-w-40 text-center font-medium tabular-nums">
            {MONTH_LABEL[month - 1]} {year}
          </span>
          <Button variant="ghost" size="icon" onClick={() => shift(1)}>
            <ChevronRight className="size-4" />
          </Button>
        </div>
      </header>

      <AccountCards scope={scope} />

      <BookFlow year={year} month={month} scope={scope} />

      {plan.data === undefined && !plan.isPending && (
        <p className="text-muted-foreground border-border rounded-lg border border-dashed px-4 py-3 text-sm">
          Für diesen Monat gibt es keinen Plan. Buchen geht trotzdem — nur
          zuordnen lässt sich nichts, solange keine Posten da sind.
        </p>
      )}

      <MonthBook
        positions={positions}
        year={year}
        month={month}
        scope={scope}
        readOnly={!mayEdit}
      />
    </div>
  )
}
