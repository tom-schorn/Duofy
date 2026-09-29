import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { Toaster } from '@/components/ui/sonner'
import { i18n } from '@/lib/i18n'
import { flushPendingDelete } from '@/lib/undo-delete'
import { PlanDetailPage } from '@/pages/PlanDetailPage'

/** A whole month, positions included — the shape `/plans/{year}/{month}` returns. */
function ownPlan(overrides: Record<string, unknown> = {}) {
  return {
    id: 'plan1',
    year: 2026,
    month: 11,
    targetNeeds: '50.00',
    targetWants: '30.00',
    targetSavings: '20.00',
    bufferPercent: '0.00',
    income: '0.00',
    distributable: '0.00',
    spent: { needs: '0.00', wants: '0.00', savings: '0.00' },
    unpaid: '0.00',
    deletable: true,
    hints: [],
    positions: [],
    unplanned: { income: '0.00', needs: '0.00', wants: '0.00', savings: '0.00' },
    ...overrides,
  }
}

function renderAt(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(
    [{ path: '/plan/:year/:month', element: <PlanDetailPage /> }],
    { initialEntries: [path] }
  )
  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
      <Toaster />
    </QueryClientProvider>
  )
  return router
}

/** A household whose only other member shares the plan area at `level`. */
function household(level: string) {
  return [
    {
      id: 'h1',
      name: 'Zuhause',
      members: [
        {
          userId: 'u2',
          firstName: 'Ida',
          lastName: 'Test',
          email: 'ida@example.org',
          role: 'member',
          grantsPlan: level,
          grantsCommitments: 'none',
          grantsAccounts: 'none',
        },
      ],
    },
  ]
}

