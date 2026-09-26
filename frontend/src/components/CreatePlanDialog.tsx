import { useState } from 'react'
import { useTranslation } from 'react-i18next'

import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { errorText } from '@/lib/api'
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

  // This year and the next few — planning retroactively rarely makes sense.
  const years = [...new Set([today.getFullYear(), today.getFullYear() + 1, year])].sort()

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <form
          onSubmit={(event) => {
            event.preventDefault()
            create.mutate(
              { year, month, ownerId },
              { onSuccess: () => onOpenChange(false) }
            )
          }}
          className="flex flex-col gap-5"
        >
          <DialogHeader>
            <DialogTitle className="font-heading text-xl">
              {ownerName === null
                ? t('plans.create')
                : t('plans.createFor', { name: ownerName })}
            </DialogTitle>
            <DialogDescription>
              {ownerName === null
                ? t('plans.createHint')
                : t('plans.createForHint', { name: ownerName })}
            </DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-2">
              <Label>{t('plans.month')}</Label>
              <Select
                value={String(month)}
                onValueChange={(value) => setMonth(Number(value))}
              >
                <SelectTrigger>
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
              <Label>{t('plans.year')}</Label>
              <Select
                value={String(year)}
                onValueChange={(value) => setYear(Number(value))}
              >
                <SelectTrigger>
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

          {create.isError && (
            <p className="border-destructive bg-destructive/10 text-destructive rounded-md border px-3 py-2 text-sm">
              {errorText(create.error)}
            </p>
          )}

          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => onOpenChange(false)}
            >
              {t('common.cancel')}
            </Button>
            <Button type="submit" disabled={create.isPending}>
              {create.isPending ? t('plans.creating') : t('common.create')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
