import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { toast } from 'sonner'
import { afterEach, describe, expect, test, vi } from 'vitest'

import { useCreatePlan, useSaveCommitment, useSaveTransaction, useSetDefaultQuota } from '@/lib/queries'

function wrapper({ children }: { children: ReactNode }) {
  return <QueryClientProvider client={new QueryClient()}>{children}</QueryClientProvider>
}

function answer(body: object) {
  vi.stubGlobal(
    'fetch',
    vi.fn(
      async () =>
        new Response(JSON.stringify(body), {
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

describe('success messages name the thing', () => {
  test('a new booking says what and how much was booked', async () => {
    answer({ id: 't', kind: 'booking', note: 'Einkauf', amount: '42.10' })
    const success = vi.spyOn(toast, 'success').mockImplementation(() => 1)
    const { result } = renderHook(() => useSaveTransaction(2026, 10), { wrapper })
    result.current.mutate({ amount: '42.10', note: 'Einkauf' })
    await waitFor(() => expect(success).toHaveBeenCalled())
    expect(success.mock.calls[0][0]).toMatch(/^Einkauf 42,10.*gebucht$/)
  })

  test('a saved commitment says its name', async () => {
    answer({ id: 'c', name: 'Miete' })
    const success = vi.spyOn(toast, 'success').mockImplementation(() => 1)
    const { result } = renderHook(() => useSaveCommitment(), { wrapper })
    result.current.mutate({ id: 'c', name: 'Miete' } as never)
    await waitFor(() => expect(success).toHaveBeenCalled())
    expect(success.mock.calls[0][0]).toBe('Miete gespeichert')
  })

  test('a created month says which month', async () => {
    answer({ id: 'p', year: 2026, month: 10 })
    const success = vi.spyOn(toast, 'success').mockImplementation(() => 1)
    const { result } = renderHook(() => useCreatePlan(), { wrapper })
    result.current.mutate({ year: 2026, month: 10 })
    await waitFor(() => expect(success).toHaveBeenCalled())
    expect(success.mock.calls[0][0]).toBe('Monat Oktober 2026 angelegt')
  })

  test('saved quotas name the three shares', async () => {
    answer({ id: 'u', targetNeeds: '50.00', targetWants: '30.00', targetSavings: '20.00' })
    const success = vi.spyOn(toast, 'success').mockImplementation(() => 1)
    const { result } = renderHook(() => useSetDefaultQuota(), { wrapper })
    result.current.mutate({ targetNeeds: '50', targetWants: '30', targetSavings: '20' })
    await waitFor(() => expect(success).toHaveBeenCalled())
    expect(success.mock.calls[0][0]).toBe('Richtwerte gespeichert: 50 / 30 / 20')
  })
})
