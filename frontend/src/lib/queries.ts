import {
  useMutation,
  useQuery,
  useQueryClient,
  type UseMutationOptions,
} from '@tanstack/react-query'

import { toast } from 'sonner'

import { api } from '@/lib/api'
import { OWN_SCOPE, scopeKey, scopeQuery } from '@/lib/domain'
import type {
  AccessLevel,
  Account,
  AreaField,
  BookScope,
  BalanceHistory,
  Commitment,
  Household,
  HouseholdPlanDetail,
  Invitation,
  Me,
  Member,
  MyInvitation,
  PlanDetail,
  PlanPosition,
  PlanSummary,
  Transaction,
} from '@/lib/domain'

/**
 * All server data goes through TanStack Query.
 *
 * The keys are collected here so that after a change it is clear what has to be
 * reloaded — a changed position affects the plan **and** the overview.
 */

export const keys = {
  me: ['me'] as const,
  households: ['households'] as const,
  invitations: (householdId: string) =>
    ['households', householdId, 'invitations'] as const,
  myInvitations: ['invitations', 'mine'] as const,
  accounts: ['accounts'] as const,
  accountsIn: (scope: BookScope) => ['accounts', scopeKey(scope)] as const,
  commitments: ['commitments'] as const,
  commitmentsOf: (ownerId: string | null) => ['commitments', ownerId ?? 'me'] as const,
  /** Every month — for invalidating when it is unclear which one is affected. */
  allTransactions: ['transactions'] as const,
  balanceHistory: (
    year: number,
    month: number,
    scope: BookScope = OWN_SCOPE,
    onlyAvailable = false
  ) =>
    [
      'accounts',
      'history',
      scopeKey(scope),
      year,
      month,
      onlyAvailable ? 'frei' : 'alle',
    ] as const,
  transactions: (year: number, month: number, scope: BookScope = OWN_SCOPE) =>
    ['transactions', scopeKey(scope), year, month] as const,
  plans: ['plans'] as const,
  /** Prefix of every single-person plan — invalidating it hits yours and theirs. */
  plan: (year: number, month: number) => ['plans', year, month] as const,
  planOf: (year: number, month: number, ownerId: string | null) =>
    ['plans', year, month, ownerId ?? 'me'] as const,
  householdPlan: (householdId: string, year: number, month: number) =>
    ['plans', 'household', householdId, year, month] as const,
}

// --- User -----------------------------------------------------------------

export function useMe() {
  return useQuery({
    queryKey: keys.me,
    queryFn: () => api.get<Me>('/users/me'),
    // Your own name does not change by the minute.
    staleTime: 5 * 60 * 1000,
  })
}

// --- Households -----------------------------------------------------------

export function useHouseholds() {
  return useQuery({
    queryKey: keys.households,
    queryFn: () => api.get<Household[]>('/households'),
  })
}

export function useCreateHousehold() {
  return useInvalidating<Household, { name: string }>(
    (input) => api.post('/households', input),
    [keys.households],
    'Haushalt angelegt'
  )
}

export function useUpdateHousehold() {
  return useInvalidating<Household, { id: string } & Partial<Household>>(
    ({ id, ...changes }) => api.patch(`/households/${id}`, changes),
    [keys.households],
    'Haushalt geändert'
  )
}

export function useLeaveHousehold() {
  return useInvalidating<void, string>(
    (householdId) => api.delete(`/households/${householdId}/members/me`),
    [keys.households],
    'Haushalt verlassen'
  )
}

/**
 * Change your own access levels in a household.
 *
 * Your own only, hence no member in the call. One area at a time: the endpoint
 * leaves out what the call does not mention, so the other two keep their level.
 * Afterwards what the others see changes, so everything shared is reloaded.
 */
export function useSetMyAccess(householdId: string) {
  return useInvalidating<Member, Partial<Record<AreaField, AccessLevel>>>(
    (grants) => api.patch(`/households/${householdId}/members/me`, grants),
    [keys.households, keys.plans, keys.accounts, keys.commitments],
    'Freigabe geändert'
  )
}

export function useInvitations(householdId: string | null) {
  return useQuery({
    queryKey: keys.invitations(householdId ?? ''),
    queryFn: () => api.get<Invitation[]>(`/households/${householdId}/invitations`),
    enabled: householdId !== null,
  })
}

export function useInvite(householdId: string) {
  return useInvalidating<Invitation, { email: string }>(
    (input) => api.post(`/households/${householdId}/invitations`, input),
    [keys.invitations(householdId)],
    'Einladung verschickt'
  )
}

export function useRevokeInvitation(householdId: string) {
  return useInvalidating<void, string>(
    (invitationId) =>
      api.delete(`/households/${householdId}/invitations/${invitationId}`),
    [keys.invitations(householdId)],
    'Einladung zurückgezogen'
  )
}

/** Pending invitations addressed to you. */
export function useMyInvitations() {
  return useQuery({
    queryKey: keys.myInvitations,
    queryFn: () => api.get<MyInvitation[]>('/households/invitations'),
  })
}

