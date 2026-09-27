import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, fireEvent, render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { BookPage } from '@/pages/BookPage'

function renderAt(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter([{ path: '/book', element: <BookPage /> }], {
    initialEntries: [path],
  })
  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  )
  return router
}

describe('BookPage month in the address', () => {
  // No plan for any month; everything else the page asks for is an empty list.
  beforeEach(() => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        if (String(url).includes('/plans/')) {
          return new Response(JSON.stringify({ detail: { code: 'plan_not_found' } }), {
            status: 404,
          })
        }
        if (String(url).includes('/accounts/history')) {
          return new Response(JSON.stringify({ openingBalance: '0.00', points: [] }), {
            status: 200,
          })
        }
        return new Response('[]', { status: 200 })
      })
    )
  })
  afterEach(() => vi.unstubAllGlobals())

  test('the month of the address is shown, so a reload keeps it', async () => {
    renderAt('/book?month=2026-09')
    expect(await screen.findByText('September 2026')).toBeInTheDocument()
  })

  test.each(['2026-13', 'abc', '2026-9', ''])(
    'an invalid month %j falls back to the current month',
    async (value) => {
      renderAt(`/book?month=${value}`)
      const now = new Date()
      const label = now.toLocaleDateString('de-DE', { month: 'long', year: 'numeric' })
      expect(await screen.findByText(label)).toBeInTheDocument()
    }
  )

  test('the arrows move the month in the address and Back returns', async () => {
    const router = renderAt('/book?month=2026-12')
    fireEvent.click(await screen.findByRole('button', { name: 'Nächster Monat' }))
    expect(router.state.location.search).toBe('?month=2027-01')
    expect(await screen.findByText('Januar 2027')).toBeInTheDocument()
    await act(() => router.navigate(-1))
    expect(router.state.location.search).toBe('?month=2026-12')
  })
})
