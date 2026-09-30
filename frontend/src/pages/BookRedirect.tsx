import { Navigate, useSearchParams } from 'react-router'

import { scopeSearch } from '@/hooks/use-active-member'
import { parseMonth } from '@/lib/dates'

/**
 * The book used to be a page of its own at `/book?month=2026-09`. It is a tab of
 * the plan page now (#241), so an old link or bookmark lands on that month's book
 * tab instead of an address that no longer exists. Whoever was being viewed
 * (`?member=` or `?household=`) stays.
 */
export function BookRedirect() {
  const [params] = useSearchParams()
  const raw = params.get('month') ?? ''
  const [rawYear, rawMonth] = raw.split('-')
  const parsed = /^\d{4}-\d{2}$/.test(raw) ? parseMonth(rawYear, rawMonth) : null
  const now = new Date()
  const year = parsed?.year ?? now.getFullYear()
  const month = parsed?.month ?? now.getMonth() + 1

  const target = new URLSearchParams(
    scopeSearch({ id: params.get('member'), householdId: params.get('household') })
  )
  target.set('tab', 'book')

  return (
    <Navigate
      to={`/plan/${year}/${String(month).padStart(2, '0')}?${target.toString()}`}
      replace
    />
  )
}
