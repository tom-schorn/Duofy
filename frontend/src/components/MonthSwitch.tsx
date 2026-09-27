import { useTranslation } from 'react-i18next'
import { ChevronLeft, ChevronRight } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { monthLabel } from '@/lib/domain'

/**
 * ‹ Month Year › — the one month switch of the app (UI guide, rule "Monatswechsel").
 *
 * It only reports the neighbouring month; where that is kept (state on the book
 * page, the address on the plan page) is the caller's business. The arrows are
 * real buttons, so Tab and Enter work and screen readers hear their names.
 */
export function MonthSwitch({
  year,
  month,
  onChange,
}: {
  year: number
  month: number
  onChange: (year: number, month: number) => void
}) {
  const { t } = useTranslation()

  function shift(by: number) {
    const date = new Date(year, month - 1 + by, 1)
    onChange(date.getFullYear(), date.getMonth() + 1)
  }

  return (
    <div className="flex items-center gap-1" data-print="hide">
      <Button
        variant="ghost"
        size="icon"
        aria-label={t('book.previousMonth')}
        onClick={() => shift(-1)}
      >
        <ChevronLeft className="size-4" />
      </Button>
      <span className="min-w-40 text-center font-medium tabular-nums">
        {monthLabel(month)} {year}
      </span>
      <Button
        variant="ghost"
        size="icon"
        aria-label={t('book.nextMonth')}
        onClick={() => shift(1)}
      >
        <ChevronRight className="size-4" />
      </Button>
    </div>
  )
}
