import {
  useMutation,
  useQueries,
  useQuery,
  useQueryClient,
  type UseMutationOptions,
} from '@tanstack/react-query'

import { i18n } from '@/lib/i18n'
import { api } from '@/lib/api'
import { reportMutationError, showsErrorInline } from '@/lib/mutation-error'
import { OWN_SCOPE, euro, scopeKey, scopeQuery } from '@/lib/domain'
import { announce, deleteWithUndo } from '@/lib/undo-delete'
import type {
  AccessLevel,
  Account,
  AreaField,
  BookScope,
  BalanceHistory,
  Category,
  Commitment,
  CommitmentStatus,
  Household,
  HouseholdPlanDetail,
  ImportSummary,
  ImportedEntry,
  InstanceInvitation,
  Invitation,
  Me,
  Member,
  RegistrationMode,
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
  registration: ['registration'] as const,
  instanceInvitations: ['admin', 'invitations'] as const,
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
  imports: ['imports'] as const,
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

// --- Instance (system level, #168) ------------------------------------------

/** Who may register here. Public: the sign-up page asks before anyone is signed in. */
export function useRegistrationMode() {
  return useQuery({
    queryKey: keys.registration,
    queryFn: () => api.get<{ mode: RegistrationMode }>('/auth/registration'),
    staleTime: 5 * 60 * 1000,
  })
}

export function useInstanceInvitations() {
  return useQuery({
    queryKey: keys.instanceInvitations,
    queryFn: () => api.get<InstanceInvitation[]>('/admin/invitations'),
  })
}

export function useCreateInstanceInvitation() {
  return useInvalidating<InstanceInvitation, { email: string | null }>(
    (input) => api.post('/admin/invitations', input),
    [keys.instanceInvitations],
    'toast.instanceInvitationCreated',
    INLINE_ERROR
  )
}

export function useRevokeInstanceInvitation() {
  return useInvalidating<void, string>(
    (id) => api.delete(`/admin/invitations/${id}`),
    [keys.instanceInvitations],
    'toast.instanceInvitationRevoked'
  )
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
    'toast.householdCreated',
    INLINE_ERROR
  )
}

export function useUpdateHousehold() {
  return useInvalidating<Household, { id: string } & Partial<Household>>(
    ({ id, ...changes }) => api.patch(`/households/${id}`, changes),
    [keys.households],
    'toast.householdUpdated',
    INLINE_ERROR
  )
}

export function useLeaveHousehold() {
  return useInvalidating<void, string>(
    (householdId) => api.delete(`/households/${householdId}/members/me`),
    [keys.households],
    'toast.householdLeft',
    // The confirmation dialog shows the error (a last owner cannot leave).
    INLINE_ERROR
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
    'toast.grantUpdated',
    INLINE_ERROR
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
    'toast.invitationSent',
    INLINE_ERROR
  )
}

export function useRevokeInvitation(householdId: string) {
  return useInvalidating<void, string>(
    (invitationId) =>
      api.delete(`/households/${householdId}/invitations/${invitationId}`),
    [keys.invitations(householdId)],
    'toast.invitationRevoked'
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
    'toast.householdJoined',
    INLINE_ERROR
  )
}

export function useDeclineInvitation() {
  return useInvalidating<void, string>(
    (token) => api.post(`/households/invitations/${token}/decline`),
    [keys.myInvitations],
    'toast.invitationDeclined',
    INLINE_ERROR
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
    'toast.accountSaved',
    INLINE_ERROR
  )
}

export function useDeleteAccount() {
  return useInvalidating<void, string>(
    (id) => api.delete(`/accounts/${id}`),
    [keys.accounts],
    'toast.accountDeleted',
    INLINE_ERROR
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
    'toast.transactionSaved',
    INLINE_ERROR
  )
}

/**
 * Delete a booking with „Rückgängig“: it leaves the book at once, the request goes
 * out when the undo window closes.
 */
export function useDeleteTransaction(
  year: number,
  month: number,
  scope: BookScope = OWN_SCOPE
) {
  const client = useQueryClient()
  return (transaction: { id: string; note: string | null; amount: string }) =>
    deleteWithUndo({
      client,
      id: transaction.id,
      name: transaction.note ?? euro.format(Number(transaction.amount)),
      hideIn: [keys.transactions(year, month, scope)],
      invalidate: [
        keys.transactions(year, month, scope),
        keys.plan(year, month),
        keys.plans,
        // Prefix: covers the accounts **and** their history.
        keys.accounts,
      ],
      request: (keepalive) => api.delete(`/transactions/${transaction.id}`, { keepalive }),
    })
}

