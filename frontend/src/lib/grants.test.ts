import { describe, expect, test } from 'vitest'

import type { AreaLevels } from '@/lib/domain'
import { GRANT_PRESETS, grantHints, presetOf } from '@/lib/grants'

const levels = (changes: Partial<AreaLevels> = {}): AreaLevels => ({
  ...GRANT_PRESETS.none,
  ...changes,
})

describe('presetOf', () => {
  test('recognises every preset from its own levels', () => {
    for (const [key, preset] of Object.entries(GRANT_PRESETS)) {
      expect(presetOf(preset)).toBe(key)
    }
  })

  test('says null for levels that match no preset, the own choice', () => {
    expect(presetOf({ ...GRANT_PRESETS.read, import: 'view' })).toBeNull()
  })
})

describe('presets', () => {
  test('partner gives everything, reading gives view without import, a child sees the plan', () => {
    expect(new Set(Object.values(GRANT_PRESETS.partner))).toEqual(new Set(['delete']))
    expect(GRANT_PRESETS.read).toEqual({
      plan: 'view',
      book: 'view',
      accounts: 'view',
      commitments: 'view',
      import: 'none',
    })
    expect(GRANT_PRESETS.kid).toEqual(levels({ plan: 'view' }))
  })
})

describe('grantHints', () => {
  test('no preset raises a hint', () => {
    for (const preset of Object.values(GRANT_PRESETS)) {
      expect(grantHints(preset)).toEqual([])
    }
  })

  test('booking from create on needs the accounts in view', () => {
    expect(grantHints(levels({ plan: 'view', book: 'create' }))).toEqual(['bookNeedsAccounts'])
    expect(grantHints(levels({ plan: 'view', book: 'view' }))).toEqual([])
  })

  test('importing from create on needs the accounts in view', () => {
    expect(grantHints(levels({ import: 'create' }))).toEqual(['importNeedsAccounts'])
    expect(grantHints(levels({ import: 'create', accounts: 'view' }))).toEqual([])
  })

  test('seeing the book needs the plan in view', () => {
    expect(grantHints(levels({ book: 'view' }))).toEqual(['bookNeedsPlan'])
  })

  test('several gaps give several hints, in a fixed order', () => {
    expect(grantHints(levels({ book: 'delete', import: 'edit' }))).toEqual([
      'bookNeedsAccounts',
      'importNeedsAccounts',
      'bookNeedsPlan',
    ])
  })
})
