import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { useFlow, useSetFlowLimitsBy } from '@/lib/queries'

const flow = {
  year: 2026,
  month: 9,
  flowLimitsBy: 'plan',
  start: '0.00',
  entries: [],
  days: [],
  hints: [],
}

let fetchMock: ReturnType<typeof vi.fn>

function wrapper() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  )
}

const calls = () =>
  fetchMock.mock.calls.map(([url, init]) => [String(url), (init as RequestInit)?.method ?? 'GET'])

beforeEach(() => {
  fetchMock = vi.fn(async () => new Response(JSON.stringify(flow), { status: 200 }))
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => vi.unstubAllGlobals())

describe('useFlow', () => {
  test('asks for your own flow without an owner', async () => {
    const { result } = renderHook(() => useFlow(2026, 9, {}), { wrapper: wrapper() })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(calls()[0][0]).toMatch(/\/plans\/2026\/9\/flow$/)
  })

  test('asks for the flow of a member with the owner in the query', async () => {
    const { result } = renderHook(() => useFlow(2026, 9, { ownerId: 'u2' }), {
      wrapper: wrapper(),
    })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(calls()[0][0]).toMatch(/\/plans\/2026\/9\/flow\?owner=u2$/)
  })

  test('asks for the household flow on the household route', async () => {
    const { result } = renderHook(() => useFlow(2026, 9, { householdId: 'h1' }), {
      wrapper: wrapper(),
    })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(calls()[0][0]).toMatch(/\/plans\/household\/h1\/2026\/9\/flow$/)
  })
})

describe('useSetFlowLimitsBy', () => {
  test('sends the setting to the user and loads the flow again', async () => {
    const both = renderHook(
      () => ({ flow: useFlow(2026, 9, {}), save: useSetFlowLimitsBy() }),
      { wrapper: wrapper() }
    )
    await waitFor(() => expect(both.result.current.flow.isSuccess).toBe(true))

    both.result.current.save.mutate('bookings')

    await waitFor(() =>
      expect(calls().some(([url, method]) => /\/users\/me$/.test(url) && method === 'PATCH')).toBe(
        true
      )
    )
    const patch = fetchMock.mock.calls.find(([, init]) => (init as RequestInit)?.method === 'PATCH')
    expect(JSON.parse((patch![1] as RequestInit).body as string)).toEqual({
      flowLimitsBy: 'bookings',
    })
    await waitFor(() =>
      expect(calls().filter(([url]) => /\/flow$/.test(url)).length).toBeGreaterThanOrEqual(2)
    )
  })
})