// --- Commitments ------------------------------------------------------------

/**
 * Commitments — your own, or those of a member who granted insight.
 *
 * The owner is part of the key: otherwise their list would overwrite your own in
 * the cache the moment you switch to them.
 */
export function useCommitments(
  ownerId: string | null = null,
  status: CommitmentStatus = 'all'
) {
  return useQuery({
    queryKey: [...keys.commitmentsOf(ownerId), status] as const,
    queryFn: () => {
      const params = new URLSearchParams({ status })
      if (ownerId !== null) params.set('owner', ownerId)
      return api.get<Commitment[]>(`/commitments?${params}`)
    },
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
    'toast.commitmentSaved',
    INLINE_ERROR
  )
}

export function useDeleteCommitment() {
  return useInvalidating<void, string>(
    (id) => api.delete(`/commitments/${id}`),
    [keys.commitments, keys.plans],
    'toast.commitmentDeleted'
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
  }, [keys.plans], 'toast.planCreated', INLINE_ERROR)
}

// --- Import -----------------------------------------------------------------

/**
 * The parking area of one person — your own, or that of a member who granted
 * insight into the accounts area.
 */
export function useImportedEntries(ownerId: string | null = null) {
  return useQuery({
    queryKey: [...keys.imports, ownerId ?? 'me'] as const,
    queryFn: () =>
      api.get<ImportedEntry[]>(
        ownerId === null ? '/imports' : `/imports?owner=${ownerId}`
      ),
  })
}

/**
 * Upload a bank file.
 *
 * `accountId` is only sent on the second attempt, after the answer said the IBAN
 * belongs to no account yet. It is written onto that account, so the question
 * comes up once per account rather than once per upload.
 */
export function useUploadStatement() {
  return useInvalidating<
    ImportSummary,
    { file: File; ownerId?: string | null; accountId?: string }
  >(({ file, ownerId, accountId }) => {
    const query = new URLSearchParams()
    if (ownerId) query.set('owner', ownerId)
    if (accountId) query.set('account', accountId)
    const suffix = query.size > 0 ? `?${query}` : ''
    return api.upload<ImportSummary>(`/imports${suffix}`, file)
  }, [keys.imports, keys.accounts, keys.allTransactions], undefined, INLINE_ERROR)
}

/**
 * The plans of several months at once, for the import screen.
 *
 * A pile of parked entries usually spans two or three months, and each needs the
 * positions of **its own** month to be assignable. One query per month rather
 * than a new endpoint: they are cached under the same keys the month view uses,
 * so opening a month afterwards costs nothing.
 */
export function usePlansForMonths(
  months: { year: number; month: number }[],
  ownerId: string | null = null
) {
  return useQueries({
    queries: months.map(({ year, month }) => ({
      queryKey: keys.planOf(year, month, ownerId),
      queryFn: () =>
        api.get<PlanDetail>(
          ownerId === null
            ? `/plans/${year}/${month}`
            : `/plans/${year}/${month}?owner=${ownerId}`
        ),
      // A month that was never created is a normal answer here, not a failure.
      retry: false,
    })),
  })
}

/** Put a position, a category or an own account on a parked entry. */
export function useAssignEntry() {
  return useInvalidating<
    ImportedEntry,
    {
      id: string
      positionId?: string | null
      category?: Category | null
      counterAccountId?: string | null
    }
  >(({ id, ...body }) => api.patch(`/imports/${id}`, body), [keys.imports])
}

/**
 * Take a suggestion and book it, in one go.
 *
 * Two requests rather than one endpoint: assigning and booking stay separate
 * everywhere else, and a combined one would be a third way to do the same
 * thing. The button is what joins them, not the API.
 */
export function useAcceptSuggestion() {
  return useInvalidating<
    ImportedEntry,
    {
      id: string
      category?: Category | null
      positionId?: string | null
      counterAccountId?: string | null
    }
  >(
    async ({ id, category, positionId, counterAccountId }) => {
      // One of the three, in the order that settles the most: an own account
      // makes it a transfer and clears the rest, a position brings its own
      // category, a category stands alone.
      const body = counterAccountId
        ? { counterAccountId }
        : positionId
          ? { positionId }
          : { category }
      await api.patch<ImportedEntry>(`/imports/${id}`, body)
      return api.post<ImportedEntry>(`/imports/${id}/book`)
    },
    [keys.imports, keys.accounts, keys.allTransactions, keys.plans],
    'toast.booked'
  )
}

