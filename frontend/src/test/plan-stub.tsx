import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { vi } from 'vitest'

import { PlanDetailPage } from '@/pages/PlanDetailPage'

/**
 * A stubbed backend and a router for the plan page, shared by the tests that look
 * at it from the outside (#251): own plan, another person's plan (`?member=u2`)
 * and the household plan (`?household=h1`).
 */

export type Tick = { url: string; body: Record<string, unknown> }

/** Whether a request asks for one month of the household plan (`/plans/2026/11?household=h1`). */
export function isHouseholdMonth(url: string) {
  return /\/plans\/\d+\/\d+\?household=/.test(url)
}

export function position(overrides: Record<string, unknown> = {}) {
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

export const TICKED = '2026-11-03T10:00:00Z'

export const planBase = {
  targetNeeds: '50.00',
  targetWants: '30.00',
  targetSavings: '20.00',
  bufferPercent: '0.00',
  income: '1000.00',
  distributable: '1000.00',
  spent: { needs: '0.00', wants: '0.00', savings: '0.00' },
  unpaid: '0.00',
  deletable: true,
  hints: [],
  unplanned: { income: '0.00', needs: '0.00', wants: '0.00', savings: '0.00' },
  year: 2026,
  month: 11,
}

/**
 * One other member who grants me `level` on the plan. The book follows the plan
 * level unless `bookLevel` says otherwise: most tests speak of "a stand-in at
 * edit", and ticking off asks the book since #254.
 */
export function households(level: string, accountsLevel = 'none', bookLevel = level) {
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
          grantsToMe: {
            plan: level,
            book: bookLevel,
            accounts: accountsLevel,
            commitments: 'none',
            import: accountsLevel,
          },
          myGrants: { plan: 'none', book: 'none', accounts: 'none', commitments: 'none', import: 'none' },
        },
      ],
    },
  ]
}

/** Every request the page makes; ticks are recorded with their body. */
export type Options = {
  /** Bookings of the month; `null` = the request is refused (no accounts view). */
  transactions?: Record<string, unknown>[] | null
  paidAt?: string | null
  deletes?: string[]
  accountsLevel?: string
  bookLevel?: string
}

export function stub(level: string, ticks: Tick[] = [], options: Options = {}) {
  const { transactions = [], paidAt = null, deletes = [] } = options
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      const path = String(url)
      const json = (body: unknown) => new Response(JSON.stringify(body), { status: 200 })
      if (path.endsWith('/paid') && init?.method === 'DELETE') {
        deletes.push(path)
        return json(position())
      }
      if (path.includes('/transactions')) {
        return transactions === null
          ? new Response(JSON.stringify({ code: 'forbidden' }), { status: 403 })
          : json(transactions)
      }
      if (path.endsWith('/paid') && init?.method === 'POST') {
        ticks.push({ url: path, body: JSON.parse(String(init.body ?? '{}')) })
        return json(position({ paidAt: '2026-11-03T10:00:00Z' }))
      }
      if (path.endsWith('/users/me')) return json({ id: 'u1', firstName: 'Max' })
      if (path.endsWith('/households'))
        return json(households(level, options.accountsLevel, options.bookLevel ?? level))
      if (path.includes('/flow')) {
        return json({
          year: 2026,
          month: 11,
          flowLimitsBy: 'plan',
          start: '0.00',
          entries: [],
          days: [],
          hints: [],
          missingMembers: [],
        })
      }
      if (isHouseholdMonth(path)) {
        return json({
          ...planBase,
          householdId: 'h1',
          householdName: 'Zuhause',
          missingMembers: [],
          positions: [
            position({ paidAt }),
            position({
              id: 'p2',
              label: 'Strom',
              ownerId: 'u2',
              ownerName: 'Ida',
              paidAt,
            }),
          ],
        })
      }
      if (path.includes('/plans/2026/11')) {
        // Own plan and another person's share the endpoint; the owner is a query.
        const foreign = path.includes('owner=u2')
        return json({
          ...planBase,
          id: foreign ? 'plan2' : 'plan1',
          positions: [
            position(foreign ? { ownerId: 'u2', ownerName: 'Ida', paidAt } : { paidAt }),
          ],
        })
      }
      return json([])
    })
  )
}

export function renderAt(path: string) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  const router = createMemoryRouter(
    [{ path: '/plan/:year/:month', element: <PlanDetailPage /> }],
    { initialEntries: [path] }
  )
  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  )
}
