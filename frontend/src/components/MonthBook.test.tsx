import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { useState } from 'react'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { MonthBook, type BookFilter } from '@/components/MonthBook'
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
  // What the server says about it (#254): the page reads these, it does not work
  // them out.
  unplanned: true,
  countsElsewhere: false,
}
const account = {
  id: 'a1',
  name: 'Giro',
  active: true,
  isDefault: true,
  type: 'checking',
  balance: '0.00',
}

/** Holds the filter like the plan page does, in its address. */
function Book({
  readOnly,
  positions,
  initial,
}: {
  readOnly: boolean
  positions: unknown[]
  initial: BookFilter
}) {
  const [filter, setFilter] = useState<BookFilter>(initial)
  return (
    <MonthBook
      positions={positions as never}
      year={2026}
      month={9}
      readOnly={readOnly}
      filter={filter}
      onFilterChange={setFilter}
    />
  )
}

function renderBook(readOnly: boolean, positions: unknown[] = [], initial: BookFilter = 'all') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <Book readOnly={readOnly} positions={positions} initial={initial} />
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

  test('the book is a list only: the quick entry now lives in the add dialog', async () => {
    renderBook(false)
    await screen.findByRole('button', { name: /Streaming/ })
    expect(screen.queryByLabelText(i18n.t('common.amount'))).not.toBeInTheDocument()
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
    unplanned: false,
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

describe('MonthBook list and filter (#241)', () => {
  const account2 = { ...account, id: 'a2', name: 'Kreditkarte', isDefault: false }
  const rent = { id: 'p1', label: 'Miete', category: 'housing.rent', budget: 'needs' }
  const planned = {
    ...booking,
    id: 't-rent',
    occurredOn: '2026-09-01',
    note: 'Miete Sept',
    amount: '950.00',
    category: 'housing.rent',
    budget: 'needs',
    positionId: 'p1',
    unplanned: false,
  }
  const unplannedLate = {
    ...booking,
    id: 't-late',
    occurredOn: '2026-09-20',
    note: 'Kino',
    amount: '35.00',
  }
  const unplannedEarly = {
    ...booking,
    id: 't-early',
    occurredOn: '2026-09-03',
    note: 'Apotheke',
    amount: '18.90',
    accountId: 'a2',
  }
  const salary = {
    ...booking,
    id: 't-salary',
    occurredOn: '2026-08-25',
    note: 'Gehalt',
    amount: '3200.00',
    category: 'income.earned',
    budget: 'income',
    planMonth: 9,
    countsElsewhere: true,
  }
  const transfer = {
    ...booking,
    id: 't-transfer',
    occurredOn: '2026-09-10',
    note: 'Tagesgeld',
    counterAccountId: 'a2',
    category: null,
    budget: null,
    unplanned: false,
  }

  beforeEach(() => {
    fetchMock = vi.fn(async (url: string) => {
      const rows = [unplannedLate, planned, transfer, unplannedEarly, salary]
      return new Response(
        JSON.stringify(String(url).includes('/accounts') ? [account, account2] : rows),
        { status: 200 }
      )
    })
    vi.stubGlobal('fetch', fetchMock)
  })
  afterEach(() => vi.unstubAllGlobals())

  const NAMES = /Gehalt|Miete Sept|Apotheke|Tagesgeld|Kino/
  const names = () =>
    screen
      .getAllByRole('button', { name: NAMES })
      .filter((row) => row.hasAttribute('data-row-open'))
      .map((row) => NAMES.exec(row.textContent ?? '')![0])

  test('rows come by date, oldest first, by their own date not the plan month', async () => {
    renderBook(false, [rent])
    await screen.findByRole('button', { name: /Kino/ })
    expect(names()).toEqual(['Gehalt', 'Miete Sept', 'Apotheke', 'Tagesgeld', 'Kino'])
  })

  test('a booking dated in another month than it counts in says which month it counts in', async () => {
    renderBook(false, [rent])
    const row = await screen.findByRole('button', { name: /Gehalt/ })
    expect(row).toHaveTextContent(/für Sep/)
    expect(screen.getByRole('button', { name: /Kino/ })).not.toHaveTextContent(/für /)
  })

  test('the filter offers Alle and Ungeplant with the number of unplanned bookings', async () => {
    renderBook(false, [rent])
    await screen.findByRole('button', { name: /Kino/ })
    expect(screen.getByRole('button', { name: 'Alle' })).toHaveAttribute('aria-pressed', 'true')
    // Salary, Apotheke and Kino hang on no position; the transfer is no spending.
    expect(screen.getByRole('button', { name: 'Ungeplant · 3' })).toHaveAttribute(
      'aria-pressed',
      'false'
    )
  })

  test('Ungeplant shows only the bookings without a position and no transfer', async () => {
    const user = userEvent.setup()
    renderBook(false, [rent])
    await screen.findByRole('button', { name: /Kino/ })
    await user.click(screen.getByRole('button', { name: 'Ungeplant · 3' }))
    expect(names()).toEqual(['Gehalt', 'Apotheke', 'Kino'])
    await user.click(screen.getByRole('button', { name: 'Alle' }))
    expect(names()).toHaveLength(5)
  })

  test('the address can ask for the unplanned filter at once', async () => {
    renderBook(false, [rent], 'unplanned')
    await screen.findByRole('button', { name: /Kino/ })
    expect(screen.queryByRole('button', { name: /Miete Sept/ })).not.toBeInTheDocument()
  })

  test('an unplanned row offers zuordnen, which opens the booking at its position', async () => {
    const user = userEvent.setup()
    renderBook(false, [rent])
    await screen.findByRole('button', { name: /Kino/ })
    const assign = screen.getAllByRole('button', { name: /zuordnen/ })
    expect(assign).toHaveLength(3)
    await user.click(assign[2])
    const dialog = await screen.findByRole('dialog')
    expect(
      within(dialog).getByRole('combobox', { name: i18n.t('monthBook.positionLabel') })
    ).toBeInTheDocument()
  })

  test('a read-only book offers no zuordnen', async () => {
    renderBook(true, [rent])
    await screen.findByText('Kino')
    expect(screen.queryByRole('button', { name: /zuordnen/ })).not.toBeInTheDocument()
  })

  test('the row shows account and position', async () => {
    renderBook(false, [rent])
    const row = await screen.findByRole('button', { name: /Miete Sept/ })
    expect(row).toHaveTextContent('Giro')
    expect(row).toHaveTextContent('Miete')
    expect(screen.getByRole('button', { name: /Apotheke/ })).toHaveTextContent('Kreditkarte')
  })

  test('a filter that leaves nothing says so instead of showing an empty list', async () => {
    fetchMock.mockImplementation(
      async (url: string) =>
        new Response(JSON.stringify(String(url).includes('/accounts') ? [account] : [planned]), {
          status: 200,
        })
    )
    renderBook(false, [rent], 'unplanned')
    expect(await screen.findByText(i18n.t('monthBook.noUnplanned'))).toBeInTheDocument()
  })

  test('the unplanned filter says so in words and offers to lift it', async () => {
    const user = userEvent.setup()
    renderBook(false, [rent], 'unplanned')
    await screen.findByRole('button', { name: /Kino/ })
    expect(screen.getByRole('status')).toHaveTextContent(i18n.t('monthBook.onlyUnplanned'))
    await user.click(screen.getByRole('button', { name: i18n.t('monthBook.clearFilter') }))
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
    expect(names()).toHaveLength(5)
  })

  test('no hint while everything is shown', async () => {
    renderBook(false, [rent])
    await screen.findByRole('button', { name: /Kino/ })
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  test('the unplanned filter leaves out planned bookings, transfers and carry-overs', async () => {
    fetchMock.mockImplementation(
      async (url: string) =>
        new Response(
          JSON.stringify(
            String(url).includes('/accounts')
              ? [account, account2]
              : [
                  unplannedLate,
                  planned,
                  transfer,
                  {
                    ...booking,
                    id: 't-co',
                    kind: 'carry_over',
                    note: null,
                    category: null,
                    budget: null,
                    unplanned: false,
                  },
                ]
          ),
          { status: 200 }
        )
    )
    renderBook(false, [rent], 'unplanned')
    await screen.findByRole('button', { name: /Kino/ })
    expect(names()).toEqual(['Kino'])
    expect(screen.queryByText(i18n.t('monthBook.carryOverName'))).not.toBeInTheDocument()
  })
})

describe('MonthBook reads the server flags (#254)', () => {
  // Rows whose fields would say the opposite of their flags: only a page that reads
  // the flags passes.
  const heldBack = { ...booking, id: 't-held', note: 'Vorgemerkt', unplanned: false }
  const movedHere = { ...booking, id: 't-moved', note: 'Zuschuss', countsElsewhere: true }

  beforeEach(() => {
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async (url: string) =>
          new Response(
            JSON.stringify(String(url).includes('/accounts') ? [account] : [heldBack, movedHere]),
            { status: 200 }
          )
      )
    )
  })
  afterEach(() => vi.unstubAllGlobals())

  test('Ungeplant counts and offers zuordnen only where the server says unplanned', async () => {
    renderBook(false)
    await screen.findByRole('button', { name: /Zuschuss/ })
    expect(screen.getByRole('button', { name: 'Ungeplant · 1' })).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: /zuordnen/ })).toHaveLength(1)
  })

  test('the month badge follows the server, not the date', async () => {
    renderBook(false)
    expect(await screen.findByRole('button', { name: /Zuschuss/ })).toHaveTextContent(/für Sep/)
    expect(screen.getByRole('button', { name: /Vorgemerkt/ })).not.toHaveTextContent(/für /)
  })
})

describe('MonthBook without an account', () => {
  beforeEach(() => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response(JSON.stringify([]), { status: 200 }))
    )
  })
  afterEach(() => vi.unstubAllGlobals())

  test.each([
    ['your own book', '/plan/2026/09', '/accounts'],
    ['the book of the chosen person', '/plan/2026/09?member=u2', '/accounts?member=u2'],
  ])('the link to the accounts keeps the person of %s', async (_name, path, expected) => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(
      <QueryClientProvider client={client}>
        <MemoryRouter initialEntries={[path]}>
          <Book readOnly={false} positions={[]} initial="all" />
        </MemoryRouter>
      </QueryClientProvider>
    )
    const link = await screen.findByRole('link', { name: i18n.t('accounts.create') })
    expect(link).toHaveAttribute('href', expected)
  })
})
