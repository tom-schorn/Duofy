import { beforeEach, describe, expect, test, vi } from 'vitest'

const error = vi.fn()
vi.mock('sonner', () => ({ toast: { error: (...args: unknown[]) => error(...args) } }))

import { ApiError } from '@/lib/api'
import { reportMutationError, showsErrorInline } from '@/lib/mutation-error'

describe('reportMutationError', () => {
  beforeEach(() => error.mockClear())

  test('shows the translated error and stays until dismissed', () => {
    reportMutationError(new ApiError('not_allowed', 403), () => {})
    const [text, options] = error.mock.calls[0]
    expect(text).toBe('Dazu fehlt dir die Berechtigung.')
    expect(options.duration).toBe(Infinity)
    expect(options.cancel.label).toBe('Meldung schließen')
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

describe('showsErrorInline', () => {
  test('is false by default, so the net reports', () => {
    expect(showsErrorInline(undefined, { id: 'a' })).toBe(false)
    expect(showsErrorInline({}, undefined)).toBe(false)
  })

  test('is true when the hook is marked', () => {
    expect(showsErrorInline({ inlineError: true }, { id: 'a' })).toBe(true)
  })

  test('is true when a single call says its form shows the error', () => {
    expect(showsErrorInline(undefined, { id: 'a', inlineError: true })).toBe(true)
  })

  test('ignores a call flag that is not exactly true', () => {
    expect(showsErrorInline(undefined, { inlineError: 'yes' })).toBe(false)
  })
})
