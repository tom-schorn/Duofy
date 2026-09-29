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

const TICKED = '2026-11-03T10:00:00Z'

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
type Options = {
  /** Bookings of the month; `null` = the request is refused (no accounts view). */
  transactions?: Record<string, unknown>[] | null
  paidAt?: string | null
  deletes?: string[]
}

function stub(level: string, ticks: Tick[], options: Options = {}) {
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

function renderAt(path: string) {
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

const tickBox = (label: string) => i18n.t('budget.tick', { label })

async function tickAndConfirm(label: string) {
  fireEvent.click(await screen.findByRole('checkbox', { name: tickBox(label) }))
  expect(
    await screen.findByRole('dialog', {
      name: i18n.t('paidDialog.title', { label }),
    })
  ).toBeInTheDocument()
  fireEvent.click(screen.getByRole('button', { name: i18n.t('paidDialog.submit') }))
}

afterEach(() => vi.unstubAllGlobals())

const booking = (positionId: string) => ({
  id: 'b1',
  positionId,
  autoBooked: true,
  amount: '42.00',
  occurredOn: '2026-11-03',
  accountId: 'a1',
  label: 'Miete',
})

async function untickAndConfirm(label: string) {
  const box = await screen.findByRole('checkbox', {
    name: i18n.t('budget.reopen', { label }),
  })
  fireEvent.click(box)
  return screen.findByRole('alertdialog')
}

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
    expect(
      await screen.findByRole('checkbox', { name: tickBox('Miete') })
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('checkbox', { name: tickBox('Strom') })
    ).not.toBeInTheDocument()
  })

  test.each([
    ['own plan', '/plan/2026/11', 'none', 'Miete', 'p1'],
    ['another person plan', '/plan/2026/11?member=u2', 'edit', 'Miete', 'p1'],
    ['household plan', '/plan/2026/11?household=h1', 'edit', 'Strom', 'p2'],
  ])(
    '%s: unticking asks with the amount, confirming sends the DELETE',
    async (_n, path, level, label, id) => {
      const deletes: string[] = []
      stub(level, [], { transactions: [booking(id)], paidAt: TICKED, deletes })
      renderAt(path)
      const dialog = await untickAndConfirm(label)
      await waitFor(() => expect(dialog).toHaveTextContent('42,00'))
      expect(deletes).toHaveLength(0)
      fireEvent.click(screen.getByRole('button', { name: i18n.t('plan.untick') }))
      await waitFor(() => expect(deletes).toHaveLength(1))
      expect(deletes[0]).toContain(`/positions/${id}/paid`)
      await waitFor(() =>
        expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
      )
    }
  )

  test('bookings not loadable: unticking still asks, without an amount', async () => {
    const deletes: string[] = []
    stub('edit', [], { transactions: null, paidAt: TICKED, deletes })
    renderAt('/plan/2026/11?member=u2')
    const dialog = await untickAndConfirm('Miete')
    expect(dialog).toHaveTextContent(i18n.t('plan.untickTextUnknown'))
    expect(deletes).toHaveLength(0)
    fireEvent.click(screen.getByRole('button', { name: i18n.t('plan.untick') }))
    await waitFor(() => expect(deletes).toHaveLength(1))
  })

  test('bookings not loadable: the tick dialog does not pretend date and amount count', async () => {
    stub('edit', [], { transactions: null })
    renderAt('/plan/2026/11?member=u2')
    fireEvent.click(await screen.findByRole('checkbox', { name: tickBox('Miete') }))
    expect(
      await screen.findByText(i18n.t('paidDialog.bookingsUnknown'))
    ).toBeInTheDocument()
  })
})
