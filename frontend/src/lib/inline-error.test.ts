import { describe, expect, test } from 'vitest'

/**
 * Which hooks are marked INLINE_ERROR — the shared net stays quiet for them.
 *
 * A hook whose error is shown in a form but is missing here reports twice; a hook
 * on this list without an inline error reports nowhere. Changing the list is a
 * deliberate act: this test makes it show up in the diff.
 */
const INLINE_HOOKS = [
  'useAcceptInvitation',
  'useCreateHousehold',
  'useCreatePlan',
  'useDeclineInvitation',
  'useDeleteAccount',
  'useInvite',
  'useLeaveHousehold',
  'useSaveAccount',
  'useSaveCommitment',
  'useSavePosition',
  'useSaveTransaction',
  'useSetDefaultQuota',
  'useSetMyAccess',
  'useTogglePaid',
  'useUpdateHousehold',
  'useUploadStatement',
]

describe('INLINE_ERROR marks', () => {
  test('are exactly the hooks whose callers show the error themselves', () => {
    const source = Object.values(
      import.meta.glob<string>('./queries.ts', {
        query: '?raw',
        import: 'default',
        eager: true,
      })
    )[0]
    const marked = source
      .split(/^export function /m)
      .slice(1)
      .filter((block) => block.includes('INLINE_ERROR'))
      .map((block) => block.slice(0, block.indexOf('(')))
      .sort()
    expect(marked).toEqual(INLINE_HOOKS)
  })
})
