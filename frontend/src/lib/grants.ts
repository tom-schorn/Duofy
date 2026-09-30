import { atLeast, type AreaLevels } from '@/lib/domain'

/**
 * Ready-made sets of levels for the grants page (decision 63).
 *
 * A preset is only a shortcut: choosing one writes its five levels, and changing
 * any area afterwards leaves it — the page then says "eigene Auswahl".
 */
export const GRANT_PRESETS = {
  partner: { plan: 'delete', book: 'delete', accounts: 'delete', commitments: 'delete', import: 'delete' },
  read: { plan: 'view', book: 'view', accounts: 'view', commitments: 'view', import: 'none' },
  kid: { plan: 'view', book: 'none', accounts: 'none', commitments: 'none', import: 'none' },
  none: { plan: 'none', book: 'none', accounts: 'none', commitments: 'none', import: 'none' },
} satisfies Record<string, AreaLevels>

export type GrantPreset = keyof typeof GRANT_PRESETS

export const PRESET_ORDER: GrantPreset[] = ['partner', 'read', 'kid', 'none']

/** The preset these levels are exactly, or null for an own choice. */
export function presetOf(levels: AreaLevels): GrantPreset | null {
  return (
    PRESET_ORDER.find((key) =>
      (Object.keys(levels) as (keyof AreaLevels)[]).every(
        (area) => GRANT_PRESETS[key][area] === levels[area]
      )
    ) ?? null
  )
}

export type GrantHint = 'bookNeedsAccounts' | 'importNeedsAccounts' | 'bookNeedsPlan'

/**
 * Where one right is of little use without another (decision 62).
 *
 * Only a hint, never a change: the owner may have reasons, and a page that
 * quietly raises a second level gives away more than was chosen. The catalog key
 * is `grants.hints.<code>`.
 */
export function grantHints(levels: AreaLevels): GrantHint[] {
  const noAccounts = !atLeast(levels.accounts, 'view')
  const hints: GrantHint[] = []
  if (atLeast(levels.book, 'create') && noAccounts) hints.push('bookNeedsAccounts')
  if (atLeast(levels.import, 'create') && noAccounts) hints.push('importNeedsAccounts')
  if (atLeast(levels.book, 'view') && !atLeast(levels.plan, 'view')) hints.push('bookNeedsPlan')
  return hints
}
