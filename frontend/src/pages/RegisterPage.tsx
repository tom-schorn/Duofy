import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { FormEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { useMutation } from '@tanstack/react-query'

import { AuthLayout } from '@/layouts/AuthLayout'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { api, errorText, setToken } from '@/lib/api'
import { invitationToken } from '@/lib/invitation'
import { useRegistrationMode } from '@/lib/queries'

export function RegisterPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const modeQuery = useRegistrationMode()
  const mode = modeQuery.data?.mode

  const [form, setForm] = useState({
    first_name: '',
    last_name: '',
    email: '',
    password: '',
  })
  // Prefilled when the person came in through the invitation link.
  const [invitation, setInvitation] = useState(params.get('invitation') ?? '')

  function set(field: keyof typeof form, value: string) {
    setForm((current) => ({ ...current, [field]: value }))
  }

  const register = useMutation({
    // Register and sign in straight away — otherwise it would mean typing twice.
    mutationFn: async () => {
      await api.post('/auth/register', {
        ...form,
        invitation_token: mode === 'invite' ? invitationToken(invitation) : undefined,
      })
      return api.login(form.email, form.password)
    },
    onSuccess: (data) => {
      setToken(data.access_token)
      navigate('/plan', { replace: true })
    },
  })

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    register.mutate()
  }

  const formElement = (
      <form onSubmit={handleSubmit} className="flex flex-col gap-6">
        <header className="flex flex-col gap-2">
          <h1 className="font-heading text-3xl font-semibold">{t('auth.register.title')}</h1>
          <p className="text-muted-foreground text-sm">
            {t('auth.register.lead')}
          </p>
        </header>

        <div className="flex flex-col gap-4">
          {mode === 'invite' && (
            <div className="flex flex-col gap-2">
              <Label htmlFor="invitation">{t('auth.register.invitationLabel')}</Label>
              <Input
                id="invitation"
                value={invitation}
                onChange={(event) => setInvitation(event.target.value)}
                required
              />
              <p className="text-muted-foreground text-xs">
                {t('auth.register.invitationHint')}
              </p>
            </div>
          )}
          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-2">
              <Label htmlFor="first-name">{t('auth.register.firstName')}</Label>
              <Input
                id="first-name"
                autoComplete="given-name"
                value={form.first_name}
                onChange={(event) => set('first_name', event.target.value)}
                required
              />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="last-name">{t('auth.register.lastName')}</Label>
              <Input
                id="last-name"
                autoComplete="family-name"
                value={form.last_name}
                onChange={(event) => set('last_name', event.target.value)}
                required
              />
            </div>
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="email">{t('auth.email')}</Label>
            <Input
              id="email"
              type="email"
              autoComplete="email"
              placeholder={t('auth.emailPlaceholder')}
              value={form.email}
              onChange={(event) => set('email', event.target.value)}
              required
            />
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="password">{t('auth.password')}</Label>
            <Input
              id="password"
              type="password"
              autoComplete="new-password"
              minLength={8}
              value={form.password}
              onChange={(event) => set('password', event.target.value)}
              required
            />
            <p className="text-muted-foreground text-xs">
              {t('auth.register.passwordRule')}
            </p>
          </div>
        </div>

        {register.isError && (
          <p className="border-destructive bg-destructive/10 text-destructive rounded-md border px-3 py-2 text-sm">
            {errorText(register.error)}
          </p>
        )}

        <Button type="submit" className="w-full" disabled={register.isPending}>
          {register.isPending ? t('auth.register.pending') : t('auth.register.title')}
        </Button>

        <p className="text-muted-foreground text-center text-sm">
          {t('auth.register.hasAccount')}{' '}
          <Link
            to="/login"
            className="text-foreground font-medium underline underline-offset-4"
          >
            {t('auth.register.login')}
          </Link>
        </p>
      </form>
  )

  if (modeQuery.isPending) {
    return (
      <AuthLayout>
        <Skeleton className="h-96 w-full" />
      </AuthLayout>
    )
  }

  // The operator's own address may register even when the door is shut, so the
  // form stays reachable — folded away, because for everybody else it is no use.
  if (mode === 'closed') {
    return (
      <AuthLayout>
        <div className="flex flex-col gap-6">
          <header className="flex flex-col gap-2">
            <h1 className="font-heading text-3xl font-semibold">
              {t('auth.register.closedTitle')}
            </h1>
            <p className="text-muted-foreground text-sm">{t('auth.register.closedLead')}</p>
          </header>
          <Link
            to="/login"
            className="text-foreground text-sm font-medium underline underline-offset-4"
          >
            {t('auth.register.login')}
          </Link>
          <details>
            <summary className="text-muted-foreground cursor-pointer text-xs">
              {t('auth.register.title')}
            </summary>
            <div className="pt-4">{formElement}</div>
          </details>
        </div>
      </AuthLayout>
    )
  }

  return <AuthLayout>{formElement}</AuthLayout>
}
