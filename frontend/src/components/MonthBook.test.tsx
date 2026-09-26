import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { MonthBook } from '@/components/MonthBook'

const booking = {
  id: 't1',
  accountId: 'a1',
  counterAccountId: null,
  occurredOn: '2026-09-05',
  amount: '50.00',
  note: 'Streaming',
  category: 'leisure.subscriptions',
  budget: 'wants',
  positionId: null,
  autoBooked: false,
  externalRef: null,
}
const account = {
  id: 'a1',
  name: 'Giro',
  active: true,
  isDefault: true,
  type: 'checking',
  balance: '0.00',
}

function renderBook(readOnly: boolean) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={client}>
      <MonthBook positions={[]} year={2026} month={9} readOnly={readOnly} />
    </QueryClientProvider>
  )
}

describe('MonthBook rows', () => {
  beforeEach(() => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) =>
        new Response(JSON.stringify(String(url).includes('/accounts') ? [account] : [booking]), {
          status: 200,
        })
      )
    )
  })
  afterEach(() => vi.unstubAllGlobals())

  test('a row opens the booking, named by what it shows', async () => {
    renderBook(false)
    const row = await screen.findByRole('button', { name: /Streaming.*Giro/ })
    expect(row).toHaveTextContent('Giro')
    expect(row).toHaveTextContent('50,00')
  })

  test('a read-only row is not a button', async () => {
    renderBook(true)
    expect(await screen.findByText('Streaming')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Streaming/ })).not.toBeInTheDocument()
  })
})
