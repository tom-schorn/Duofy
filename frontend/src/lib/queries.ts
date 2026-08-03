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
  BookScope,
  BalanceHistory,
  Commitment,
  Household,
  HouseholdPlanDetail,
  Invitation,
  Me,
  Member,
  MemberPlanDetail,
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
  accountsIn: (scope: BookScope) => ['accounts', scopeKey(scope)] as const,
  commitments: ['commitments'] as const,
  /** Alle Monate — zum Ungültigmachen, wenn unklar ist, welcher betroffen ist. */
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
  plan: (year: number, month: number) => ['plans', year, month] as const,
  householdPlan: (householdId: string, year: number, month: number) =>
    ['plans', 'household', householdId, year, month] as const,
  memberPlan: (ownerId: string, year: number, month: number) =>
    ['plans', 'member', ownerId, year, month] as const,
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
 * Stellt die eigene Freigabe in einem Haushalt um.
 *
 * Nur die eigene — deshalb kein Mitglied im Aufruf. Danach ändert sich, was
 * der Partner sieht, also müssen Pläne und Konten neu geladen werden.
 */
export function useSetMyAccess(householdId: string) {
  return useInvalidating<Member, AccessLevel>(
    (grantsAccess) =>
      api.patch(`/households/${householdId}/members/me`, { grantsAccess }),
    [keys.households, keys.plans, keys.accounts],
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

// --- Konten ---------------------------------------------------------------

/**
 * Konten mit Stand. Mit `ownerId` die eines Mitglieds, das Einblick gegeben hat.
 *
 * Der Besitzer steht im Schlüssel: sonst überschriebe Jasmins Kontoliste die
 * eigene, sobald man ihre Seite öffnet.
 */
export function useAccounts(scope: BookScope = OWN_SCOPE) {
  return useQuery({
    queryKey: keys.accountsIn(scope),
    // Der Anhang beginnt mit `&`, deshalb steht ein leeres `?` davor.
    queryFn: () => api.get<Account[]>(`/accounts?${scopeQuery(scope).slice(1)}`),
  })
}

/**
 * Der echte Kontostandverlauf eines Kalendermonats.
 *
 * Eigener Endpunkt statt aus den Buchungen gerechnet: das Buch ordnet nach
 * Posten zu, diese Kurve nach Datum. Aus derselben Liste ließe sich beides
 * nicht ableiten.
 */
/**
 * Der Verlauf eines Kalendermonats.
 *
 * `onlyAvailable` betrachtet nur die Konten, die als verfügbar gelten. Nur so
 * gehen Balken und Saldolinie auf: eine Umbuchung aufs Tagesgeld verlässt den
 * Topf und senkt die Linie um genau den Balken, den sie erzeugt.
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
  // Das Standardkonto wechseln nimmt einem anderen die Markierung — deshalb
  // immer die ganze Liste neu laden, nicht nur den einen Eintrag.
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

// --- Haushaltsbuch --------------------------------------------------------

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
    [
      keys.transactions(year, month),
      keys.plan(year, month),
      keys.plans,
      // Präfix: deckt die Konten **und** ihren Verlauf ab.
      keys.accounts,
    ],
    'Buchung gespeichert'
  )
}

export function useDeleteTransaction(year: number, month: number) {
  return useInvalidating<void, string>(
    (id) => api.delete(`/transactions/${id}`),
    [
      keys.transactions(year, month),
      keys.plan(year, month),
      keys.plans,
      // Präfix: deckt die Konten **und** ihren Verlauf ab.
      keys.accounts,
    ],
    'Buchung gelöscht'
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

/**
 * Der ganze Plan eines Mitglieds, das Einblick gegeben hat.
 *
 * Nicht der gemeinsame Plan: der fasst alle zusammen und zeigt nur Posten mit
 * Haushalt. Hier steht eine Person für sich, samt privater Posten.
 */
export function useMemberPlan(
  ownerId: string | null,
  year: number,
  month: number
) {
  return useQuery({
    queryKey: keys.memberPlan(ownerId ?? '', year, month),
    queryFn: () =>
      api.get<MemberPlanDetail>(`/plans/member/${ownerId}/${year}/${month}`),
    enabled: ownerId !== null,
    retry: false,
  })
}

export function useCreatePlan() {
  return useInvalidating<PlanDetail, { year: number; month: number }>(
    (input) => api.post('/plans', input),
    [keys.plans],
    'Monat angelegt'
  )
}

export function useConfirmPlan() {
  return useInvalidating<PlanDetail, string>(
    (planId) => api.post(`/plans/${planId}/confirm`),
    [keys.plans],
    'Monat bestätigt'
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
 * Haken setzen oder wegnehmen.
 *
 * Beim Setzen dürfen Datum und Betrag der erzeugten Buchung mitkommen — die
 * Zahlung liegt oft ein paar Tage zurück. Ohne die beiden Felder bucht das
 * Backend heute und den geplanten Betrag.
 *
 * Räumt auch die Buchungen ab: der Haken erzeugt und löscht sie, das Buch
 * zeigt sonst weiter, was es vor dem Klick zeigte.
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
 * Mutation, die danach aufräumt.
 *
 * Fast jede Änderung berührt mehrere Ansichten — ein Posten ändert den Plan
 * und die Übersicht. Statt das an jeder Stelle einzeln zu schreiben, steht
 * hier einmal, welche Schlüssel danach ungültig sind.
 */
function useInvalidating<TData, TInput>(
  mutationFn: (input: TInput) => Promise<TData>,
  invalidate: readonly (readonly unknown[])[],
  /**
   * Was der Toast sagt. An **einer** Stelle statt an jedem Aufrufer — sonst
   * bekommt die Hälfte der Aktionen eine Rückmeldung und die andere nicht.
   *
   * Fehler bleiben draußen: die stehen im Formular neben dem Feld, das sie
   * betrifft. Ein Toast, der wegfliegt, ist der falsche Ort für etwas, das man
   * korrigieren soll.
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
