import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { FormError } from '@/components/FormError'
import { LimitsSwitch } from '@/components/MonthFlow'
import { QueryState } from '@/components/QueryState'
import { QuotaSliders } from '@/components/QuotaSliders'
import { Button } from '@/components/ui/button'
import { useMe, useSetDefaultQuota } from '@/lib/queries'

/**
 * Personal settings (#191): the guideline the next months start from, and what the
 * flow chart counts for limits. Everything here belongs to the signed-in person.
 */
export function SettingsPage() {
  const { t } = useTranslation()
  const me = useMe()
  const save = useSetDefaultQuota()
  const [values, setValues] = useState<[number, number, number]>([50, 30, 20])

  const stored = me.data
  // Start from what is stored, again after a save (the server may round).
  useEffect(() => {
    if (stored) {
      setValues([
        Number(stored.targetNeeds),
        Number(stored.targetWants),
        Number(stored.targetSavings),
      ])
    }
  }, [stored?.targetNeeds, stored?.targetWants, stored?.targetSavings]) // eslint-disable-line react-hooks/exhaustive-deps

  const dirty =
    stored !== undefined &&
    (values[0] !== Number(stored.targetNeeds) ||
      values[1] !== Number(stored.targetWants) ||
      values[2] !== Number(stored.targetSavings))

  return (
    <div className="flex max-w-2xl flex-col gap-8">
      <header className="flex flex-col gap-2">
        <h1 className="font-heading text-2xl font-semibold">{t('settings.title')}</h1>
        <p className="text-muted-foreground text-sm">{t('settings.lead')}</p>
      </header>

      <QueryState isPending={me.isPending} error={me.error}>
        {me.data && (
          <>
            <form
              className="flex flex-col gap-4"
              onSubmit={(event) => {
                event.preventDefault()
                save.mutate({
                  targetNeeds: String(values[0]),
                  targetWants: String(values[1]),
                  targetSavings: String(values[2]),
                })
              }}
            >
              <h2 className="font-medium">{t('settings.quotaTitle')}</h2>
              <p className="text-muted-foreground text-sm">{t('quota.userDescription')}</p>
              <QuotaSliders values={values} onChange={setValues} />
              <FormError error={save.isError ? save.error : null} />
              <div>
                <Button type="submit" disabled={!dirty || save.isPending}>
                  {save.isPending ? t('common.saving') : t('common.save')}
                </Button>
              </div>
            </form>

            <section className="flex flex-col gap-3">
              <h2 className="font-medium">{t('settings.flowTitle')}</h2>
              <LimitsSwitch value={me.data.flowLimitsBy} />
            </section>
          </>
        )}
      </QueryState>
    </div>
  )
}
