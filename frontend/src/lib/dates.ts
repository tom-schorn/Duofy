/**
 * Dates as ISO days, without the timezone trap.
 *
 * `new Date().toISOString().slice(0, 10)` looks harmless and is wrong east of
 * UTC: `toISOString` converts to UTC first. Booking something at 00:30 local
 * summer time yields the **previous day** — the booking lands a day early and
 * possibly in the wrong month.
 *
 * Use these helpers everywhere instead of `toISOString`.
 */

/** `2026-08-03` built from the **local** date parts. */
export function toIsoDay(date: Date): string {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

/** Today as an ISO day. */
export function today(): string {
  return toIsoDay(new Date())
}

/**
 * An ISO day to a `Date`, at **local** midnight.
 *
 * `new Date('2026-08-03')` parses the string as UTC, which east of UTC gives the
 * 3rd of August at 02:00. Reading the local parts back still works there, but
 * west of UTC it does not.
 */
export function fromIsoDay(iso: string): Date | undefined {
  const parts = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso)
  if (!parts) return undefined
  return new Date(Number(parts[1]), Number(parts[2]) - 1, Number(parts[3]))
}

const LONG_DATE = new Intl.DateTimeFormat('de-DE', {
  day: '2-digit',
  month: 'long',
  year: 'numeric',
})

/** `03. August 2026` — for buttons that show a chosen date. */
export function longDate(iso: string): string {
  const date = fromIsoDay(iso)
  return date ? LONG_DATE.format(date) : ''
}
