import { formatNumber } from '@/lib/format'

/**
 * Amounts the way people type them, and the way the API wants them.
 *
 * The API takes a decimal **string** (`"1234.56"`). Nothing here goes through
 * `Number` on the way in, so nothing is rounded on the way; only the display
 * (`formatAmount`) uses a number, and by then the value is known to have at most
 * two decimals and ten integer digits.
 */

export type AmountError = 'empty' | 'invalid' | 'tooManyDecimals' | 'tooSmall' | 'tooLarge'

export type ParsedAmount = { ok: true; value: string } | { ok: false; reason: AmountError }

/** The database column holds 12 digits, two of them decimals. */
const MAX_INTEGER_DIGITS = 10

/**
 * Read what was typed: `1.234,56`, `1234,56` and `1234.56` all mean the same.
 *
 * With a comma, the comma is the decimal separator and dots are thousands
 * separators. Without one, a single dot is a decimal point (`12.5`) unless it
 * clearly groups thousands (`1.234`: one to three digits, not starting with 0,
 * then exactly three). So `0.005` is refused as three decimals, never read as 5.
 * `12e3`, letters and signs are refused; the sign comes from the kind of booking.
 */
export function parseAmount(text: string): ParsedAmount {
  const typed = text.replace('€', '').trim()
  if (typed === '') return { ok: false, reason: 'empty' }
  if (!/^[0-9.,]+$/.test(typed)) return { ok: false, reason: 'invalid' }

  let integer: string
  let decimals = ''
  const commas = typed.split(',')

  if (commas.length > 2) return { ok: false, reason: 'invalid' }
  if (commas.length === 2) {
    integer = commas[0]
    decimals = commas[1]
    if (!isGrouped(integer) && !/^[0-9]+$/.test(integer)) return { ok: false, reason: 'invalid' }
    integer = integer.replaceAll('.', '')
  } else {
    const dots = typed.split('.')
    if (dots.length === 1) {
      integer = typed
    } else if (dots.length === 2 && !isGrouped(typed)) {
      // One dot that is not a thousands group: a decimal point.
      integer = dots[0]
      decimals = dots[1]
    } else if (isGrouped(typed)) {
      integer = typed.replaceAll('.', '')
    } else {
      return { ok: false, reason: 'invalid' }
    }
  }

  if (integer === '' && decimals === '') return { ok: false, reason: 'invalid' }
  if (integer === '') integer = '0'
  if (!/^[0-9]*$/.test(decimals)) return { ok: false, reason: 'invalid' }
  if (decimals.length > 2) return { ok: false, reason: 'tooManyDecimals' }

  integer = integer.replace(/^0+(?=\d)/, '')
  if (integer.length > MAX_INTEGER_DIGITS) return { ok: false, reason: 'tooLarge' }

  const value = `${integer}.${decimals.padEnd(2, '0')}`
  if (value === '0.00') return { ok: false, reason: 'tooSmall' }
  return { ok: true, value }
}

/** `1.234` or `12.345.678`: groups of three after a first group that does not start with 0. */
function isGrouped(text: string): boolean {
  return /^[1-9][0-9]{0,2}(\.[0-9]{3})+$/.test(text)
}

/** An API amount as shown in the field: `1234.5` becomes `1.234,50`. Empty stays empty. */
export function formatAmount(value: string): string {
  const parsed = parseAmount(value.replace('.', ','))
  if (!parsed.ok) return ''
  return formatNumber(Number(parsed.value), { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
