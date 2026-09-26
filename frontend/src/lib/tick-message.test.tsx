import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { toast } from 'sonner'
import { afterEach, describe, expect, test, vi } from 'vitest'

import { useTogglePaid } from '@/lib/queries'

function wrapper({ children }: { children: ReactNode }) {
  return <QueryClientProvider client={new QueryClient()}>{children}</QueryClientProvider>
}

function stubPosition(budget: string) {
  vi.stubGlobal(
    'fetch',
    vi.fn(
      async () =>
        new Response(JSON.stringify({ id: 'p', label: 'Miete', budget, amountPlanned: '950.00' }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        })
    )
  )
}

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe('ticking off', () => {
  test('says which position was booked and how much', async () => {
    stubPosition('needs')
    const success = vi.spyOn(toast, 'success').mockImplementation(() => 1)
    const { result } = renderHook(() => useTogglePaid(), { wrapper })
    result.current.mutate({ id: 'p', paid: true, amount: '950.00' })
    await waitFor(() => expect(success).toHaveBeenCalled())
    expect(success.mock.calls[0][0]).toMatch(/^Miete abgehakt: 950,00.*gebucht$/)
  })

  test('says received for income', async () => {
    stubPosition('income')
    const success = vi.spyOn(toast, 'success').mockImplementation(() => 1)
    const { result } = renderHook(() => useTogglePaid(), { wrapper })
    result.current.mutate({ id: 'p', paid: true })
    await waitFor(() => expect(success).toHaveBeenCalled())
    expect(success.mock.calls[0][0]).toMatch(/^Miete abgehakt: 950,00.*erhalten$/)
  })
})