export function useAcceptInvitation() {
  // Joining changes the household list — and the plans, because other people
  // shared positions can appear.
  return useInvalidating<Household, string>(
    (token) => api.post(`/households/invitations/${token}/accept`),
    [keys.myInvitations, keys.households, keys.plans],
    'Haushalt beigetreten'
  )
}

export function useDeclineInvitation() {
  return useInvalidating<void, string>(
    (token) => api.post(`/households/invitations/${token}/decline`),
    [keys.myInvitations],
    'Einladung abgelehnt'
  )
}

// --- Accounts ---------------------------------------------------------------

/**
 * Accounts including balances. With `ownerId`, those of a member who granted
 * insight.
 *
 * The owner is part of the key: otherwise their account list would overwrite your
 * own in the cache as soon as you open their page.
 */
export function useAccounts(scope: BookScope = OWN_SCOPE) {
  return useQuery({
    queryKey: keys.accountsIn(scope),
    // The suffix starts with `&`, hence the empty `?` in front of it.
    queryFn: () => api.get<Account[]>(`/accounts?${scopeQuery(scope).slice(1)}`),
  })
}

/**
 * The real balance history of a calendar month.
 *
 * Its own endpoint rather than derived from the bookings: the book groups by
 * position, this curve by date. Both cannot be read off the same list.
 */
/**
 * The history of a calendar month.
 *
 * `onlyAvailable` looks at the accounts that count as spendable only. That is the
 * only way bars and balance line add up: a transfer to savings leaves the pot and
 * lowers the line by exactly the bar it produces.
 */
export function useBalanceHistory(
  year: number,
  month: number,
  scope: BookScope = OWN_SCOPE,
  onlyAvailable = false
) {
  return useQuery({
    queryKey: keys.balanceHistory(year, month, scope, onlyAvailable),
    queryFn: () =>
      api.get<BalanceHistory>(
        `/accounts/history?year=${year}&month=${month}` +
          `${scopeQuery(scope)}${onlyAvailable ? '&onlyAvailable=true' : ''}`
      ),
  })
}

export function useSaveAccount() {
  // Switching the default account clears the flag on another one — so always
  // reload the whole list, not just the single entry.
  return useInvalidating<Account, Partial<Account> & { id?: string }>(
    ({ id, ownerId: _o, ...body }) =>
      id ? api.patch(`/accounts/${id}`, body) : api.post('/accounts', body),
    [keys.accounts],
    'Konto gespeichert'
  )
}

export function useDeleteAccount() {
  return useInvalidating<void, string>(
    (id) => api.delete(`/accounts/${id}`),
    [keys.accounts],
    'Konto gelöscht'
  )
}

// --- Household book -------------------------------------------------------

export function useTransactions(
  year: number,
  month: number,
  scope: BookScope = OWN_SCOPE
) {
  return useQuery({
    queryKey: keys.transactions(year, month, scope),
    queryFn: () =>
      api.get<Transaction[]>(
        `/transactions?year=${year}&month=${month}${scopeQuery(scope)}`
      ),
  })
}

/**
 * Save a booking.
 *
 * Besides the book, the plan has to be reloaded too: an assigned booking changes
 * the position `amountActual`, and the actual figures in the budget view hang off
 * that.
 */
export function useSaveTransaction(
  year: number,
  month: number,
  scope: BookScope = OWN_SCOPE
) {
  return useInvalidating<
    Transaction,
    Partial<Transaction> & { id?: string }
  >(
    ({ id, ownerId: _o, ...body }) =>
      id
        ? api.patch(`/transactions/${id}`, body)
        : api.post('/transactions', body),
    [
      keys.transactions(year, month, scope),
      keys.plan(year, month),
      keys.plans,
      // Prefix: covers the accounts **and** their history.
      keys.accounts,
    ],
    'Buchung gespeichert'
  )
}

export function useDeleteTransaction(
  year: number,
  month: number,
  scope: BookScope = OWN_SCOPE
) {
  return useInvalidating<void, string>(
    (id) => api.delete(`/transactions/${id}`),
    [
      keys.transactions(year, month, scope),
      keys.plan(year, month),
      keys.plans,
      // Prefix: covers the accounts **and** their history.
      keys.accounts,
    ],
    'Buchung gelöscht'
  )
}

// --- Commitments ------------------------------------------------------------

/**
 * Commitments — your own, or those of a member who granted insight.
 *
 * The owner is part of the key: otherwise their list would overwrite your own in
 * the cache the moment you switch to them.
 */
export function useCommitments(ownerId: string | null = null) {
  return useQuery({
    queryKey: keys.commitmentsOf(ownerId),
    queryFn: () =>
      api.get<Commitment[]>(ownerId === null ? '/commitments' : `/commitments?owner=${ownerId}`),
  })
}

export function useSaveCommitment() {
  return useInvalidating<Commitment, Commitment>(
    (input) => {
      const { id, ownerId: _ownerId, ...body } = input
      return id
        ? api.patch(`/commitments/${id}`, body)
        : api.post('/commitments', body)
    },
    // A changed commitment only affects **future** months — existing positions
    // stay. Reload both anyway, because a newly created month depends on it
    // immediately.
    [keys.commitments, keys.plans],
    'Vertrag gespeichert'
  )
}

