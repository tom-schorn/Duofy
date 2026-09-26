import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { CarryOverCard } from '@/components/CarryOverCard'
import { MonthFlow } from '@/components/MonthFlow'
import { i18n } from '@/lib/i18n'
import type { Account, Transaction } from '@/lib/domain'

const account = {
  id: 'a1',
  name: 'Giro',
  active: true,
  isDefault: true,
  type: 'checking',
  balance: '0.00',
} as Account

const carryOver = {
  id: 't1',
  kind: 'carry_over',
  accountId: 'a1',
  counterAccountId: null,
  occurredOn: '2026-10-01',
  amount: '-120.00',
  note: null,
  category: null,
  budget: null,
  positionId: null,
  autoBooked: false,
  externalRef: null,
} as Transaction

function renderCard(existing: Transaction | undefined) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={client}>
      <CarryOverCard account={account} carryOver={existing} year={2026} month={10} />
    </QueryClientProvider>
  )
}

let fetchMock: ReturnType<typeof vi.fn>

describe('CarryOverCard', () => {
  beforeEach(() => {
    fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
      if (init?.method === 'POST') return new Response(JSON.stringify(carryOver), { status: 201 })
      if (String(url).includes('carry-over-suggestion')) {
        return new Response(JSON.stringify({ amount: '950.00' }), { status: 200 })
      }
      return new Response('[]', { status: 200 })
    })
    vi.stubGlobal('fetch', fetchMock)
  })
  afterEach(() => vi.unstubAllGlobals())

  test('without a carry-over it says the curve starts at zero', () => {
    renderCard(undefined)
    expect(screen.getByText(i18n.t('carryOver.notSet', { account: 'Giro' }))).toBeInTheDocument()
    expect(screen.getByText(i18n.t('carryOver.notSetHint'))).toBeInTheDocument()
  })

  test('the dialog offers the book balance and saves a carry-over on the first of the month', async () => {
    const user = userEvent.setup()
    renderCard(undefined)
    await user.click(screen.getByRole('button', { name: i18n.t('carryOver.setButton') }))

    const field = await screen.findByLabelText(i18n.t('carryOver.amount'))
    await waitFor(() => expect(field).toHaveValue('950,00'))

    await user.click(screen.getByRole('button', { name: i18n.t('common.save') }))

    await waitFor(() => {
      const post = fetchMock.mock.calls.find(([, init]) => init?.method === 'POST')
      expect(post).toBeDefined()
      expect(JSON.parse(post![1].body)).toEqual({
        kind: 'carry_over',
        accountId: 'a1',
        occurredOn: '2026-10-01',
        amount: '950.00',
      })
    })
  })

  test('a set carry-over shows its amount and can be deleted from its dialog', async () => {
    const user = userEvent.setup()
    renderCard(carryOver)
    expect(screen.getByText(/Giro startet mit/)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: i18n.t('carryOver.change') }))

    expect(await screen.findByRole('button', { name: i18n.t('common.delete') })).toBeInTheDocument()
    // Only a new carry-over asks for a suggestion.
    expect(
      fetchMock.mock.calls.some(([url]) => String(url).includes('carry-over-suggestion'))
    ).toBe(false)
  })
})

describe('MonthFlow with a carry-over', () => {
  const rent = {
    id: 'p1',
    label: 'Miete',
    amountPlanned: '300.00',
    budget: 'needs',
    dueDay: 5,
  } as never

  test('an account going below zero from a small carry-over is called overdrawn', () => {
    render(<MonthFlow positions={[rent]} year={2026} month={10} startBalance={100} />)
    expect(screen.getByText(/im Minus/)).toBeInTheDocument()
  })

  test('a carry-over that covers the month keeps it self-carrying', () => {
    render(<MonthFlow positions={[rent]} year={2026} month={10} startBalance={500} />)
    expect(screen.getByText(i18n.t('monthFlow.selfCarrying'))).toBeInTheDocument()
  })
})
