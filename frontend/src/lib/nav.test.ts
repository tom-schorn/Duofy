import { describe, expect, it } from 'vitest'

import { titleKeyFor } from './nav'

describe('titleKeyFor', () => {
  it('names the page for each sidebar entry', () => {
    expect(titleKeyFor('/contracts')).toBe('nav.commitments')
    expect(titleKeyFor('/accounts')).toBe('nav.accounts')
    expect(titleKeyFor('/household')).toBe('nav.household')
  })

  it('gives a month of the plan the title of the plan', () => {
    expect(titleKeyFor('/plan/2026/09')).toBe('nav.plan')
  })

  it('has no sidebar entry for the book: it is a tab of the plan (#241)', () => {
    expect(titleKeyFor('/book')).toBeNull()
  })

  it('has no title for an address the sidebar does not know', () => {
    expect(titleKeyFor('/nowhere')).toBeNull()
  })
})
