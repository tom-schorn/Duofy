import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, test, vi } from 'vitest'

import { RegisterPage } from '@/pages/RegisterPage'

afterEach(() => vi.unstubAllGlobals())

function open(mode: string, url = '/register') {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    if (String(input).endsWith('/auth/registration')) {
      return new Response(JSON.stringify({ mode }), { status: 200 })
    }
    return new Response(JSON.stringify({ access_token: 't' }), { status: 200 })
  })
  vi.stubGlobal('fetch', fetchMock)
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <MemoryRouter initialEntries={[url]}>
        <RegisterPage />
      </MemoryRouter>
    </QueryClientProvider>
  )
  return fetchMock
}

describe('sign-up page by registration mode', () => {
  test('open: the plain form, no invitation field', async () => {
    open('open')
    expect(await screen.findByLabelText('Vorname')).toBeInTheDocument()
    expect(screen.queryByLabelText('Einladungslink')).not.toBeInTheDocument()
  })

  test('invite: asks for the invitation link and prefills it from the address', async () => {
    open('invite', '/register?invitation=abc123')
    expect(await screen.findByLabelText('Einladungslink')).toHaveValue('abc123')
  })

  test('invite: the pasted link is sent as a bare token', async () => {
    const user = userEvent.setup()
    const fetchMock = open('invite')
    await user.type(await screen.findByLabelText('Einladungslink'), 'https://x.example/register?invitation=tok-1')
    await user.type(screen.getByLabelText('Vorname'), 'A')
    await user.type(screen.getByLabelText('Nachname'), 'B')
    await user.type(screen.getByLabelText('E-Mail'), 'a@example.org')
    await user.type(screen.getByLabelText('Passwort'), 'password-123')
    await user.click(screen.getByRole('button', { name: 'Konto anlegen' }))

    const register = fetchMock.mock.calls.find(([url]) => String(url).endsWith('/auth/register'))
    expect(JSON.parse(String(register?.[1]?.body)).invitation_token).toBe('tok-1')
  })

  test('closed: says so, and keeps the form folded away', async () => {
    open('closed')
    expect(await screen.findByRole('heading', { name: 'Registrierung geschlossen' })).toBeInTheDocument()
    expect(screen.getByText('Konto anlegen', { selector: 'summary' })).toBeInTheDocument()
  })
})
