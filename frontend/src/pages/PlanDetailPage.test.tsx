import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

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
    householdIds: [],
    deletable: true,
    hints: [],
    positions: [],
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

    test('a deletable month leaves with undo, and the request waits for it', async () => {
      const deletes: string[] = []
      stubPlan(ownPlan(), deletes)

      renderAt('/plan/2026/11')
      const button = await screen.findByRole('button', { name: i18n.t('plan.deleteMonth') })
      expect(button).toBeEnabled()

      fireEvent.click(button)
      // Nothing is sent while the „Rückgängig" message could still take it back.
      expect(deletes).toEqual([])

      await act(() => flushPendingDelete())
      await waitFor(() => expect(deletes).toHaveLength(1))
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
              householdIds: [],
              hints: [],
              positions: [],
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
