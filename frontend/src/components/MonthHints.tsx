import { Info } from 'lucide-react'
import { useTranslation } from 'react-i18next'

import { euro, type PlanHint } from '@/lib/domain'

/**
 * The hints that concern the whole month (no position), shown under the figures.
 *
 * A code this version does not know is ignored, never shown raw. The hint
 * "nothing free" has two wordings: exactly used up, or planned beyond the income.
 */
export function MonthHints({ hints }: { hints: PlanHint[] }) {
  const { t, i18n } = useTranslation()
  const shown = hints.filter(
    (hint) => hint.positionId === null && i18n.exists(`hints.${hint.code}`)
  )
  if (shown.length === 0) return null

  return (
    <ul className="flex flex-col gap-2">
      {shown.map((hint) => {
        const free = Number(hint.params.free ?? 0)
        const key =
          hint.code === 'plan_nothing_free' && free < 0
            ? 'hints.plan_nothing_free_over'
            : `hints.${hint.code}`
        return (
          <li
            key={hint.code}
            className="text-muted-foreground flex items-center gap-2 text-sm"
          >
            <Info className="size-4 shrink-0" aria-hidden="true" />
            {t(key, { amount: euro.format(Math.abs(free)) })}
          </li>
        )
      })}
    </ul>
  )
}
