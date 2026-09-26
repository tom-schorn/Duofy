import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, test, vi } from 'vitest'

import { AdminPage } from '@/pages/AdminPage'

afterEach(() => vi.unstubAllGlobals())

function open(isSuperuser: boolean) {
  const invitation = {
    id: '1',
    token: 'tok',
    email: null,
    createdAt: '2026-09-26T10:00:00Z',
    expiresAt: '2026-10-03T10:00:00Z',
  }
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input)
    if (url.endsWith('/users/me')) {
      return new Response(JSON.stringify({ id: 'u', email: 'a@example.org', firstName: 'A', lastName: 'B', isSuperuser }))
    }
    if (url.endsWith('/auth/registration')) return new Response(JSON.stringify({ mode: 'invite' }))
    if (init?.method === 'DELETE') return new Response(null, { status: 204 })
    return new Response(JSON.stringify([invitation]))
  })
  vi.stubGlobal('fetch', fetchMock)
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <MemoryRouter initialEntries={['/admin']}>
        <AdminPage />
      </MemoryRouter>
    </QueryClientProvider>
  )
  return fetchMock
}

describe('admin page', () => {
  test('lists open invitations and revokes one', async () => {
    const user = userEvent.setup()
    const fetchMock = open(true)
    expect(await screen.findByText('Für jede Adresse')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Widerrufen' }))

    expect(fetchMock.mock.calls.some(([url, init]) => String(url).endsWith('/admin/invitations/1') && init?.method === 'DELETE')).toBe(true)
  })

  test('a plain user does not get to see it', async () => {
    open(false)
    await vi.waitFor(() => expect(screen.queryByText('Instanz verwalten')).not.toBeInTheDocument())
  })
})
