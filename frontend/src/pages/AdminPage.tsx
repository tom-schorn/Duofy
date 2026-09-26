import { useState } from 'react'
import type { FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Navigate } from 'react-router'
import { Copy } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { FormError } from '@/components/FormError'
import { QueryState } from '@/components/QueryState'
import { announce } from '@/lib/undo-delete'
import { invitationLink } from '@/lib/invitation'
import {
  useCreateInstanceInvitation,
  useInstanceInvitations,
  useMe,
  useRegistrationMode,
  useRevokeInstanceInvitation,
} from '@/lib/queries'

/**
 * The system level: who may come onto this instance.
 *
 * Only for admins. Nothing here shows anyone's plans, books or accounts — the
 * admin runs the instance, they do not look into it (#168).
 */
export function AdminPage() {
  const { t } = useTranslation()
  const me = useMe()
  const mode = useRegistrationMode().data?.mode
  const invitations = useInstanceInvitations()
  const create = useCreateInstanceInvitation()
  const revoke = useRevokeInstanceInvitation()
  const [email, setEmail] = useState('')

  // The backend refuses anyone else anyway; this only spares them an empty page.
  if (me.data && !me.data.isSuperuser) return <Navigate to="/plan" replace />

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    create.mutate({ email: email.trim() || null }, { onSuccess: () => setEmail('') })
  }

  async function copy(token: string) {
    try {
      await navigator.clipboard.writeText(invitationLink(window.location.origin, token))
      announce('success', t('toast.linkCopied'))
    } catch {
      // Clipboard blocked (plain http, permissions): nothing to do but say so.
      announce('error', t('errors.unknown'))
    }
  }

  return (
    <div className="flex max-w-2xl flex-col gap-8">
      <header className="flex flex-col gap-2">
        <h1 className="font-heading text-2xl font-semibold">{t('admin.title')}</h1>
        <p className="text-muted-foreground text-sm">{t('admin.lead')}</p>
        {mode && (
          <p className="text-sm">
            {t(mode === 'open' ? 'admin.modeOpen' : mode === 'invite' ? 'admin.modeInvite' : 'admin.modeClosed')}
          </p>
        )}
      </header>

      <form onSubmit={handleSubmit} className="flex flex-col gap-3">
        <h2 className="font-medium">{t('admin.createTitle')}</h2>
        <div className="flex flex-col gap-2">
          <Label htmlFor="admin-invite-email">{t('admin.emailLabel')}</Label>
          <Input
            id="admin-invite-email"
            type="email"
            placeholder={t('auth.emailPlaceholder')}
            value={email}
            onChange={(event) => setEmail(event.target.value)}
          />
          <p className="text-muted-foreground text-xs">{t('admin.emailHint')}</p>
        </div>
        <FormError error={create.isError ? create.error : null} />
        <Button type="submit" className="self-start" disabled={create.isPending}>
          {create.isPending ? t('admin.creating') : t('admin.create')}
        </Button>
      </form>

      <section className="flex flex-col gap-3">
        <h2 className="font-medium">{t('admin.openTitle')}</h2>
        <QueryState isPending={invitations.isPending} error={invitations.error}>
          {(invitations.data ?? []).length === 0 ? (
              <p className="text-muted-foreground text-sm">{t('admin.empty')}</p>
            ) : (
              <ul className="flex flex-col divide-y rounded-lg border">
                {(invitations.data ?? []).map((invitation) => (
                  <li
                    key={invitation.id}
                    className="flex flex-wrap items-center justify-between gap-2 px-4 py-3"
                  >
                    <div className="flex flex-col">
                      <span className="text-sm font-medium">
                        {invitation.email ?? t('admin.anyone')}
                      </span>
                      <span className="text-muted-foreground text-xs">
                        {t('admin.validUntil', {
                          date: new Date(invitation.expiresAt).toLocaleDateString('de-DE'),
                        })}
                      </span>
                    </div>
                    <div className="flex gap-2">
                      <Button variant="outline" size="sm" onClick={() => copy(invitation.token)}>
                        <Copy className="size-4" />
                        {t('admin.copy')}
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        disabled={revoke.isPending}
                        onClick={() => revoke.mutate(invitation.id)}
                      >
                        {t('admin.revoke')}
                      </Button>
                    </div>
                  </li>
                ))}
              </ul>
            )}
        </QueryState>
      </section>
    </div>
  )
}