/** Turn a parked entry into a booking. The parked row is gone afterwards. */
export function useBookEntry() {
  return useInvalidating<ImportedEntry, string>(
    (id) => api.post(`/imports/${id}/book`),
    [keys.imports, keys.accounts, keys.allTransactions, keys.plans],
    'toast.booked'
  )
}

/** Throw an entry out. The row stays, so a second import does not bring it back. */
export function useDiscardEntry() {
  return useInvalidating<ImportedEntry, string>(
    (id) => api.delete(`/imports/${id}`),
    [keys.imports],
    'toast.discarded'
  )
}

// --- Positions --------------------------------------------------------------

type PositionInput = PlanPosition & { planId: string }

export function useSavePosition() {
  return useInvalidating<PlanPosition, PositionInput>((input) => {
    const { id, planId, commitmentId: _c, paidAt: _p, ...body } = input
    return id
      ? api.patch(`/positions/${id}`, body)
      : api.post(`/plans/${planId}/positions`, body)
  }, [keys.plans], 'toast.positionSaved', INLINE_ERROR)
}

/**
 * Delete a position with „Rückgängig“: it leaves the plan at once, the request goes
 * out when the undo window closes.
 */
export function useDeletePosition() {
  const client = useQueryClient()
  return (position: { id: string; label: string }) =>
    deleteWithUndo({
      client,
      id: position.id,
      name: position.label,
      hideIn: [keys.plans],
      invalidate: [keys.plans],
      request: (keepalive) => api.delete(`/positions/${position.id}`, { keepalive }),
    })
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
    {
      id: string
      paid: boolean
      occurredOn?: string
      amount?: string
      /** The caller shows the error in its own dialog — the net stays quiet. */
      inlineError?: boolean
      /** The position already has bookings: ticking then books nothing. */
      hasBookings?: boolean
    }
  >(
    ({ id, paid, occurredOn, amount }) =>
      paid
        ? api.post(`/positions/${id}/paid`, { occurredOn, amount })
        : api.delete(`/positions/${id}/paid`),
    [keys.plans, keys.allTransactions, keys.accounts],
    undefined,
    {
      // Says what happened: which position, and how much was booked.
      onSuccess: (position, { paid, amount, hasBookings }) => {
        if (!paid) {
          return announce('success', i18n.t('toast.unticked', { label: position.label }))
        }
        if (hasBookings) {
          return announce('success', i18n.t('toast.tickedKept', { label: position.label }))
        }
        announce(
          'success',
          i18n.t(position.budget === 'income' ? 'toast.tickedIncome' : 'toast.ticked', {
            label: position.label,
            amount: euro.format(Number(amount ?? position.amountPlanned)),
          })
        )
      },
    }
  )
}

/** For callers that show the error in their own form — the shared net skips them. */
const INLINE_ERROR = { meta: { inlineError: true } }

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
   * The catalog key of what the toast says (`toast.*`). In **one** place rather
   * than at every caller — otherwise half the actions give feedback and the other
   * half do not.
   *
   * Errors stay out of it: those belong in the form, next to the field they
   * concern. What has no place of its own is caught below by the shared net.
   */
  successKey?: string,
  options?: UseMutationOptions<TData, Error, TInput>
) {
  const client = useQueryClient()
  const mutation = useMutation<TData, Error, TInput>({
    mutationFn,
    ...options,
    onError: (error, variables, ...rest) => {
      // The shared net: a change that failed must never vanish silently. Callers
      // that show the error in their form say so with INLINE_ERROR.
      if (!showsErrorInline(options?.meta, variables)) {
        reportMutationError(error, () => mutation.mutate(variables))
      }
      options?.onError?.(error, variables, ...rest)
    },
    onSuccess: (...args) => {
      for (const key of invalidate) {
        client.invalidateQueries({ queryKey: key })
      }
      if (successKey) announce('success', i18n.t(successKey))
      options?.onSuccess?.(...args)
    },
  })
  return mutation
}
