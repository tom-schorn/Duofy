import { useSearchParams } from 'react-router'

import { type AccessLevel, type Area, type Household, type Member } from '@/lib/domain'
import { useHouseholds } from '@/lib/queries'

/** What `userId` grants me in one area; `none` if they are not in my household. */
export function grantedToMe(households: Household[], userId: string, area: Area): AccessLevel {
  const member = households
    .flatMap((household) => household.members)
    .find((candidate) => candidate.userId === userId)
  return member?.grantsToMe[area] ?? 'none'
}

/**
 * Who the app is currently showing, and what may be done with their data.
 *
 * The person comes from `?member=<uuid>`, the levels come from the member list
 * that `useHouseholds` already carries — each member says what they grant me.
 * No extra request for a question the frontend can answer from what it has.
 *
 * `levelFor` answers `delete` when nobody is selected: your own data has no
 * restriction, exactly as `granted_level()` decides it in the backend. The check
 * that counts still happens there; this one only keeps the UI from offering
 * buttons that would end in a 403.
 */
export function useActiveMember(): {
  id: string | null
  member: Member | null
  levelFor: (area: Area) => AccessLevel
} {
  const [params] = useSearchParams()
  const households = useHouseholds().data ?? []
  const id = params.get('member')

  const member =
    id === null
      ? null
      : (households
          .flatMap((household) => household.members)
          .find((candidate) => candidate.userId === id) ?? null)

  return {
    id,
    member,
    levelFor: (area) => (id === null ? 'delete' : grantedToMe(households, id, area)),
  }
}
