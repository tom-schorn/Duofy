import { useSearchParams } from 'react-router'

import {
  ACCESS_ORDER,
  AREA_FIELD,
  type AccessLevel,
  type Area,
  type Household,
  type Member,
} from '@/lib/domain'
import { useHouseholds } from '@/lib/queries'

/**
 * Who the app is currently showing, and what may be done with their data.
 *
 * The person comes from `?member=<uuid>`, the levels come from the member list
 * that `useHouseholds` already carries — no extra request for a question the
 * frontend can answer from what it has.
 *
 * `levelFor` answers `edit` when nobody is selected: your own data has no
 * restriction, exactly as `granted_level()` decides it in the backend. If both
 * share several households, the highest level wins, again as in the backend. The
 * check that counts still happens there; this one only keeps the UI from offering
 * buttons that would end in a 403.
 */
/** The highest level `userId` grants in one area over all households. */
export function highestLevel(
  households: Household[],
  userId: string,
  area: Area
): AccessLevel {
  const levels = households
    .flatMap((household) => household.members)
    .filter((candidate) => candidate.userId === userId)
    .map((candidate) => candidate[AREA_FIELD[area]])
  return levels.reduce<AccessLevel>(
    (best, level) => (ACCESS_ORDER.indexOf(level) > ACCESS_ORDER.indexOf(best) ? level : best),
    'plan'
  )
}

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
    levelFor: (area) => (id === null ? 'edit' : highestLevel(households, id, area)),
  }
}