describe('PlanDetailPage', () => {
  // The month has no plan; everything else the page asks for is an empty list.
  beforeEach(() => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        if (String(url).includes('/plans/')) {
          return new Response(JSON.stringify({ detail: { code: 'plan_not_found' } }), {
            status: 404,
          })
        }
        if (String(url).endsWith('/households')) {
          return new Response(JSON.stringify(household('view')), { status: 200 })
        }
        return new Response('[]', { status: 200 })
      })
    )
  })
  afterEach(() => vi.unstubAllGlobals())

  test.each(['/plan/2026/13', '/plan/abc/x'])('%s is the not-found page', (path) => {
    renderAt(path)
    expect(screen.getByRole('heading', { name: 'Diese Seite gibt es nicht' })).toBeInTheDocument()
  })

  test('a valid month without a plan offers to create it', async () => {
    renderAt('/plan/2026/11')
    expect(await screen.findByRole('button', { name: 'Monat anlegen' })).toBeInTheDocument()
    expect(screen.getByText('November 2026 ist noch nicht angelegt')).toBeInTheDocument()
  })

  test('the create dialog offers the month of the new address after the route changes', async () => {
    const router = renderAt('/plan/2026/11')
    await screen.findByRole('button', { name: 'Monat anlegen' })
    await act(() => router.navigate('/plan/2026/12'))
    fireEvent.click(await screen.findByRole('button', { name: 'Monat anlegen' }))
    // The month select comes first in the dialog, the year select second.
    const [monthSelect] = await screen.findAllByRole('combobox')
    expect(monthSelect).toHaveTextContent('Dezember')
  })

  test.each([
    ['own plan', '/plan/2026/11', ''],
    ['member plan', '/plan/2026/11?member=u2&tab=flow', '?member=u2&tab=flow'],
    ['household plan', '/plan/2026/11?household=h1', '?household=h1'],
  ])('the month switch moves through the %s and keeps the address parameters', async (_name, path, search) => {
    const router = renderAt(path)
    fireEvent.click(await screen.findByRole('button', { name: i18n.t('book.nextMonth') }))
    expect(router.state.location.pathname).toBe('/plan/2026/12')
    expect(router.state.location.search).toBe(search)
    expect(await screen.findByText('Dezember 2026')).toBeInTheDocument()

    // Across the year boundary, with a two-digit month.
    fireEvent.click(screen.getByRole('button', { name: i18n.t('book.nextMonth') }))
    expect(router.state.location.pathname).toBe('/plan/2027/01')
    fireEvent.click(screen.getByRole('button', { name: 'Vorheriger Monat' }))
    expect(router.state.location.pathname).toBe('/plan/2026/12')

    // Back goes to the month before, not out of the page.
    await act(() => router.navigate(-1))
    expect(router.state.location.pathname).toBe('/plan/2027/01')
  })

  test('without the right to edit the plan there is no create button', async () => {
    renderAt('/plan/2026/11?member=u2')
    expect(await screen.findByText('November 2026 ist noch nicht angelegt')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Monat anlegen' })).not.toBeInTheDocument()
  })

  describe('deleting the month (#219)', () => {
    function stubPlan(plan: Record<string, unknown>, deletes: string[]) {
      vi.stubGlobal(
        'fetch',
        vi.fn(async (url: string, init?: RequestInit) => {
          const target = String(url)
          // The permanently-mounted print chart loads the flow even on the plan tab.
          if (target.includes('/flow')) {
            return new Response(
              JSON.stringify({
                year: 2026,
                month: 11,
                flowLimitsBy: 'plan',
                start: '0.00',
                entries: [],
                days: [],
                hints: [],
              }),
              { status: 200 }
            )
          }
          if (target.includes('/plans/2026/11') && init?.method === 'DELETE') {
            deletes.push(target)
            return new Response(null, { status: 204 })
          }
          if (target.includes('/plans/2026/11')) {
            return new Response(JSON.stringify(plan), { status: 200 })
          }
          if (target.endsWith('/households')) {
            return new Response(JSON.stringify(household('view')), { status: 200 })
          }
          return new Response('[]', { status: 200 })
        })
      )
    }

    test('clicking it shows the missing-month state at once, before any request', async () => {
      const deletes: string[] = []
      stubPlan(ownPlan(), deletes)

      renderAt('/plan/2026/11')
      const button = await screen.findByRole('button', { name: i18n.t('plan.deleteMonth') })
      expect(button).toBeEnabled()

      fireEvent.click(button)
      // Gone at once (decisions 21/22) — same moment a small thing leaves its list.
      expect(
        await screen.findByRole('button', { name: 'Monat anlegen' })
      ).toBeInTheDocument()
      expect(screen.queryByRole('button', { name: i18n.t('plan.deleteMonth') })).not.toBeInTheDocument()
      // Nothing is sent while the „Rückgängig" message could still take it back.
      expect(deletes).toEqual([])

      await act(() => flushPendingDelete())
      await waitFor(() => expect(deletes).toHaveLength(1))
    })

    test('Undo brings the plan back without ever sending a request', async () => {
      const deletes: string[] = []
      stubPlan(ownPlan(), deletes)

      renderAt('/plan/2026/11')
      fireEvent.click(await screen.findByRole('button', { name: i18n.t('plan.deleteMonth') }))
      await screen.findByRole('button', { name: 'Monat anlegen' })

      fireEvent.click(await screen.findByRole('button', { name: i18n.t('ui.undo') }))

      expect(
        await screen.findByRole('button', { name: i18n.t('plan.deleteMonth') })
      ).toBeInTheDocument()
      await act(() => flushPendingDelete())
      expect(deletes).toEqual([])
    })

    test('a month with bookings cannot be deleted, and says why', async () => {
      stubPlan(ownPlan({ deletable: false }), [])

      renderAt('/plan/2026/11')
      const button = await screen.findByRole('button', { name: i18n.t('plan.deleteMonth') })
      expect(button).toBeDisabled()
      expect(screen.getByText(i18n.t('errors.plan_has_transactions'))).toBeInTheDocument()
    })

    test('a member plan without the delete right shows no delete button', async () => {
      stubPlan(ownPlan(), [])

      renderAt('/plan/2026/11?member=u2')
      await screen.findByText('November 2026')
      expect(
        screen.queryByRole('button', { name: i18n.t('plan.deleteMonth') })
      ).not.toBeInTheDocument()
    })
  })

  test.each([
    ['own plan', '/plan/2026/11', '/plan'],
    ['member plan', '/plan/2026/11?member=u2', '/plan?member=u2'],
    ['household plan', '/plan/2026/11?household=h1', '/plan?household=h1'],
  ])('the all-plans link keeps the scope of the %s', async (_name, path, expected) => {
    renderAt(path)
    const link = await screen.findByRole('link', { name: i18n.t('plan.allPlans') })
    expect(link).toHaveAttribute('href', expected)
  })
})

