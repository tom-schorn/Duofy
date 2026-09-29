import { describe, expect, it } from 'vitest'

import type { Household, Member } from '@/lib/domain'
import { areaLevels } from '@/test/levels'

import { grantedToMe } from './use-active-member'

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
