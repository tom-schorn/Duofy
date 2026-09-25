/**
 * Numbers for the UI, in the active language.
 *
 * Never `toFixed` plus a hand-made comma, never a hard-coded `de-DE`: the
 * decimal separator belongs to the language, not to the component. Amounts in
 * euro are `euro` in `domain.ts`, dates live in `dates.ts`.
 */

import { locale } from '@/lib/i18n'

/** A plain number — `12,5` in German, `12.5` in English. */
export function formatNumber(
  value: number,
  options?: Intl.NumberFormatOptions
): string {
  return new Intl.NumberFormat(locale(), options).format(value)
}

/**
 * A share with one decimal place, without the percent sign — `12,5`.
 *
 * No thousands separator: a share above 999 % is an error in the data, and
 * grouping it would only make it look deliberate.
 */
export function formatShare(value: number): string {
  return formatNumber(value, {
    minimumFractionDigits: 1,
    maximumFractionDigits: 1,
    useGrouping: false,
  })
}
