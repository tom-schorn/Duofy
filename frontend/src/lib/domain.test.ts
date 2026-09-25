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
    budget: '2000.00',
    spent: { needs: '0.00', wants: '0.00', savings: '0.00' },
    unpaid: '0.00',
    householdIds: [],
    ...overrides,
  }
}

describe('unallocated', () => {
  it('is the whole budget when nothing is allocated yet', () => {
    expect(unallocated(plan())).toBe(2000)
  })

  it('subtracts what all three blocks have spent', () => {
    expect(
      unallocated(
        plan({ spent: { needs: '600.00', wants: '300.00', savings: '200.00' } })
      )
    ).toBe(900)
  })

  it('can go negative when the blocks overspend the budget', () => {
    expect(
      unallocated(
        plan({ budget: '1000.00', spent: { needs: '800.00', wants: '400.00', savings: '0.00' } })
      )
    ).toBe(-200)
  })
})

describe('QUOTA_KEY', () => {
  it('maps each block to the matching target field on the plan', () => {
    // What every call site relies on: plan[QUOTA_KEY[block]] reads the right
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
    block: 'needs',
    dueDay: 1,
    accountId: null,
    counterAccountId: null,
    paymentMethod: null,
    isBudget: false,
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
    expect(stillDue(position({ block: 'income', amountPlanned: '3000.00' }))).toBe(0)
  })

  it('is zero for a pass-through position, budget notwithstanding', () => {
    expect(stillDue(position({ passThrough: true, amountPlanned: '400.00' }))).toBe(0)
  })
})
