import { useLocation, useNavigate, useSearchParams } from 'react-router'

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { AREA_FIELD, AREA_ORDER, type Member } from '@/lib/domain'
import { useHouseholds, useMe } from '@/lib/queries'

/**
 * Whose figures the app is showing.
 *
 * Sits under the logo, and it is deliberately the only switch in the shell: it
 * changes what every page shows, and something that far-reaching has to be
 * visible from every page. The earlier design put the members in the sidebar as
 * places to visit — that was right while the point was to look at one plan, and
 * stopped being right once the point became working on somebody else's month,
 * contracts and book.
 *
 * The chosen person stands in the address as `?member=<uuid>`. A link therefore
 * still points at exactly what the sender was looking at, and reloading keeps it.
 * The uuid rather than the name: names repeat and change, an id does not.
 */

/** Anyone who granted at least `view` in some area — below that there is nothing to show. */
export function sharesAnything(member: Member): boolean {
  return AREA_ORDER.some((area) => member[AREA_FIELD[area]] !== 'plan')
}

/** The value the select uses for "myself" — an empty param would be ambiguous. */
const MYSELF = 'me'

export function MemberSwitcher() {
  const me = useMe().data
  const households = useHouseholds().data ?? []
  const [params] = useSearchParams()
  const location = useLocation()
  const navigate = useNavigate()

  // The same person can sit in several households; they belong in the list once.
  const shared = new Map<string, Member>()
  for (const household of households) {
    for (const member of household.members) {
      if (member.userId !== me?.id && sharesAnything(member)) {
        shared.set(member.userId, member)
      }
    }
  }

  // Nobody has granted anything — a switch with a single entry is furniture.
  if (shared.size === 0) return null

  const active = params.get('member') ?? MYSELF

  function handleSwitch(next: string) {
    const search = new URLSearchParams(params)
    if (next === MYSELF) search.delete('member')
    else search.set('member', next)
    // A household plan is a composed view of everybody, so a person on top of it
    // would contradict itself.
    search.delete('household')
    navigate({ pathname: location.pathname, search: search.toString() })
  }

  return (
    <Select value={active} onValueChange={handleSwitch}>
      <SelectTrigger
        className="h-8 w-full text-xs group-data-[collapsible=icon]:hidden"
        aria-label="Person, deren Zahlen angezeigt werden"
      >
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value={MYSELF}>{me?.firstName ?? 'Ich'}</SelectItem>
        {[...shared.values()].map((member) => (
          <SelectItem key={member.userId} value={member.userId}>
            {member.firstName}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}
