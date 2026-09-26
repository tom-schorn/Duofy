import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { PlanDetailPage } from '@/pages/PlanDetailPage'

function renderAt(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/plan/:year/:month" element={<PlanDetailPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  )
}

describe('PlanDetailPage', () => {
  // The month has no plan; everything else the page asks for is an empty list.
  beforeEach(() => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) =>
        String(url).includes('/plans/')
          ? new Response(JSON.stringify({ detail: { code: 'plan_not_found' } }), {
              status: 404,
            })
          : new Response('[]', { status: 200 })
      )
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
})
