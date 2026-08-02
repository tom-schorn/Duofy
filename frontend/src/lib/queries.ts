import {
  useMutation,
  useQuery,
  useQueryClient,
  type UseMutationOptions,
} from '@tanstack/react-query'

import { api } from '@/lib/api'
import type {
  Account,
  Commitment,
  Household,
  HouseholdPlanDetail,
  Invitation,
  Me,
  MyInvitation,
  PlanDetail,
  PlanPosition,
  PlanSummary,
  Transaction,
} from '@/lib/domain'

/**
 * Alle Server-Daten laufen über TanStack Query.
 *
 * Die Schlüssel sind hier gesammelt, damit nach einer Änderung klar ist, was
 * neu geladen werden muss — ein geänderter Posten wirkt sich auf den Plan
 * **und** auf die Übersicht aus.
 */

export const keys = {
  me: ['me'] as const,
  households: ['households'] as const,
  invitations: (householdId: string) =>
    ['households', householdId, 'invitations'] as const,
  myInvitations: ['invitations', 'mine'] as const,
  accounts: ['accounts'] as const,
  commitments: ['commitments'] as const,
  transactions: (year: number, month: number) =>
    ['transactions', year, month] as const,
  plans: ['plans'] as const,
  plan: (year: number, month: number) => ['plans', year, month] as const,
  householdPlan: (householdId: string, year: number, month: number) =>
    ['plans', 'household', householdId, year, month] as const,
}

// --- Nutzer ---------------------------------------------------------------

export function useMe() {
  return useQuery({
    queryKey: keys.me,
    queryFn: () => api.get<Me>('/users/me'),
    // Der eigene Name ändert sich nicht im Minutentakt.
    staleTime: 5 * 60 * 1000,
  })
}

// --- Haushalte ------------------------------------------------------------

export function useHouseholds() {
  return useQuery({
    queryKey: keys.households,
    queryFn: () => api.get<Household[]>('/households'),
  })
}

export function useCreateHousehold() {
  return useInvalidating<Household, { name: string }>(
    (input) => api.post('/households', input),
    [keys.households]
  )
}

export function useUpdateHousehold() {
  return useInvalidating<Household, { id: string } & Partial<Household>>(
    ({ id, ...changes }) => api.patch(`/households/${id}`, changes),
    [keys.households]
  )
}

export function useLeaveHousehold() {
  return useInvalidating<void, string>(
    (householdId) => api.delete(`/households/${householdId}/members/me`),
    [keys.households]
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
    [keys.invitations(householdId)]
  )
}

export function useRevokeInvitation(householdId: string) {
  return useInvalidating<void, string>(
    (invitationId) =>
      api.delete(`/households/${householdId}/invitations/${invitationId}`),
    [keys.invitations(householdId)]
  )
}

/** Offene Einladungen an die eigene Adresse. */
export function useMyInvitations() {
  return useQuery({
    queryKey: keys.myInvitations,
    queryFn: () => api.get<MyInvitation[]>('/households/invitations'),
  })
}

export function useAcceptInvitation() {
  // Nach dem Beitritt ändert sich die Haushaltsliste — und die Pläne, weil
  // fremde Haushaltsposten dazukommen können.
  return useInvalidating<Household, string>(
    (token) => api.post(`/households/invitations/${token}/accept`),
    [keys.myInvitations, keys.households, keys.plans]
  )
}

export function useDeclineInvitation() {
  return useInvalidating<void, string>(
    (token) => api.post(`/households/invitations/${token}/decline`),
    [keys.myInvitations]
  )
}

// --- Konten ---------------------------------------------------------------

export function useAccounts() {
  return useQuery({
    queryKey: keys.accounts,
    queryFn: () => api.get<Account[]>('/accounts'),
  })
}

export function useSaveAccount() {
  // Das Standardkonto wechseln nimmt einem anderen die Markierung — deshalb
  // immer die ganze Liste neu laden, nicht nur den einen Eintrag.
  return useInvalidating<Account, Partial<Account> & { id?: string }>(
    ({ id, ownerId: _o, ...body }) =>
      id ? api.patch(`/accounts/${id}`, body) : api.post('/accounts', body),
    [keys.accounts]
  )
}

export function useDeleteAccount() {
  return useInvalidating<void, string>(
    (id) => api.delete(`/accounts/${id}`),
    [keys.accounts]
  )
}

