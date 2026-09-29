import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { MonthBook } from '@/components/MonthBook'
import { i18n } from '@/lib/i18n'
import { flushPendingDelete } from '@/lib/undo-delete'

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
  planYear: 2026,
  planMonth: 9,
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
      <MemoryRouter>
        <MonthBook positions={[]} year={2026} month={9} readOnly={readOnly} />
      </MemoryRouter>
    </QueryClientProvider>
  )
}

let fetchMock: ReturnType<typeof vi.fn>

describe('MonthBook rows', () => {
  beforeEach(() => {
    fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
      if (init?.method === 'DELETE') return new Response(null, { status: 204 })
      return new Response(JSON.stringify(String(url).includes('/accounts') ? [account] : [booking]), {
        status: 200,
      })
    })
    vi.stubGlobal('fetch', fetchMock)
  })
  afterEach(() => vi.unstubAllGlobals())

  const deleted = () => fetchMock.mock.calls.some(([, init]) => init?.method === 'DELETE')

  test('a row opens the booking, named by what it shows, and the amount is its description', async () => {
    renderBook(false)
    const row = await screen.findByRole('button', { name: /Streaming.*Giro/ })
    expect(row).toHaveTextContent('Giro')
    expect(row).toHaveAccessibleDescription(/50,00/)
  })

  test('a row has no delete button and no menu of its own', async () => {
    renderBook(false)
    await screen.findByRole('button', { name: /Streaming/ })
    expect(screen.queryByRole('button', { name: /löschen|weitere Aktionen/ })).not.toBeInTheDocument()
  })

  test('Delete sits in the edit dialog, hides the row at once, sends nothing before the undo window ends and puts the focus on the list heading', async () => {
    const user = userEvent.setup()
    renderBook(false)
    await user.click(await screen.findByRole('button', { name: /Streaming/ }))
    const dialog = screen.getByRole('dialog')
    expect(deleted()).toBe(false)
    await user.click(within(dialog).getByRole('button', { name: i18n.t('common.delete') }))
    await waitFor(() =>
      expect(screen.getByRole('heading', { name: i18n.t('monthBook.title') })).toHaveFocus()
    )
    expect(screen.queryByRole('button', { name: /Streaming/ })).not.toBeInTheDocument()
    expect(deleted()).toBe(false)
    // The window closing sends the request.
    act(() => flushPendingDelete())
    await waitFor(() => expect(deleted()).toBe(true))
  })

  test('the quick entry counts an unplanned booking in the month of today unless another is chosen', async () => {
    const user = userEvent.setup()
    renderBook(false)
    await user.type(await screen.findByLabelText(i18n.t('common.amount')), '12,50')
    await user.click(screen.getByRole('button', { name: i18n.t('monthBook.book') }))
    const post = await waitFor(() => {
      const call = fetchMock.mock.calls.find(([, init]) => init?.method === 'POST')
      expect(call).toBeDefined()
      return JSON.parse(String(call![1].body))
    })
    const now = new Date()
    expect(post.planYear).toBe(now.getFullYear())
    expect(post.planMonth).toBe(now.getMonth() + 1)
  })

  test('a read-only row is not a button and its book cannot delete', async () => {
    renderBook(true)
    expect(await screen.findByText('Streaming')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Streaming/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: i18n.t('common.delete') })).not.toBeInTheDocument()
  })
})

describe('MonthBook carry-over row', () => {
  const carryOver = {
    ...booking,
    id: 't2',
    kind: 'carry_over',
    amount: '120.00',
    note: null,
    category: null,
    budget: null,
  }

  beforeEach(() => {
    fetchMock = vi.fn(
      async (url: string) =>
        new Response(JSON.stringify(String(url).includes('/accounts') ? [account] : [carryOver]), {
          status: 200,
        })
    )
    vi.stubGlobal('fetch', fetchMock)
  })
  afterEach(() => vi.unstubAllGlobals())

  test('it is marked, says where to change it and does not open the edit dialog', async () => {
    const user = userEvent.setup()
    renderBook(false)

    expect(await screen.findByText(i18n.t('monthBook.carryOverName'))).toBeInTheDocument()
    expect(screen.getByText(new RegExp(i18n.t('monthBook.carryOverHint')))).toBeInTheDocument()

    await user.click(screen.getByText(i18n.t('monthBook.carryOverName')))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })
})
