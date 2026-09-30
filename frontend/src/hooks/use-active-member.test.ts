import { describe, expect, it } from 'vitest'

import type { Household, Member } from '@/lib/domain'
import { areaLevels } from '@/test/levels'

import { grantedToMe, scopeSearch } from './use-active-member'

function member(overrides: Partial<Member>): Member {
  return {
    userId: 'alex',
    firstName: 'Alex',
    lastName: 'Muster',
    email: 'alex@example.org',
    role: 'member',
    grantsToMe: areaLevels(),
    myGrants: areaLevels(),
    ...overrides,
  }
}

function household(id: string, members: Member[]): Household {
  return {
    id,
    name: id,
    targetNeeds: '50',
    targetWants: '30',
    targetSavings: '20',
    members,
  }
}

describe('grantedToMe', () => {
  it('reads what the member grants me, area by area', () => {
    const households = [household('flat', [member({ grantsToMe: areaLevels({ plan: 'edit' }) })])]

    expect(grantedToMe(households, 'alex', 'plan')).toBe('edit')
    expect(grantedToMe(households, 'alex', 'accounts')).toBe('none')
  })

  it('ignores what I grant the member', () => {
    const households = [household('flat', [member({ myGrants: areaLevels({ book: 'delete' }) })])]

    expect(grantedToMe(households, 'alex', 'book')).toBe('none')
  })

  it('answers none for someone who shares no household', () => {
    expect(grantedToMe([household('flat', [member({})])], 'stranger', 'plan')).toBe('none')
  })
})

describe('scopeSearch', () => {
  it('is empty for your own data', () => {
    expect(scopeSearch({ id: null, householdId: null })).toBe('')
  })

  it('carries the chosen person', () => {
    expect(scopeSearch({ id: 'u2', householdId: null })).toBe('?member=u2')
  })

  it('carries the household before a person', () => {
    expect(scopeSearch({ id: null, householdId: 'h1' })).toBe('?household=h1')
    expect(scopeSearch({ id: 'u2', householdId: 'h1' })).toBe('?household=h1')
  })
})
