import { BookOpen, CalendarRange, FileText, Upload, Users, Wallet } from 'lucide-react'

/**
 * One page for every commitment — savings plans and loans are commitments too.
 *
 * This used to be three entries split by `Commitment.type`. That was the wrong
 * axis: a commitment can sit in **any** budget (rent → needs, streaming → wants,
 * a savings plan → savings). `type` only says whether the thing has an end — a
 * property, not a navigation point.
 */
export const NAV = [
  { to: '/plan', label: 'nav.plan', icon: CalendarRange },
  { to: '/contracts', label: 'nav.commitments', icon: FileText },
  { to: '/book', label: 'nav.book', icon: BookOpen },
  { to: '/accounts', label: 'nav.accounts', icon: Wallet },
  { to: '/import', label: 'nav.import', icon: Upload },
  { to: '/household', label: 'nav.household', icon: Users },
]

/** The catalog key of the page title for an address — the header shows it. */
export function titleKeyFor(pathname: string): string | null {
  const entry = NAV.find(
    (item) => pathname === item.to || pathname.startsWith(`${item.to}/`)
  )
  return entry?.label ?? null
}
