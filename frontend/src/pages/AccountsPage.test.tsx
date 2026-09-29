import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { i18n } from '@/lib/i18n'
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
  // where the accounts do.
  test('the balance of every account is shown as a card above the list', async () => {
    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter>
          <AccountsPage />
        </MemoryRouter>
      </QueryClientProvider>
    )
    const cards = await screen.findByRole('region', { name: i18n.t('accountCards.label') })
    expect(cards).toHaveTextContent('120,50')
  })
})
