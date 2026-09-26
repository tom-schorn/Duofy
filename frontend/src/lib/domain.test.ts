/**
 * Pure functions in domain.ts: date arithmetic, the unallocated budget, and the
 * helpers around a position's quota and payment state.
 *
 * The shared date cases (`daysInMonth`, `effectiveDueDay`, checked against the
 * backend's own rule) live in date-cases.test.ts, not here — this file is what
 * does not need to agree with Python.
 */
import { describe, expect, it } from 'vitest'

import {
  daysInMonth,
  dueMonths,
  intervalLabel,
  isValidInterval,
  monthlyEquivalent,
  effectiveDueDay,
  isPaid,
  type PlanPosition,
  type PlanSummary,
  QUOTA_KEY,
  stillDue,
  unallocated,
} from './domain'

describe('daysInMonth', () => {
  it('counts a common february as 28 days', () => {
    expect(daysInMonth(2026, 2)).toBe(28)
  })

  it('counts a leap february as 29 days', () => {
    expect(daysInMonth(2028, 2)).toBe(29)
  })

  it('counts a 31-day month correctly', () => {
    expect(daysInMonth(2026, 1)).toBe(31)
  })

  it('counts a 30-day month correctly', () => {
    expect(daysInMonth(2026, 4)).toBe(30)
  })
})

describe('effectiveDueDay', () => {
  it('leaves a day that exists every month unchanged', () => {
    expect(effectiveDueDay(15, 2026, 2)).toBe(15)
  })

  it('moves the 31st to the 28th in a common february', () => {
    expect(effectiveDueDay(31, 2026, 2)).toBe(28)
  })

  it('moves the 31st to the 29th in a leap february', () => {
    expect(effectiveDueDay(31, 2028, 2)).toBe(29)
  })

  it('moves the 31st to the 30th in a 30-day month', () => {
    expect(effectiveDueDay(31, 2026, 4)).toBe(30)
  })

  it('leaves the 31st in a 31-day month', () => {
    expect(effectiveDueDay(31, 2026, 1)).toBe(31)
  })
})

function plan(overrides: Partial<PlanSummary> = {}): PlanSummary {
  return {
    year: 2026,
    month: 9,
    targetNeeds: '50.00',
    targetWants: '30.00',
    targetSavings: '20.00',
    bufferPercent: '0.00',
    income: '2000.00',
    distributable: '2000.00',
    spent: { needs: '0.00', wants: '0.00', savings: '0.00' },
    unpaid: '0.00',
    householdIds: [],
    ...overrides,
  }
}

describe('unallocated', () => {
  it('is the whole distributable amount when nothing is allocated yet', () => {
    expect(unallocated(plan())).toBe(2000)
  })

  it('subtracts what all three budgets have spent', () => {
    expect(
      unallocated(
        plan({ spent: { needs: '600.00', wants: '300.00', savings: '200.00' } })
      )
    ).toBe(900)
  })

  it('can go negative when the budgets overspend the distributable amount', () => {
    expect(
      unallocated(
        plan({ distributable: '1000.00', spent: { needs: '800.00', wants: '400.00', savings: '0.00' } })
      )
    ).toBe(-200)
  })
})

describe('QUOTA_KEY', () => {
  it('maps each budget to the matching target field on the plan', () => {
    // What every call site relies on: plan[QUOTA_KEY[budget]] reads the right
    // percentage. A typo here would silently read the wrong quota.
    const summary = plan({ targetNeeds: '55.00', targetWants: '25.00', targetSavings: '20.00' })
    expect(summary[QUOTA_KEY.needs]).toBe('55.00')
    expect(summary[QUOTA_KEY.wants]).toBe('25.00')
    expect(summary[QUOTA_KEY.savings]).toBe('20.00')
  })
})

function position(overrides: Partial<PlanPosition> = {}): PlanPosition {
  return {
    id: 'p1',
    label: 'Miete',
    amountPlanned: '600.00',
    amountActual: null,
    category: 'housing.rent',
    budget: 'needs',
    dueDay: 1,
    accountId: null,
    counterAccountId: null,
    paymentMethod: null,
    isLimit: false,
    passThrough: false,
    householdId: null,
    commitmentId: null,
    paidAt: null,
    ...overrides,
  }
}

describe('isPaid', () => {
  it('is false without a paid-at timestamp', () => {
    expect(isPaid(position())).toBe(false)
  })

  it('is true once paidAt is set', () => {
    expect(isPaid(position({ paidAt: '2026-09-05T12:00:00Z' }))).toBe(true)
  })
})

describe('stillDue', () => {
  it('is the full planned amount when nothing is booked yet', () => {
    expect(stillDue(position())).toBe(600)
  })

  it('is the remainder once part of it is booked', () => {
    expect(stillDue(position({ amountActual: '127.50' }))).toBe(472.5)
  })

  it('never goes below zero when the booked amount overshoots', () => {
    expect(stillDue(position({ amountActual: '650.00' }))).toBe(0)
  })

  it('is zero once the position is paid, whatever is booked', () => {
    expect(stillDue(position({ paidAt: '2026-09-05T12:00:00Z', amountActual: '0.00' }))).toBe(0)
  })

  it('is zero for income — income never leaves the account', () => {
    expect(stillDue(position({ budget: 'income', amountPlanned: '3000.00' }))).toBe(0)
  })

  it('is zero for a pass-through position, planned amount notwithstanding', () => {
    expect(stillDue(position({ passThrough: true, amountPlanned: '400.00' }))).toBe(0)
  })

  it('is zero for a limit position, whatever remains unbooked', () => {
    expect(
      stillDue(position({ isLimit: true, amountPlanned: '600.00', amountActual: '127.50' }))
    ).toBe(0)
  })
})

describe('intervalLabel', () => {
  it('says monatlich for 1', () => {
    expect(intervalLabel(1)).toBe('monatlich')
  })

  it('says vierteljährlich for 3', () => {
    expect(intervalLabel(3)).toBe('vierteljährlich')
  })

  it.each([2, 5, 6, 12, 120])('says alle N Monate for %i', (months) => {
    expect(intervalLabel(months)).toBe(`alle ${months} Monate`)
  })
})

describe('isValidInterval', () => {
  it.each([1, 3, 120])('accepts %i', (months) => {
    expect(isValidInterval(months)).toBe(true)
  })

  it.each([0, -1, 121, 2.5, Number.NaN])('rejects %s', (months) => {
    expect(isValidInterval(months)).toBe(false)
  })
})

describe('dueMonths', () => {
  it('is empty for a monthly commitment', () => {
    expect(dueMonths(1, null, 2026)).toEqual([])
  })

  it('runs a quarterly cadence across the turn of the year', () => {
    expect(dueMonths(3, '2026-07-01', 2027)).toEqual([1, 4, 7, 10])
  })

  it('does not list months before the start', () => {
    expect(dueMonths(3, '2026-07-01', 2026)).toEqual([7, 10])
  })

  it('keeps an interval that does not divide 12 running', () => {
    expect(dueMonths(5, '2026-11-01', 2027)).toEqual([4, 9])
  })
})

describe('monthlyEquivalent', () => {
  it('divides by the number of months', () => {
    expect(monthlyEquivalent('108.00', 12)).toBe(9)
    expect(monthlyEquivalent('50.00', 1)).toBe(50)
  })
})
