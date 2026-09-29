import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router'

import { FormError } from '@/components/FormError'
import { LimitsSwitch } from '@/components/LimitsSwitch'
import { QueryState } from '@/components/QueryState'
import { QuotaSliders } from '@/components/QuotaSliders'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { api, clearToken } from '@/lib/api'
import { useMe, useSetDefaultQuota } from '@/lib/queries'

/**
 * Personal settings (#191): the guideline the next months start from, and what the
 * flow chart counts for limits. Everything here belongs to the signed-in person.
 */
export function SettingsPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const client = useQueryClient()
  const me = useMe()
  const save = useSetDefaultQuota()
  const [values, setValues] = useState<[number, number, number]>([50, 30, 20])
  const [confirming, setConfirming] = useState(false)

  // The account is gone once this succeeds — clear it locally too and leave, the
  // way UserMenu's logout does. No toast: the login page is the next thing seen.
  const deleteAccount = useMutation({
    mutationFn: () => api.delete<void>('/users/me'),
    onSuccess: () => {
      clearToken()
      client.clear()
      navigate('/login', { replace: true })
    },
  })

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
              <p className="text-muted-foreground text-sm">{t('settings.flowLead')}</p>
              <LimitsSwitch value={me.data.flowLimitsBy} />
            </section>

            <section className="flex flex-col gap-3">
              <h2 className="font-medium">{t('settings.dangerTitle')}</h2>
              <p className="text-muted-foreground text-sm">{t('settings.deleteAccountLead')}</p>
              <div>
                <Button
                  type="button"
                  variant="outline"
                  className="text-destructive"
                  onClick={() => setConfirming(true)}
                >
                  {t('settings.deleteAccount')}
                </Button>
              </div>

              <AlertDialog open={confirming} onOpenChange={setConfirming}>
                <AlertDialogContent>
                  <AlertDialogHeader>
                    <AlertDialogTitle className="font-heading">
                      {t('settings.deleteAccountTitle')}
                    </AlertDialogTitle>
                    <AlertDialogDescription>
                      {t('settings.deleteAccountText')}
                    </AlertDialogDescription>
                  </AlertDialogHeader>
                  <AlertDialogFooter>
                    <AlertDialogCancel>{t('common.cancel')}</AlertDialogCancel>
                    <AlertDialogAction
                      disabled={deleteAccount.isPending}
                      onClick={(event) => {
                        // Stays open until the server has said yes.
                        event.preventDefault()
                        deleteAccount.mutate()
                      }}
                    >
                      {t('settings.deleteAccount')}
                    </AlertDialogAction>
                  </AlertDialogFooter>
                  {deleteAccount.isError && <FormError error={deleteAccount.error} />}
                </AlertDialogContent>
              </AlertDialog>
            </section>
          </>
        )}
      </QueryState>
    </div>
  )
}
