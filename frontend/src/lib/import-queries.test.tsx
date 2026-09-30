import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { useAcceptSuggestion } from '@/lib/queries'

/**
 * Assigning a parked line and booking it (#254). The server keeps a position
 * over a category sent next to it, so which field goes out decides where the
 * money lands: an own account over a position over a category.
 */

let fetchMock: ReturnType<typeof vi.fn>

function wrapper() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  )
}

/** The body of the one PATCH the assignment sent. */
function patchBody() {
  const patch = fetchMock.mock.calls.find(([, init]) => init?.method === 'PATCH')
  return JSON.parse(String(patch?.[1]?.body))
}

async function accept(assignment: Parameters<ReturnType<typeof useAcceptSuggestion>['mutate']>[0]) {
  const { result } = renderHook(() => useAcceptSuggestion(), { wrapper: wrapper() })
  result.current.mutate(assignment)
  await waitFor(() => expect(result.current.isSuccess).toBe(true))
}

describe('assigning and booking a parked line', () => {
  beforeEach(() => {
    fetchMock = vi.fn(
      async () =>
        new Response(JSON.stringify({ id: 'e1', amount: '12.50', counterpartyName: 'Muster Markt' }), {
          status: 200,
        })
    )
    vi.stubGlobal('fetch', fetchMock)
  })
  afterEach(() => vi.unstubAllGlobals())

  test('a position wins over the category that comes with it', async () => {
    await accept({ id: 'e1', positionId: 'p1', category: 'housing.rent' })
    expect(patchBody()).toEqual({ positionId: 'p1' })
  })

  test('an own account wins over position and category', async () => {
    await accept({ id: 'e1', positionId: 'p1', category: 'housing.rent', counterAccountId: 'a2' })
    expect(patchBody()).toEqual({ counterAccountId: 'a2' })
  })

  test('a category alone clears a parked position, so the line does not book on it', async () => {
    await accept({ id: 'e1', positionId: null, category: 'household.groceries' })
    expect(patchBody()).toEqual({ positionId: null, category: 'household.groceries' })
  })

  test('books after assigning', async () => {
    await accept({ id: 'e1', category: 'household.groceries' })
    const writes = fetchMock.mock.calls
      .filter(([, init]) => init?.method === 'PATCH' || init?.method === 'POST')
      .map(([url, init]) => `${init?.method} ${String(url).replace(/^.*\/imports/, '/imports')}`)
    expect(writes).toEqual(['PATCH /imports/e1', 'POST /imports/e1/book'])
  })
})
