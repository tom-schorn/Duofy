import { useTranslation } from 'react-i18next'

import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import type { FlowLimitsBy } from '@/lib/domain'
import { useSetFlowLimitsBy } from '@/lib/queries'
import { announce } from '@/lib/undo-delete'

/**
 * The setting for the flow chart — plan or bookings — as its own switch (#253).
 *
 * Lives on the settings page since the flow only names the setting it follows and
 * links here. The one and only place where the value is changed.
 */
export function LimitsSwitch({ value }: { value: FlowLimitsBy }) {
  const { t } = useTranslation()
  const save = useSetFlowLimitsBy()
  return (
    <div className="flex flex-col gap-1.5 print:hidden">
      <Label htmlFor="flow-limits-by">{t('monthFlow.limitsBy')}</Label>
      {/* Not disabled while saving: that would drop the focus. A second choice
          during the save is simply ignored. */}
      <Select
        value={value}
        onValueChange={(next) => {
          if (save.isPending) return
          save.mutate(next as FlowLimitsBy, {
            onSuccess: () =>
              announce(
                'success',
                t(next === 'bookings' ? 'monthFlow.limitsNowBookings' : 'monthFlow.limitsNowPlan')
              ),
          })
        }}
      >
        <SelectTrigger id="flow-limits-by" className="w-56" aria-busy={save.isPending}>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="plan">{t('monthFlow.limitsByPlan')}</SelectItem>
          <SelectItem value="bookings">{t('monthFlow.limitsByBookings')}</SelectItem>
        </SelectContent>
      </Select>
      <p className="text-muted-foreground max-w-[60ch] text-xs">
        {t(value === 'plan' ? 'monthFlow.limitsByPlanHint' : 'monthFlow.limitsByBookingsHint')}
      </p>
    </div>
  )
}
