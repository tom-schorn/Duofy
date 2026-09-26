import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, fireEvent, render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { PlanDetailPage } from '@/pages/PlanDetailPage'

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

  test('without the right to edit the plan there is no create button', async () => {
    renderAt('/plan/2026/11?member=u2')
    expect(await screen.findByText('November 2026 ist noch nicht angelegt')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Monat anlegen' })).not.toBeInTheDocument()
  })
})
