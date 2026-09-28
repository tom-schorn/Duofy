import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import type { PlanSummary } from '@/lib/domain'
import { PlansPage } from '@/pages/PlansPage'

function plan(overrides: Partial<PlanSummary> = {}): PlanSummary {
  return {
    year: 2026,
    month: 9,
    deletable: false,
    targetNeeds: '50.00',
    targetWants: '30.00',
    targetSavings: '20.00',
    income: '2000.00',
    distributable: '2000.00',
    spent: { needs: '0.00', wants: '0.00', savings: '0.00' },
    unpaid: '0.00',
    householdIds: [],
    ...overrides,
  }
}

function renderAt(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter([{ path: '/plan', element: <PlansPage /> }], {
    initialEntries: [path],
  })
  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  )
  return router
}

describe('PlansPage', () => {
  // Own plans and the household's months come from two different endpoints now
  // (#214): the household list is composed on the backend from every member's
  // positions, not filtered client-side from the viewer's own plans. September
  // exists only on the household endpoint here — as if a partner alone had a
  // shared position in it — to prove the page does not need it in the viewer's
  // own list too.
  const ownPlans = [plan({ month: 8, householdIds: [] }), plan({ month: 10, householdIds: [] })]
  const householdPlans = [plan({ month: 9, householdIds: ['h1'] })]

  beforeEach(() => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        if (String(url).endsWith('/households')) {
          return new Response(
            JSON.stringify([{ id: 'h1', name: 'Zuhause', members: [] }]),
            { status: 200 }
          )
        }
        if (String(url).includes('/plans/household/')) {
          return new Response(JSON.stringify(householdPlans), { status: 200 })
        }
        if (String(url).includes('/plans')) {
          return new Response(JSON.stringify(ownPlans), { status: 200 })
        }
        return new Response('[]', { status: 200 })
      })
    )
  })
  afterEach(() => vi.unstubAllGlobals())

  test('?household= lists the household endpoint\'s own months, linked with it', async () => {
    renderAt('/plan?household=h1')
    // September only exists on the household endpoint — not in the viewer's own
    // plans — and still shows. October is a private month of the viewer's own
    // and must not leak into the household view.
    expect(await screen.findByText('September 2026')).toBeInTheDocument()
    expect(screen.queryByText('Oktober 2026')).not.toBeInTheDocument()

    const link = screen.getByRole('link', { name: /September 2026/ })
    expect(link).toHaveAttribute('href', '/plan/2026/09?household=h1')
  })

  test('the household has no create button — the household plan is composed, never made', async () => {
    renderAt('/plan?household=h1')
    await screen.findByText('September 2026')
    expect(screen.queryByRole('button', { name: 'Monat anlegen' })).not.toBeInTheDocument()
  })

  test('without ?household= every own month is listed, linked without a scope', async () => {
    renderAt('/plan')
    expect(await screen.findByText('August 2026')).toBeInTheDocument()
    expect(await screen.findByText('Oktober 2026')).toBeInTheDocument()

    const link = screen.getByRole('link', { name: /August 2026/ })
    expect(link).toHaveAttribute('href', '/plan/2026/08')
  })
})
