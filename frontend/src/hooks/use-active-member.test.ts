import { describe, expect, it } from 'vitest'

import type { Household, Member } from '@/lib/domain'

import { highestLevel } from './use-active-member'

function member(overrides: Partial<Member>): Member {
  return {
    userId: 'alex',
    firstName: 'Alex',
    lastName: 'Muster',
    email: 'alex@example.org',
    role: 'member',
    grantsPlan: 'plan',
    grantsCommitments: 'plan',
    grantsAccounts: 'plan',
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
    bufferPercent: '0',
    members,
  }
}

describe('highestLevel', () => {
  it('takes the higher grant when two households differ, like the backend', () => {
    const households = [
      household('flat', [member({ grantsCommitments: 'view' })]),
      household('club', [member({ grantsCommitments: 'edit' })]),
    ]

    expect(highestLevel(households, 'alex', 'commitments')).toBe('edit')
  })

  it('keeps the areas apart', () => {
    const households = [household('flat', [member({ grantsPlan: 'edit' })])]

    expect(highestLevel(households, 'alex', 'plan')).toBe('edit')
    expect(highestLevel(households, 'alex', 'accounts')).toBe('plan')
  })

  it('answers plan for someone who shares no household', () => {
    expect(highestLevel([household('flat', [member({})])], 'stranger', 'plan')).toBe('plan')
  })
})
