import { useEffect, useState } from 'react'
import { useLocation, useSearchParams } from 'react-router'

import type { HelpKey } from '@/lib/help'

/**
 * Set once a person closes the column beside the page. Default: open. The old
 * `duofy.help.pinned` key (default was closed) is ignored on purpose, so a stored
 * "false" from before does not hide the help for everyone.
 */
const CLOSED_KEY = 'duofy.help.closed'

/** The width from which the help can stay open next to the page (Tailwind `xl`). */
const WIDE_QUERY = '(min-width: 1280px)'

/** Which set of entries belongs to the current route. */
export function helpKeyFor(pathname: string, tab: string | null): HelpKey | null {
  if (pathname.startsWith('/plan/')) return tab === 'book' ? 'book' : 'plan'
  if (pathname === '/book') return 'book'
  if (pathname === '/import') return 'import'
  if (pathname === '/plan') return 'plans'
  if (pathname === '/contracts') return 'commitments'
  if (pathname === '/accounts') return 'accounts'
  if (pathname === '/household') return 'household'
  if (pathname === '/admin') return 'admin'
  if (pathname === '/einstellungen') return 'settings'
  return null
}

/** The help set of the page you are on; null where a page has none. */
export function useHelpKey(): HelpKey | null {
  const { pathname } = useLocation()
  const [params] = useSearchParams()
  return helpKeyFor(pathname, params.get('tab'))
}

/**
 * Whether the help stays open beside the page (from 1280 px up): open until the
 * person closes it, and that choice is remembered.
 *
 * Storage can be refused (private window, blocked site data): reading then
 * answers "open", and a write that fails is logged, not thrown — the choice
 * simply lasts until the page is left.
 */
export function useHelpPinned(): { pinned: boolean; setPinned: (next: boolean) => void } {
  const [pinned, setPinnedState] = useState(() => {
    try {
      return localStorage.getItem(CLOSED_KEY) !== 'true'
    } catch (error) {
      console.warn('Could not read the help setting', error)
      return true
    }
  })

  function setPinned(next: boolean) {
    setPinnedState(next)
    try {
      localStorage.setItem(CLOSED_KEY, String(!next))
    } catch (error) {
      console.warn('Could not save the help setting', error)
    }
  }

  return { pinned, setPinned }
}

/** True from 1280 px up. Without `matchMedia` (tests) it answers false. */
export function useIsWide(): boolean {
  const [wide, setWide] = useState(() => window.matchMedia?.(WIDE_QUERY).matches ?? false)

  useEffect(() => {
    const query = window.matchMedia?.(WIDE_QUERY)
    if (!query) return
    const onChange = () => setWide(query.matches)
    query.addEventListener('change', onChange)
    return () => query.removeEventListener('change', onChange)
  }, [])

  return wide
}
