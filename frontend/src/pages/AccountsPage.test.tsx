import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import '@/lib/i18n'
import { AccountsPage } from '@/pages/AccountsPage'

const giro = {
  id: 'a1',
  name: 'Giro',
  type: 'checking',
  active: true,
  isDefault: true,
  balance: '120.50',
  openingBalance: '100.00',
  openingDate: '2026-01-01',
  countsAsAvailable: true,
  deletable: false,
  externalRef: null,
}

describe('AccountsPage balances (#241)', () => {
  beforeEach(() => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) =>
        new Response(JSON.stringify(String(url).includes('/accounts') ? [giro] : []), {
          status: 200,
        })
      )
    )
  })
  afterEach(() => vi.unstubAllGlobals())

  // The book is no page of its own any more, so the balances of the accounts live
  // where the accounts do: in the row, not in cards above it.
  test('the balance of every account is shown in its row, without cards', async () => {
    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter>
          <AccountsPage />
        </MemoryRouter>
      </QueryClientProvider>
    )
    const row = (await screen.findByRole('button', { name: /^Giro/ })).closest('li')
    expect(row).toHaveTextContent('120,50')
    expect(screen.queryByRole('region')).not.toBeInTheDocument()
  })
})

describe('AccountsPage for the person chosen in the sidebar (#254)', () => {
  const partnerAccount = { ...giro, id: 'a2', name: 'Partnerkonto' }

  beforeEach(() => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        const target = String(url)
        if (!target.includes('/accounts')) return new Response('[]', { status: 200 })
        const rows = target.includes('owner=u2') ? [partnerAccount] : [giro]
        return new Response(JSON.stringify(rows), { status: 200 })
      })
    )
  })
  afterEach(() => vi.unstubAllGlobals())

  test('?member= loads the accounts of that person, not your own', async () => {
    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter initialEntries={['/accounts?member=u2']}>
          <AccountsPage />
        </MemoryRouter>
      </QueryClientProvider>
    )
    expect(await screen.findByText('Partnerkonto')).toBeInTheDocument()
    expect(screen.queryByText('Giro')).not.toBeInTheDocument()
  })
})
