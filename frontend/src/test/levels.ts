import type { AreaLevels } from '@/lib/domain'

/** Every area at `none`, except what the test names. */
export function areaLevels(overrides: Partial<AreaLevels> = {}): AreaLevels {
  return {
    plan: 'none',
    book: 'none',
    accounts: 'none',
    commitments: 'none',
    import: 'none',
    ...overrides,
  }
}
