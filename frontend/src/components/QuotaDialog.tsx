import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { DialogFrame } from '@/components/DialogFrame'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { QuotaValues } from '@/lib/domain'

const FIELDS = [
  ['targetNeeds', 'quota.needs'],
  ['targetWants', 'quota.wants'],
  ['targetSavings', 'quota.savings'],
  ['bufferPercent', 'quota.buffer'],
] as const

/** Digits with at most two decimals, comma or point: no "1e2", no "0x10". */
const PERCENT = /^\d+([.,]\d{1,2})?$/

/** Percent as typed; NaN while it is not one. */
function percent(text: string): number {
  const trimmed = text.trim()
  return PERCENT.test(trimmed) ? Number(trimmed.replace(',', '.')) : NaN
}

/** Rounded to two decimals, so 33.33 + 33.33 + 33.34 compares as exactly 100. */
function cents(value: number): number {
  return Math.round(value * 100) / 100
}

/** `50.00` from the API becomes `50` in the field. */
function show(value: string): string {
  return String(Number(value))
}

/**
 * The four numbers of a quota: needs, wants, savings and the buffer.
 *
 * One dialog for the personal default and for the household's shared target — they
 * are the same four numbers with a different owner. The three quotas must add up to
 * 100, which the server also insists on; the sum is shown while typing.
 */
export function QuotaDialog({
  open,
  onOpenChange,
  title,
  description,
  initial,
  pending,
  error,
  onSave,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  title: string
  description: string
  initial: QuotaValues
  pending: boolean
  error: unknown
  onSave: (values: QuotaValues) => void
}) {
  const { t } = useTranslation()
  const [text, setText] = useState<QuotaValues>(initial)

  // Every opening starts from what is stored; closing without saving discards.
  useEffect(() => {
    if (open) {
      setText({
        targetNeeds: show(initial.targetNeeds),
        targetWants: show(initial.targetWants),
        targetSavings: show(initial.targetSavings),
        bufferPercent: show(initial.bufferPercent),
      })
    }
    // Only when it opens: a reload behind the dialog must not overwrite typing.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  const numbers = FIELDS.map(([key]) => percent(text[key]))
  const valid = numbers.every((n) => n >= 0 && n <= 100)
  const sum = cents(numbers[0] + numbers[1] + numbers[2])
  const adds = valid && sum === 100

  return (
    <DialogFrame
      open={open}
      onOpenChange={onOpenChange}
      title={title}
      description={description}
      submitLabel={t('common.save')}
      submitDisabled={!adds}
      dirty={FIELDS.some(([key]) => text[key] !== show(initial[key]))}
      pending={pending}
      error={error}
      onSubmit={(event) => {
        event.preventDefault()
        onSave({
          targetNeeds: String(percent(text.targetNeeds)),
          targetWants: String(percent(text.targetWants)),
          targetSavings: String(percent(text.targetSavings)),
          bufferPercent: String(percent(text.bufferPercent)),
        })
      }}
    >
      <div className="grid grid-cols-2 gap-4">
        {FIELDS.map(([key, label]) => (
          <div key={key} className="flex flex-col gap-2">
            <Label htmlFor={`quota-${key}`}>{t(label)}</Label>
            <Input
              id={`quota-${key}`}
              inputMode="decimal"
              value={text[key]}
              onChange={(event) => setText({ ...text, [key]: event.target.value })}
            />
          </div>
        ))}
      </div>
      <p
        className={adds ? 'text-muted-foreground text-sm' : 'text-destructive text-sm'}
        role={adds ? undefined : 'alert'}
      >
        {adds ? t('quota.sum', { sum: String(sum) }) : t('quota.mustSum')}
      </p>
    </DialogFrame>
  )
}