describe('PlanDetailPage — household month not everyone has planned yet (#214)', () => {
  // A household month exists only once every current member has created their
  // own plan for it. Until then the backend sends empty positions and names who
  // is still missing — the page must show that instead of a half plan.
  beforeEach(() => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        if (String(url).endsWith('/households')) {
          return new Response(JSON.stringify(household('view')), { status: 200 })
        }
        if (String(url).includes('/plans/household/')) {
          return new Response(
            JSON.stringify({
              householdId: 'h1',
              householdName: 'Zuhause',
              year: 2026,
              month: 11,
              targetNeeds: '50.00',
              targetWants: '30.00',
              targetSavings: '20.00',
              bufferPercent: '0.00',
              income: '0.00',
              distributable: '0.00',
              spent: { needs: '0.00', wants: '0.00', savings: '0.00' },
              unpaid: '0.00',
              hints: [],
              positions: [],
              unplanned: { income: '0.00', needs: '0.00', wants: '0.00', savings: '0.00' },
              missingMembers: ['Ida'],
            }),
            { status: 200 }
          )
        }
        return new Response('[]', { status: 200 })
      })
    )
  })
  afterEach(() => vi.unstubAllGlobals())

  test('shows a calm notice instead of a half plan', async () => {
    renderAt('/plan/2026/11?household=h1')
    expect(await screen.findByText(i18n.t('plan.incompleteTitle'))).toBeInTheDocument()
    expect(
      screen.getByText(
        i18n.t('plan.incompleteOne', { names: 'Ida', month: 'November 2026' })
      )
    ).toBeInTheDocument()
  })
})

describe('PlanDetailPage — household plan positions clickable by rights (#218)', () => {
  // Own position (Miete, owner u1 — the signed-in user) and Ida's (u2), whose
  // grant varies per test. Both sit in the same budget on purpose: the
  // household plan mixes rows with and without edit rights side by side.
  function position(overrides: Record<string, unknown>) {
    return {
      id: 'p1',
      label: 'Miete',
      amountPlanned: '500.00',
      amountActual: null,
      category: 'housing.rent',
      budget: 'needs',
      dueDay: 1,
      accountId: null,
      counterAccountId: null,
      paymentMethod: null,
      isLimit: false,
      passThrough: false,
      isPrivate: false,
      commitmentId: null,
      paidAt: null,
      ownerId: 'u1',
      ownerName: 'Max',
      ...overrides,
    }
  }

  function mockFetch(level: string) {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        const path = String(url)
        if (path.endsWith('/users/me')) {
          return new Response(JSON.stringify({ id: 'u1', firstName: 'Max' }), { status: 200 })
        }
        if (path.endsWith('/households')) {
          return new Response(JSON.stringify(household(level)), { status: 200 })
        }
        // The paper version of the charts is permanently mounted (also outside
        // the flow tab), so it always asks for this too.
        if (path.endsWith('/flow')) {
          return new Response(
            JSON.stringify({
              year: 2026,
              month: 11,
              flowLimitsBy: 'plan',
              start: '0.00',
              entries: [],
              days: [],
              hints: [],
              missingMembers: [],
            }),
            { status: 200 }
          )
        }
        if (path.includes('/plans/household/')) {
          return new Response(
            JSON.stringify({
              householdId: 'h1',
              householdName: 'Zuhause',
              year: 2026,
              month: 11,
              targetNeeds: '50.00',
              targetWants: '30.00',
              targetSavings: '20.00',
              bufferPercent: '0.00',
              income: '1000.00',
              distributable: '1000.00',
              spent: { needs: '0.00', wants: '0.00', savings: '0.00' },
              unpaid: '0.00',
              hints: [],
              positions: [
                position({}),
                position({
                  id: 'p2',
                  label: 'Strom',
                  category: 'housing.utilities',
                  ownerId: 'u2',
                  ownerName: 'Ida',
                }),
              ],
              unplanned: { income: '0.00', needs: '25.00', wants: '0.00', savings: '0.00' },
              missingMembers: [],
            }),
            { status: 200 }
          )
        }
        return new Response('[]', { status: 200 })
      })
    )
  }

  afterEach(() => vi.unstubAllGlobals())

  test('an own position opens to edit even without a grant — it needs none for your own plan', async () => {
    mockFetch('view')
    renderAt('/plan/2026/11?household=h1')
    fireEvent.click(await screen.findByRole('button', { name: /^Miete(?!:)/ }))
    expect(
      await screen.findByRole('dialog', { name: i18n.t('positionDialog.kinds.obligation.editTitle') })
    ).toBeInTheDocument()
  })

  test('the household plan shows the unplanned sum of its members too (#240)', async () => {
    mockFetch('view')
    renderAt('/plan/2026/11?household=h1')
    expect(
      await screen.findByRole('button', { name: new RegExp(`^${i18n.t('budget.unplanned')}`) })
    ).toBeInTheDocument()
  })

  test('a position of somebody who granted edit opens too', async () => {
    mockFetch('edit')
    renderAt('/plan/2026/11?household=h1')
    fireEvent.click(await screen.findByRole('button', { name: /^Strom(?!:)/ }))
    expect(
      await screen.findByRole('dialog', { name: i18n.t('positionDialog.kinds.obligation.editTitle') })
    ).toBeInTheDocument()
  })

  test('a position of somebody who did not grant edit opens read-only', async () => {
    mockFetch('view')
    renderAt('/plan/2026/11?household=h1')
    fireEvent.click(await screen.findByRole('button', { name: /^Strom(?!:)/ }))
    expect(
      await screen.findByRole('dialog', { name: i18n.t('positionDialog.kinds.obligation.editTitle') })
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: i18n.t('common.save') })
    ).not.toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: i18n.t('ui.close') })).toHaveLength(2)
  })
})

