import { describe, expect, it } from 'vitest'

import { helpKeyFor } from './help-state'

describe('helpKeyFor', () => {
  it('gives the book tab of a plan month the help of the book (#241)', () => {
    expect(helpKeyFor('/plan/2026/09', 'book')).toBe('book')
  })

  it('gives the other tabs of a plan month the help of the plan', () => {
    expect(helpKeyFor('/plan/2026/09', null)).toBe('plan')
    expect(helpKeyFor('/plan/2026/09', 'flow')).toBe('plan')
  })

  it('has no help of its own for the old book address, which only redirects', () => {
    expect(helpKeyFor('/book', null)).toBeNull()
  })
})
