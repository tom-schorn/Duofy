import { useId } from 'react'
import { useTranslation } from 'react-i18next'

import { coupleQuotas } from '@/lib/domain'

const KEYS = [
  ['targetNeeds', 'quota.needs'],
  ['targetWants', 'quota.wants'],
  ['targetSavings', 'quota.savings'],
] as const

/**
 * The three quotas as coupled sliders: moving one takes the difference from the
 * other two, so the sum is always 100 and nothing has to be corrected by hand.
 *
 * Native range inputs on purpose — arrow keys, Home/End and PageUp/PageDown work
 * without extra code, and screen readers announce them as sliders.
 */
export function QuotaSliders({
  values,
  onChange,
}: {
  values: [number, number, number]
  onChange: (values: [number, number, number]) => void
}) {
  const { t } = useTranslation()
  const prefix = useId()

  return (
    <div className="flex flex-col gap-4">
      {KEYS.map(([key, label], index) => (
        <div key={key} className="flex flex-col gap-1.5">
          <div className="flex items-baseline justify-between gap-4">
            <label htmlFor={`${prefix}-${key}`} className="text-sm font-medium">
              {t(label)}
            </label>
            <output htmlFor={`${prefix}-${key}`} className="text-sm tabular-nums">
              {Math.round(values[index])} %
            </output>
          </div>
          <input
            id={`${prefix}-${key}`}
            type="range"
            min={0}
            max={100}
            step={1}
            value={Math.round(values[index])}
            onChange={(event) =>
              onChange(coupleQuotas(values, index as 0 | 1 | 2, Number(event.target.value)))
            }
            className="accent-primary w-full"
          />
        </div>
      ))}
      <p className="text-muted-foreground text-sm">{t('quota.sumAlways')}</p>
    </div>
  )
}
