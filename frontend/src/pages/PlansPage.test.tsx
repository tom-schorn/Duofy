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
    targetNeeds: '50.00',
    targetWants: '30.00',
    targetSavings: '20.00',
    bufferPercent: '0.00',
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
  const plans = [plan({ month: 9, householdIds: ['h1'] }), plan({ month: 10, householdIds: [] })]

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
        if (String(url).includes('/plans')) {
          return new Response(JSON.stringify(plans), { status: 200 })
        }
        return new Response('[]', { status: 200 })
      })
    )
  })
  afterEach(() => vi.unstubAllGlobals())

  test('?household= keeps only the months that carry the household, linked with it', async () => {
    renderAt('/plan?household=h1')
    // Only September feeds the household; October stays out of the list.
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
    expect(await screen.findByText('September 2026')).toBeInTheDocument()
    expect(await screen.findByText('Oktober 2026')).toBeInTheDocument()

    const link = screen.getByRole('link', { name: /September 2026/ })
    expect(link).toHaveAttribute('href', '/plan/2026/09')
  })
})