describe('PlanDetailPage — the Ungeplant row (#240)', () => {
  function stub(plan: Record<string, unknown>) {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        const target = String(url)
        if (target.includes('/flow')) {
          return new Response(
            JSON.stringify({
              year: 2026,
              month: 11,
              flowLimitsBy: 'plan',
              start: '0.00',
              entries: [],
              days: [],
              hints: [],
            }),
            { status: 200 }
          )
        }
        if (target.includes('/plans/2026/11')) {
          return new Response(JSON.stringify(plan), { status: 200 })
        }
        if (target.endsWith('/households')) return new Response('[]', { status: 200 })
        return new Response('[]', { status: 200 })
      })
    )
  }
  afterEach(() => vi.unstubAllGlobals())

  test('a budget with unplanned bookings shows the row, another without does not', async () => {
    stub(
      ownPlan({ unplanned: { income: '0.00', needs: '40.00', wants: '0.00', savings: '0.00' } })
    )
    renderAt('/plan/2026/11')
    expect(await screen.findAllByText(i18n.t('budget.unplanned'))).toHaveLength(1)
  })

  test('unplanned income shows on the income side, not in a budget', async () => {
    stub(
      ownPlan({ unplanned: { income: '80.00', needs: '0.00', wants: '0.00', savings: '0.00' } })
    )
    renderAt('/plan/2026/11')
    const row = await screen.findByRole('button', { name: new RegExp(`^${i18n.t('budget.unplanned')}`) })
    const income = screen.getByRole('heading', { name: i18n.t('enums.budget.income') })
    expect(income.compareDocumentPosition(row) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    const needs = screen.getByRole('heading', { name: i18n.t('enums.budget.needs') })
    expect(needs.compareDocumentPosition(row) & Node.DOCUMENT_POSITION_PRECEDING).toBeTruthy()
  })

  test('a click opens the book of the month, filtered to the unplanned', async () => {
    stub(
      ownPlan({ unplanned: { income: '0.00', needs: '40.00', wants: '0.00', savings: '0.00' } })
    )
    const router = renderAt('/plan/2026/11')
    fireEvent.click(
      await screen.findByRole('button', { name: new RegExp(`^${i18n.t('budget.unplanned')}`) })
    )
    expect(router.state.location.pathname).toBe('/plan/2026/11')
    expect(new URLSearchParams(router.state.location.search).get('tab')).toBe('book')
    expect(new URLSearchParams(router.state.location.search).get('filter')).toBe('unplanned')
  })
})

