import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { DialogFrame } from '@/components/DialogFrame'
import { QuotaSliders } from '@/components/QuotaSliders'
import type { QuotaValues } from '@/lib/domain'

function numbers(values: QuotaValues): [number, number, number] {
  return [Number(values.targetNeeds), Number(values.targetWants), Number(values.targetSavings)]
}

/**
 * The three quotas: needs, wants and savings.
 *
 * One dialog for the household's shared target; the personal default lives on the
 * settings page. Coupled sliders, so the three always add up to 100 — the server
 * insists on it too.
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
  const [values, setValues] = useState(numbers(initial))

  // Every opening starts from what is stored; closing without saving discards.
  useEffect(() => {
    if (open) setValues(numbers(initial))
    // Only when it opens: a reload behind the dialog must not overwrite the sliders.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  const stored = numbers(initial)

  return (
    <DialogFrame
      open={open}
      onOpenChange={onOpenChange}
      title={title}
      description={description}
      submitLabel={t('common.save')}
      dirty={values.some((value, index) => value !== stored[index])}
      pending={pending}
      error={error}
      onSubmit={(event) => {
        event.preventDefault()
        onSave({
          targetNeeds: String(values[0]),
          targetWants: String(values[1]),
          targetSavings: String(values[2]),
        })
      }}
    >
      <QuotaSliders values={values} onChange={setValues} />
    </DialogFrame>
  )
}
