import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { useSaveAccount, useSaveCommitment } from '@/lib/queries'
import type { Account, Commitment } from '@/lib/domain'

/**
 * Creating something *for* a member used to drop `ownerId` on the floor: the
 * dialog set it on the draft, but the mutation destructured it out and posted
 * to `/commitments` or `/accounts` with no `owner`, which the backend reads as
 * "for me" — a contract or account meant for the person being stood in for
 * landed on the helper's own list instead, silently (#217).
 */

const commitment: Commitment = {
  id: '',
  ownerId: 'u2',
  deletable: false,
  type: 'contract',
  name: 'Miete',
  amount: '500.00',
  category: 'housing.rent',
  budget: 'needs',
  isLimit: false,
  householdId: null,
  intervalMonths: 1,
  firstDueDate: '2026-10-01',
  endsOn: null,
  passThrough: false,
  counterAccountId: null,
  targetAmount: null,
  targetDate: null,
  paymentMethod: null,
  accountId: null,
}

const account: Partial<Account> & { id?: string; ownerId?: string } = {
  ownerId: 'u2',
  name: 'Girokonto',
  type: 'checking',
  openingBalance: '0.00',
  openingDate: '2026-01-01',
  isDefault: true,
  active: true,
  externalRef: null,
  countsAsAvailable: true,
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
  fetchMock = vi.fn(async () => new Response(JSON.stringify({ id: 'new' }), { status: 201 }))
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => vi.unstubAllGlobals())

describe('useSaveCommitment', () => {
  test('creating with an ownerId puts it on the owner, not the caller', async () => {
    const { result } = renderHook(() => useSaveCommitment(), { wrapper: wrapper() })
    result.current.mutate(commitment)
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(
      calls().some(([url, method]) => /\/commitments\?owner=u2$/.test(url) && method === 'POST')
    ).toBe(true)
  })

  test('creating without an ownerId asks for the caller’s own, as before', async () => {
    const { result } = renderHook(() => useSaveCommitment(), { wrapper: wrapper() })
    result.current.mutate({ ...commitment, ownerId: undefined })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(calls().some(([url, method]) => /\/commitments$/.test(url) && method === 'POST')).toBe(
      true
    )
  })

  test('editing never adds an owner — the id on the path already says whose it is', async () => {
    const { result } = renderHook(() => useSaveCommitment(), { wrapper: wrapper() })
    result.current.mutate({ ...commitment, id: 'c1' })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(calls().some(([url, method]) => /\/commitments\/c1$/.test(url) && method === 'PATCH')).toBe(
      true
    )
  })
})

describe('useSaveAccount', () => {
  test('creating with an ownerId puts it on the owner, not the caller', async () => {
    const { result } = renderHook(() => useSaveAccount(), { wrapper: wrapper() })
    result.current.mutate(account)
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(
      calls().some(([url, method]) => /\/accounts\?owner=u2$/.test(url) && method === 'POST')
    ).toBe(true)
  })

  test('editing never adds an owner — the id on the path already says whose it is', async () => {
    const { result } = renderHook(() => useSaveAccount(), { wrapper: wrapper() })
    result.current.mutate({ ...account, id: 'a1' })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(calls().some(([url, method]) => /\/accounts\/a1$/.test(url) && method === 'PATCH')).toBe(
      true
    )
  })
})