describe('PlanDetailPage — the book as a tab (#241)', () => {
  const account = {
    id: 'a1',
    name: 'Giro',
    active: true,
    isDefault: true,
    type: 'checking',
    balance: '0.00',
    countsAsAvailable: true,
  }
  const rent = {
    id: 'p1',
    label: 'Miete',
    amountPlanned: '500.00',
    amountActual: null,
    category: 'housing.rent',
    budget: 'needs',
    dueDay: 1,
    accountId: null,
    counterAccountId: null,
    paymentMethod: null,
    isLimit: false,
    passThrough: false,
    householdId: null,
    commitmentId: null,
    paidAt: null,
  }
  const booking = (overrides: Record<string, unknown>) => ({
    id: 't1',
    accountId: 'a1',
    counterAccountId: null,
    kind: 'booking',
    occurredOn: '2026-11-05',
    amount: '10.00',
    note: 'Kino',
    category: 'leisure.entertainment',
    budget: 'wants',
    positionId: null,
    planYear: 2026,
    planMonth: 11,
    autoBooked: false,
    externalRef: null,
    ...overrides,
  })
  const rows = [
    booking({ id: 't1', note: 'Kino' }),
    booking({ id: 't2', note: 'Miete Nov', positionId: 'p1', occurredOn: '2026-11-01' }),
  ]

  function members(accountsLevel: string) {
    return [
      {
        id: 'h1',
        name: 'Zuhause',
        members: [
          {
            userId: 'u2',
            firstName: 'Ida',
            lastName: 'Test',
            email: 'ida@example.org',
            role: 'member',
            grantsPlan: 'view',
            grantsCommitments: 'none',
            grantsAccounts: accountsLevel,
          },
        ],
      },
    ]
  }

  function stub(
    accountsLevel = 'none',
    bookError: string | null = null,
    householdPositions: unknown[] = [{ ...rent, ownerId: 'u2', ownerName: 'Ida' }]
  ) {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        const target = String(url)
        if (target.includes('/flow')) {
          return new Response(
            JSON.stringify({
              year: 2026,
              month: 11,
              flowLimitsBy: 'plan',
              start: '0.00',
              entries: [],
              days: [],
              hints: [],
              missingMembers: [],
            }),
            { status: 200 }
          )
        }
        if (target.includes('/transactions')) {
          return bookError
            ? new Response(JSON.stringify({ detail: { code: bookError } }), { status: 403 })
            : new Response(JSON.stringify(rows), { status: 200 })
        }
        if (target.includes('/accounts')) {
          return new Response(JSON.stringify([account]), { status: 200 })
        }
        if (target.includes('/plans/household/')) {
          return new Response(
            JSON.stringify({
              householdId: 'h1',
              householdName: 'Zuhause',
              year: 2026,
              month: 11,
              targetNeeds: '50.00',
              targetWants: '30.00',
              targetSavings: '20.00',
              income: '1000.00',
              distributable: '1000.00',
              spent: { needs: '0.00', wants: '0.00', savings: '0.00' },
              unpaid: '0.00',
              householdIds: [],
              hints: [],
              positions: householdPositions,
              unplanned: { income: '0.00', needs: '0.00', wants: '0.00', savings: '0.00' },
              missingMembers: [],
            }),
            { status: 200 }
          )
        }
        if (target.includes('/plans/2026/11')) {
          return new Response(JSON.stringify(ownPlan({ positions: [rent] })), { status: 200 })
        }
        if (target.endsWith('/households')) {
          return new Response(JSON.stringify(members(accountsLevel)), { status: 200 })
        }
        return new Response('[]', { status: 200 })
      })
    )
  }
  afterEach(() => vi.unstubAllGlobals())

  const add = () => screen.queryByRole('button', { name: i18n.t('monthBook.add') })

  test('the tabs are Plan, Buch and Verlauf, in that order, the book with its number of bookings', async () => {
    stub()
    renderAt('/plan/2026/11')
    await screen.findByRole('tab', { name: i18n.t('plan.tabBookCount', { count: 2 }) })
    expect(screen.getAllByRole('tab').map((tab) => tab.textContent)).toEqual([
      i18n.t('plan.tabPlan'),
      i18n.t('plan.tabBookCount', { count: 2 }),
      i18n.t('plan.tabFlow'),
    ])
  })

  test('a carry-over is no booking in the count of the tab', async () => {
    stub()
    const original = globalThis.fetch
    vi.stubGlobal('fetch', async (url: string, init?: RequestInit) => {
      if (String(url).includes('/transactions')) {
        return new Response(
          JSON.stringify([...rows, booking({ id: 't3', kind: 'carry_over', category: null, budget: null })]),
          { status: 200 }
        )
      }
      return original(url, init)
    })
    renderAt('/plan/2026/11')
    expect(
      await screen.findByRole('tab', { name: i18n.t('plan.tabBookCount', { count: 2 }) })
    ).toBeInTheDocument()
  })

  test('without a readable book the tab is just Buch', async () => {
    stub('none', 'no_insight_granted')
    renderAt('/plan/2026/11?member=u2')
    expect(await screen.findByRole('tab', { name: i18n.t('plan.tabBook') })).toBeInTheDocument()
  })

  test('the book tab lists the bookings of the month by date', async () => {
    stub()
    renderAt('/plan/2026/11?tab=book')
    expect(await screen.findByRole('button', { name: /Miete Nov/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Kino/ })).toBeInTheDocument()
    const names = screen
      .getAllByRole('button')
      .filter((button) => button.hasAttribute('data-row-open'))
      .map((button) => button.textContent)
    expect(names[0]).toMatch(/Miete Nov/)
  })

  test('clicking the tab puts it in the address', async () => {
    stub()
    const router = renderAt('/plan/2026/11')
    await userEvent.setup().click(await screen.findByRole('tab', { name: /^Buch/ }))
    expect(new URLSearchParams(router.state.location.search).get('tab')).toBe('book')
  })

  test('the add-booking button sits at the top of the page, on every tab', async () => {
    stub()
    renderAt('/plan/2026/11')
    expect(await screen.findByRole('button', { name: i18n.t('monthBook.add') })).toBeInTheDocument()
    await userEvent.setup().click(await screen.findByRole('tab', { name: /^Buch/ }))
    expect(add()).toBeInTheDocument()
  })

  test('the filter lives in the address: ?filter=unplanned shows the unplanned only', async () => {
    stub()
    const router = renderAt('/plan/2026/11?tab=book&filter=unplanned')
    await screen.findByRole('button', { name: /Kino/ })
    expect(screen.queryByRole('button', { name: /Miete Nov/ })).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: i18n.t('monthBook.filterAll') }))
    expect(await screen.findByRole('button', { name: /Miete Nov/ })).toBeInTheDocument()
    expect(new URLSearchParams(router.state.location.search).get('filter')).toBeNull()
  })

  test('another person plan with the accounts grant at view shows their book and no add button', async () => {
    stub('view')
    renderAt('/plan/2026/11?member=u2&tab=book')
    expect(await screen.findByText('Kino')).toBeInTheDocument()
    expect(add()).not.toBeInTheDocument()
  })

  test('another person plan with the accounts grant at edit offers to book', async () => {
    stub('edit')
    renderAt('/plan/2026/11?member=u2&tab=book')
    expect(await screen.findByRole('button', { name: i18n.t('monthBook.add') })).toBeInTheDocument()
  })

  test('another person plan without the accounts grant says the book is not shared', async () => {
    stub('none', 'no_insight_granted')
    renderAt('/plan/2026/11?member=u2&tab=book')
    expect(
      await screen.findByText(i18n.t('book.notShared', { name: 'Ida' }))
    ).toBeInTheDocument()
  })

  test('the household plan has the book tab, read only', async () => {
    stub('view')
    renderAt('/plan/2026/11?household=h1&tab=book')
    expect(await screen.findByText('Kino')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Kino/ })).not.toBeInTheDocument()
    expect(add()).not.toBeInTheDocument()
  })

  test('the household book does not claim that somebody shares no numbers (it shows everybody)', async () => {
    stub('plan')
    renderAt('/plan/2026/11?household=h1&tab=book')
    await screen.findByText('Kino')
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
    expect(screen.queryByText(/teilt noch keine Zahlen|teilen noch keine Zahlen/)).not.toBeInTheDocument()
  })

  test('the household book leaves out the balance figures: they come from grant-limited accounts', async () => {
    stub('view')
    renderAt('/plan/2026/11?household=h1&tab=book')
    await screen.findByText('Kino')
    expect(screen.queryByText(i18n.t('bookMetrics.available'))).not.toBeInTheDocument()
    expect(screen.queryByText(i18n.t('bookMetrics.leftover'))).not.toBeInTheDocument()
    expect(screen.getByText(i18n.t('bookMetrics.spending'))).toBeInTheDocument()
  })

  test('the own book keeps the balance figures', async () => {
    stub()
    renderAt('/plan/2026/11?tab=book')
    await screen.findByText('Kino')
    expect(screen.getByText(i18n.t('bookMetrics.available'))).toBeInTheDocument()
    expect(screen.getByText(i18n.t('bookMetrics.leftover'))).toBeInTheDocument()
  })

  test('a household month without shared positions still has its tabs and its book', async () => {
    stub('view', null, [])
    renderAt('/plan/2026/11?household=h1&tab=book')
    expect(await screen.findByText('Kino')).toBeInTheDocument()
    expect(screen.getAllByRole('tab')).toHaveLength(3)
  })

  test('a household month without shared positions says so on the plan tab', async () => {
    stub('view', null, [])
    renderAt('/plan/2026/11?household=h1')
    expect(await screen.findByText(i18n.t('plan.nothingShared'))).toBeInTheDocument()
    expect(screen.getAllByRole('tab')).toHaveLength(3)
  })
})
