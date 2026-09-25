/**
 * The date cases shared with the backend (`testdata/date_cases.json`).
 *
 * The same rule lives in Python (`Commitment.effective_due_day`) and here
 * (`effectiveDueDay`). If the two drift apart, a position shows one due day in the
 * plan and another in the backend — silently. Both suites read the same table, so
 * a disagreement fails one of them. Same idea as `test_enums_match_frontend.py`.
 */
import { describe, expect, it } from 'vitest'

import dateCases from '../../../testdata/date_cases.json'
import { daysInMonth, effectiveDueDay } from './domain'

type DateCases = {
  days_in_month: { case: string; year: number; month: number; expected: number }[]
  effective_due_day: {
    case: string
    due_day: number
    year: number
    month: number
    expected: number
  }[]
}

// A plain import, not `readFileSync`: `node:fs` would need Node types added to
// tsconfig.app.json, which is out of scope here. Vite/Vitest resolve a JSON
// import natively, in the browser build and under test alike.
const cases = dateCases as DateCases

describe('days in month, as the backend counts them', () => {
  it.each(cases.days_in_month)('$case', ({ year, month, expected }) => {
    expect(daysInMonth(year, month)).toBe(expected)
  })
})

describe('effective due day, as the backend clamps it', () => {
  it.each(cases.effective_due_day)('$case', ({ due_day, year, month, expected }) => {
    expect(effectiveDueDay(due_day, year, month)).toBe(expected)
  })
})
