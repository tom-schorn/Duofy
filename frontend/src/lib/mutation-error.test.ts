import { beforeEach, describe, expect, test, vi } from 'vitest'

const error = vi.fn()
vi.mock('sonner', () => ({ toast: { error: (...args: unknown[]) => error(...args) } }))

import { ApiError } from '@/lib/api'
import { reportMutationError } from '@/lib/mutation-error'

describe('reportMutationError', () => {
  beforeEach(() => error.mockClear())

  test('shows the translated error and stays until dismissed', () => {
    reportMutationError(new ApiError('not_allowed', 403), () => {})
    const [text, options] = error.mock.calls[0]
    expect(text).toBe('Dazu fehlt dir die Berechtigung.')
    expect(options.duration).toBe(Infinity)
  })

  test('offers to try again and runs the action again on click', () => {
    const retry = vi.fn()
    reportMutationError(new ApiError('not_allowed', 403), retry)
    const { action } = error.mock.calls[0][1]
    expect(action.label).toBe('Erneut versuchen')
    action.onClick()
    expect(retry).toHaveBeenCalledOnce()
  })

  test('says something even for an error without a known code', () => {
    reportMutationError(new Error('boom'), () => {})
    expect(error.mock.calls[0][0]).not.toBe('')
  })
})
