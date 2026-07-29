import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router'
import { useMutation } from '@tanstack/react-query'

import { AuthLayout } from '@/layouts/AuthLayout'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { api, errorText, setToken } from '@/lib/api'

export function LoginPage() {
  const navigate = useNavigate()
  const location = useLocation()

  // Wohin es nach der Anmeldung gehen soll — der Schutz merkt sich den Pfad.
  const from = (location.state as { from?: string } | null)?.from ?? '/plan'

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')

  const login = useMutation({
    mutationFn: () => api.login(email, password),
    onSuccess: (data) => {
      setToken(data.access_token)
      navigate(from, { replace: true })
    },
  })

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    login.mutate()
  }

  return (
    <AuthLayout>
      <form onSubmit={handleSubmit} className="flex flex-col gap-6">
        <header className="flex flex-col gap-2">
          <h1 className="font-heading text-3xl font-semibold">
            Willkommen zurück
          </h1>
          <p className="text-muted-foreground text-sm">
            Plan den Monat, bevor er anfängt.
          </p>
        </header>

        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-2">
            <Label htmlFor="email">E-Mail</Label>
            <Input
              id="email"
              name="email"
              type="email"
              autoComplete="email"
              placeholder="tom@beispiel.de"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              required
            />
          </div>

          <div className="flex flex-col gap-2">
            <div className="flex items-baseline justify-between">
              <Label htmlFor="password">Passwort</Label>
              {/* TODO: Route /passwort-vergessen bauen. Das Backend hat den
                  Reset-Endpunkt schon (fastapi-users), die Seite fehlt. */}
              <Link
                to="#"
                className="text-muted-foreground hover:text-foreground text-xs underline underline-offset-4"
              >
                Passwort vergessen?
              </Link>
            </div>
            <Input
              id="password"
              name="password"
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              required
            />
          </div>
        </div>

        {login.isError && (
          <p className="border-destructive bg-destructive/10 text-destructive rounded-md border px-3 py-2 text-sm">
            {errorText(login.error)}
          </p>
        )}

        <Button type="submit" className="w-full" disabled={login.isPending}>
          {login.isPending ? 'Wird geprüft…' : 'Anmelden'}
        </Button>

        <p className="text-muted-foreground text-center text-sm">
          Noch kein Konto?{' '}
          <Link
            to="/register"
            className="text-foreground font-medium underline underline-offset-4"
          >
            Registrieren
          </Link>
        </p>
      </form>
    </AuthLayout>
  )
}
