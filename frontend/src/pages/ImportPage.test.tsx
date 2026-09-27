import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { Toaster } from '@/components/ui/sonner'
import { i18n } from '@/lib/i18n'
import { flushPendingDelete } from '@/lib/undo-delete'
import { ImportPage } from '@/pages/ImportPage'

const entry = {
  id: 'e1',
  occurredOn: '2026-09-03',
  counterpartyName: 'Muster Markt',
  purpose: null,
  amount: '12.50',
  incoming: false,
  category: null,
  budget: null,
  positionId: null,
  counterAccountId: null,
  suggestion: null,
  discardedAt: null,
}

let fetchMock: ReturnType<typeof vi.fn>

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter([{ path: '/', element: <ImportPage /> }])
  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
      <Toaster />
    </QueryClientProvider>
  )
}

describe('ImportPage', () => {
  beforeEach(() => {
    fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
      if (init?.method === 'DELETE') return new Response(null, { status: 204 })
      if (String(url).includes('/imports')) return new Response(JSON.stringify([entry]), { status: 200 })
      return new Response('[]', { status: 200 })
    })
    vi.stubGlobal('fetch', fetchMock)
  })
  afterEach(() => {
    act(() => flushPendingDelete())
    vi.unstubAllGlobals()
  })

  test('Verwerfen removes the row at once and Rückgängig brings it back without sending anything', async () => {
    const user = userEvent.setup()
    renderPage()
    await user.click(await screen.findByRole('button', { name: i18n.t('import.discard') }))
    expect(screen.queryByText('Muster Markt')).not.toBeInTheDocument()

    await user.click(await screen.findByRole('button', { name: i18n.t('ui.undo') }))
    expect(await screen.findByText('Muster Markt')).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'DELETE')).toBe(false)
  })
})
