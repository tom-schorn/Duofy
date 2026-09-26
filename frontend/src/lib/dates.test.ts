import { describe, expect, it } from 'vitest'

import { firstOfNextMonth, parseMonth } from './dates'

describe('firstOfNextMonth', () => {
  it('is the 1st of the following month', () => {
    expect(firstOfNextMonth(new Date(2026, 8, 17))).toBe('2026-10-01')
  })

  it('rolls December over into January of the next year', () => {
    expect(firstOfNextMonth(new Date(2026, 11, 31))).toBe('2027-01-01')
  })

  it('handles the 31st in a month before a shorter one', () => {
    expect(firstOfNextMonth(new Date(2026, 0, 31))).toBe('2026-02-01')
  })

  it('uses the local date, not UTC, shortly after midnight', () => {
    expect(firstOfNextMonth(new Date(2026, 8, 30, 0, 30))).toBe('2026-10-01')
  })
})

describe('parseMonth', () => {
  it('reads a valid year and month from the address', () => {
    expect(parseMonth('2026', '11')).toEqual({ year: 2026, month: 11 })
    expect(parseMonth('2026', '09')).toEqual({ year: 2026, month: 9 })
  })

  it('refuses a month outside 1 to 12', () => {
    expect(parseMonth('2026', '13')).toBeNull()
    expect(parseMonth('2026', '0')).toBeNull()
  })

  it('refuses what is not a number, or a year that is not four digits', () => {
    expect(parseMonth('abc', 'x')).toBeNull()
    expect(parseMonth('26', '05')).toBeNull()
    expect(parseMonth(undefined, undefined)).toBeNull()
  })
})
