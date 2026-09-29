import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { afterEach, describe, expect, test, vi } from 'vitest'

import { i18n } from '@/lib/i18n'
import { PlanDetailPage } from '@/pages/PlanDetailPage'

/**
 * #251: ticking a position off works the same in every plan — own, another
 * person's and the household's. Only the grant decides whether the box is there.
 */

type Tick = { url: string; body: Record<string, unknown> }

function position(overrides: Record<string, unknown> = {}) {
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

const planBase = {
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

function households(level: string) {
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

/** Every request the page makes; ticks are recorded with their body. */
function stub(level: string, ticks: Tick[]) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      const path = String(url)
      const json = (body: unknown) => new Response(JSON.stringify(body), { status: 200 })
      if (path.endsWith('/paid') && init?.method === 'POST') {
        ticks.push({ url: path, body: JSON.parse(String(init.body ?? '{}')) })
        return json(position({ paidAt: '2026-11-03T10:00:00Z' }))
      }
      if (path.endsWith('/users/me')) return json({ id: 'u1', firstName: 'Max' })
      if (path.endsWith('/households')) return json(households(level))
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
      if (path.includes('/plans/household/')) {
        return json({
          ...planBase,
          householdId: 'h1',
          householdName: 'Zuhause',
          missingMembers: [],
          positions: [
            position(),
            position({ id: 'p2', label: 'Strom', ownerId: 'u2', ownerName: 'Ida' }),
          ],
        })
      }
      if (path.includes('/plans/2026/11')) {
        // Own plan and another person's share the endpoint; the owner is a query.
        const foreign = path.includes('owner=u2')
        return json({
          ...planBase,
          id: foreign ? 'plan2' : 'plan1',
          positions: [position(foreign ? { ownerId: 'u2', ownerName: 'Ida' } : {})],
        })
      }
      return json([])
    })
  )
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
}

const tickBox = (label: string) => i18n.t('budget.tick', { label })

async function tickAndConfirm(label: string) {
  fireEvent.click(await screen.findByRole('checkbox', { name: tickBox(label) }))
  expect(
    await screen.findByRole('dialog', { name: i18n.t('paidDialog.title', { label }) })
  ).toBeInTheDocument()
  fireEvent.click(screen.getByRole('button', { name: i18n.t('paidDialog.submit') }))
}

afterEach(() => vi.unstubAllGlobals())

describe('ticking off, one operation in every plan (#251)', () => {
  test('own plan: the dialog opens and the tick carries date and amount', async () => {
    const ticks: Tick[] = []
    stub('none', ticks)
    renderAt('/plan/2026/11')
    await tickAndConfirm('Miete')
    await waitFor(() => expect(ticks).toHaveLength(1))
    expect(ticks[0].url).toContain('/positions/p1/paid')
    expect(ticks[0].body).toMatchObject({ amount: '500.00' })
    expect(ticks[0].body).toHaveProperty('occurredOn')
  })

  test('another person with the edit grant: the same dialog, the same tick', async () => {
    const ticks: Tick[] = []
    stub('edit', ticks)
    renderAt('/plan/2026/11?member=u2')
    await tickAndConfirm('Miete')
    await waitFor(() => expect(ticks).toHaveLength(1))
    expect(ticks[0].url).toContain('/positions/p1/paid')
    expect(ticks[0].body).toMatchObject({ amount: '500.00' })
    expect(ticks[0].body).toHaveProperty('occurredOn')
  })

  test('another person without the edit grant: no box at all, only the view', async () => {
    stub('view', [])
    renderAt('/plan/2026/11?member=u2')
    await screen.findByText('November 2026')
    await screen.findByText('Miete')
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
  })

  test('household plan, own position: the same dialog', async () => {
    const ticks: Tick[] = []
    stub('view', ticks)
    renderAt('/plan/2026/11?household=h1')
    await tickAndConfirm('Miete')
    await waitFor(() => expect(ticks).toHaveLength(1))
    expect(ticks[0].url).toContain('/positions/p1/paid')
    expect(ticks[0].body).toHaveProperty('occurredOn')
  })

  test('household plan, position of somebody with the edit grant: the same dialog', async () => {
    const ticks: Tick[] = []
    stub('edit', ticks)
    renderAt('/plan/2026/11?household=h1')
    await tickAndConfirm('Strom')
    await waitFor(() => expect(ticks).toHaveLength(1))
    expect(ticks[0].url).toContain('/positions/p2/paid')
    expect(ticks[0].body).toHaveProperty('occurredOn')
  })

  test('household plan, position of somebody without the edit grant: no box', async () => {
    stub('view', [])
    renderAt('/plan/2026/11?household=h1')
    expect(await screen.findByRole('checkbox', { name: tickBox('Miete') })).toBeInTheDocument()
    expect(screen.queryByRole('checkbox', { name: tickBox('Strom') })).not.toBeInTheDocument()
  })
})
