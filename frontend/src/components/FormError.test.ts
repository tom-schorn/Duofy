import { createElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, test } from 'vitest'

import { ApiError } from '@/lib/api'
import { FormError } from '@/components/FormError'

describe('FormError', () => {
  test('renders nothing without an error', () => {
    expect(renderToStaticMarkup(createElement(FormError, { error: null }))).toBe('')
  })

  test('announces the translated error as an alert', () => {
    const html = renderToStaticMarkup(
      createElement(FormError, { error: new ApiError('not_allowed', 403) })
    )
    expect(html).toContain('role="alert"')
    expect(html).toContain('Dazu fehlt dir die Berechtigung.')
  })
})