// --- Haushaltsbuch --------------------------------------------------------

export function useTransactions(year: number, month: number) {
  return useQuery({
    queryKey: keys.transactions(year, month),
    queryFn: () =>
      api.get<Transaction[]>(`/transactions?year=${year}&month=${month}`),
  })
}

/**
 * Eine Buchung sichern.
 *
 * Neben dem Buch muss auch der Plan neu geladen werden: eine zugeordnete
 * Buchung verändert `amountActual` des Postens, und daran hängen die
 * Ist-Beträge in der Budgetansicht.
 */
export function useSaveTransaction(year: number, month: number) {
  return useInvalidating<
    Transaction,
    Partial<Transaction> & { id?: string }
  >(
    ({ id, ownerId: _o, ...body }) =>
      id
        ? api.patch(`/transactions/${id}`, body)
        : api.post('/transactions', body),
    [keys.transactions(year, month), keys.plan(year, month), keys.plans]
  )
}

export function useDeleteTransaction(year: number, month: number) {
  return useInvalidating<void, string>(
    (id) => api.delete(`/transactions/${id}`),
    [keys.transactions(year, month), keys.plan(year, month), keys.plans]
  )
}

// --- Verträge -------------------------------------------------------------

export function useCommitments() {
  return useQuery({
    queryKey: keys.commitments,
    queryFn: () => api.get<Commitment[]>('/commitments'),
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
    // Ein geänderter Vertrag wirkt sich erst auf **künftige** Monate aus —
    // bestehende Posten bleiben. Trotzdem beide neu laden, weil ein neu
    // angelegter Monat sofort davon abhängt.
    [keys.commitments, keys.plans]
  )
}

export function useDeleteCommitment() {
  return useInvalidating<void, string>(
    (id) => api.delete(`/commitments/${id}`),
    [keys.commitments, keys.plans]
  )
}

// --- Pläne ----------------------------------------------------------------

export function usePlans() {
  return useQuery({
    queryKey: keys.plans,
    queryFn: () => api.get<PlanSummary[]>('/plans'),
  })
}

export function usePlan(year: number, month: number, enabled = true) {
  return useQuery({
    queryKey: keys.plan(year, month),
    queryFn: () => api.get<PlanDetail>(`/plans/${year}/${month}`),
    // Ein fehlender Plan ist kein Fehler, den man wiederholen sollte.
    retry: false,
    // Im Haushaltsmodus wird der eigene Plan nicht gebraucht.
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

export function useCreatePlan() {
  return useInvalidating<PlanDetail, { year: number; month: number }>(
    (input) => api.post('/plans', input),
    [keys.plans]
  )
}

export function useConfirmPlan() {
  return useInvalidating<PlanDetail, string>(
    (planId) => api.post(`/plans/${planId}/confirm`),
    [keys.plans]
  )
}

// --- Posten ---------------------------------------------------------------

type PositionInput = PlanPosition & { planId: string }

export function useSavePosition() {
  return useInvalidating<PlanPosition, PositionInput>((input) => {
    const { id, planId, commitmentId: _c, paidAt: _p, ...body } = input
    return id
      ? api.patch(`/positions/${id}`, body)
      : api.post(`/plans/${planId}/positions`, body)
  }, [keys.plans])
}

export function useDeletePosition() {
  return useInvalidating<void, string>(
    (id) => api.delete(`/positions/${id}`),
    [keys.plans]
  )
}

export function useTogglePaid() {
  return useInvalidating<PlanPosition, { id: string; paid: boolean }>(
    ({ id, paid }) =>
      paid ? api.post(`/positions/${id}/paid`) : api.delete(`/positions/${id}/paid`),
    [keys.plans]
  )
}

/**
 * Mutation, die danach aufräumt.
 *
 * Fast jede Änderung berührt mehrere Ansichten — ein Posten ändert den Plan
 * und die Übersicht. Statt das an jeder Stelle einzeln zu schreiben, steht
 * hier einmal, welche Schlüssel danach ungültig sind.
 */
function useInvalidating<TData, TInput>(
  mutationFn: (input: TInput) => Promise<TData>,
  invalidate: readonly (readonly unknown[])[],
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
      options?.onSuccess?.(...args)
    },
  })
}