export function useDeleteCommitment() {
  return useInvalidating<void, string>(
    (id) => api.delete(`/commitments/${id}`),
    [keys.commitments, keys.plans],
    'Vertrag gelöscht'
  )
}

// --- Plans ------------------------------------------------------------------

/**
 * Every month, newest first — your own or those of a member who granted insight.
 *
 * The owner is part of the key, otherwise their months would overwrite yours in
 * the cache the moment you switch.
 */
export function usePlans(ownerId: string | null = null) {
  return useQuery({
    queryKey: [...keys.plans, ownerId ?? 'me'] as const,
    queryFn: () =>
      api.get<PlanSummary[]>(ownerId === null ? '/plans' : `/plans?owner=${ownerId}`),
  })
}

/**
 * One month, whole — your own or that of a member who granted insight.
 *
 * Not the shared plan: that one merges everyone and shows only positions with a
 * household. Here one person stands alone, private positions included.
 */
export function usePlan(
  year: number,
  month: number,
  enabled = true,
  ownerId: string | null = null
) {
  return useQuery({
    queryKey: keys.planOf(year, month, ownerId),
    queryFn: () =>
      api.get<PlanDetail>(
        ownerId === null
          ? `/plans/${year}/${month}`
          : `/plans/${year}/${month}?owner=${ownerId}`
      ),
    // A missing plan is not an error worth retrying.
    retry: false,
    // In household mode no single person's plan is needed.
    enabled,
  })
}

export function useHouseholdPlan(
  householdId: string | null,
  year: number,
  month: number
) {
  return useQuery({
    queryKey: keys.householdPlan(householdId ?? '', year, month),
    queryFn: () =>
      api.get<HouseholdPlanDetail>(
        `/plans/household/${householdId}/${year}/${month}`
      ),
    enabled: householdId !== null,
    retry: false,
  })
}

/**
 * Create a month — your own, or that of a member who granted `edit`.
 *
 * The owner goes into the query string, never into the body: the endpoint reads
 * the plan **and** the commitments it grows from off that one value, and a body
 * field would have looked like data rather than like a target.
 */
export function useCreatePlan() {
  return useInvalidating<
    PlanDetail,
    { year: number; month: number; ownerId?: string | null }
  >(({ ownerId, ...body }) => {
    const path = ownerId ? `/plans?owner=${ownerId}` : '/plans'
    return api.post(path, body)
  }, [keys.plans], 'Monat angelegt')
}

// --- Positions --------------------------------------------------------------

type PositionInput = PlanPosition & { planId: string }

export function useSavePosition() {
  return useInvalidating<PlanPosition, PositionInput>((input) => {
    const { id, planId, commitmentId: _c, paidAt: _p, ...body } = input
    return id
      ? api.patch(`/positions/${id}`, body)
      : api.post(`/plans/${planId}/positions`, body)
  }, [keys.plans], 'Posten gespeichert')
}

export function useDeletePosition() {
  return useInvalidating<void, string>(
    (id) => api.delete(`/positions/${id}`),
    [keys.plans],
    'Posten gelöscht'
  )
}

/**
 * Tick a position off, or take the tick away.
 *
 * When ticking, the date and amount of the created booking may come along — the
 * payment often happened a few days ago. Without those two fields the backend books
 * today and the planned amount.
 *
 * Invalidates the bookings as well: ticking creates and deletes them, and the book
 * would otherwise keep showing what it showed before the click.
 */
export function useTogglePaid() {
  return useInvalidating<
    PlanPosition,
    { id: string; paid: boolean; occurredOn?: string; amount?: string }
  >(
    ({ id, paid, occurredOn, amount }) =>
      paid
        ? api.post(`/positions/${id}/paid`, { occurredOn, amount })
        : api.delete(`/positions/${id}/paid`),
    [keys.plans, keys.allTransactions, keys.accounts],
    'Posten aktualisiert'
  )
}

/**
 * A mutation that cleans up after itself.
 *
 * Almost every change touches several views — a position changes the plan and the
 * overview. Rather than repeating that at every call site, the keys to invalidate
 * are declared once, here.
 */
function useInvalidating<TData, TInput>(
  mutationFn: (input: TInput) => Promise<TData>,
  invalidate: readonly (readonly unknown[])[],
  /**
   * What the toast says. In **one** place rather than at every caller — otherwise
   * half the actions give feedback and the other half do not.
   *
   * Errors stay out of it: those belong in the form, next to the field they
   * concern. A toast that flies away is the wrong place for something that needs
   * correcting.
   */
  erfolg?: string,
  options?: UseMutationOptions<TData, Error, TInput>
) {
  const client = useQueryClient()
  return useMutation<TData, Error, TInput>({
    mutationFn,
    ...options,
    onSuccess: (...args) => {
      for (const key of invalidate) {
        client.invalidateQueries({ queryKey: key })
      }
      if (erfolg) toast.success(erfolg)
      options?.onSuccess?.(...args)
    },
  })
}
