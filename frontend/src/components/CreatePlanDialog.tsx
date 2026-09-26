import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { DialogFrame } from '@/components/DialogFrame'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { MONTHS, monthLabel } from '@/lib/domain'
import { useCreatePlan } from '@/lib/queries'

/**
 * The dialog that creates a month. `year` and `month` preselect it — the plan page
 * of a month without a plan opens it for exactly that month.
 */
export function CreatePlanDialog({
  ownerId,
  ownerName,
  open,
  onOpenChange,
  year: initialYear,
  month: initialMonth,
}: {
  ownerId: string | null
  ownerName: string | null
  year?: number
  month?: number
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const today = new Date()
  const { t } = useTranslation()
  const [year, setYear] = useState(initialYear ?? today.getFullYear())
  const [month, setMonth] = useState(initialMonth ?? today.getMonth() + 1)
  const create = useCreatePlan()

  // An old error must not greet the next attempt.
  const resetCreate = create.reset
  useEffect(() => {
    if (open) resetCreate()
  }, [open, resetCreate])

  // This year and the next few — planning retroactively rarely makes sense.
  const years = [...new Set([today.getFullYear(), today.getFullYear() + 1, year])].sort()

  return (
    <DialogFrame
      open={open}
      onOpenChange={onOpenChange}
      title={
        ownerName === null
          ? t('plans.create')
          : t('plans.createFor', { name: ownerName })
      }
      description={
        ownerName === null
          ? t('plans.createHint')
          : t('plans.createForHint', { name: ownerName })
      }
      submitLabel={t('common.create')}
      pendingLabel={t('plans.creating')}
      onSubmit={(event) => {
        event.preventDefault()
        create.mutate(
          { year, month, ownerId },
          { onSuccess: () => onOpenChange(false) }
        )
      }}
      dirty={year !== (initialYear ?? today.getFullYear()) || month !== (initialMonth ?? today.getMonth() + 1)}
      pending={create.isPending}
      error={create.isError ? create.error : null}
    >
      <div className="grid grid-cols-2 gap-3">
        <div className="flex flex-col gap-2">
          <Label htmlFor="plan-month">{t('plans.month')}</Label>
          <Select
            value={String(month)}
            onValueChange={(value) => setMonth(Number(value))}
          >
            <SelectTrigger id="plan-month">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {MONTHS.map((month) => (
                <SelectItem key={month} value={String(month)}>
                  {monthLabel(month)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="flex flex-col gap-2">
          <Label htmlFor="plan-year">{t('plans.year')}</Label>
          <Select
            value={String(year)}
            onValueChange={(value) => setYear(Number(value))}
          >
            <SelectTrigger id="plan-year">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {years.map((value) => (
                <SelectItem key={value} value={String(value)}>
                  {value}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>
    </DialogFrame>
  )
}
