import { render, screen } from '@testing-library/react'
import { describe, expect, test } from 'vitest'

import { fillSentence } from '@/lib/sentence'

describe('fillSentence', () => {
  test('fills a token in the middle of the text', () => {
    render(<div>{fillSentence('Geht {rhythm} ab.', { rhythm: <b>monatlich</b> })}</div>)
    expect(screen.getByText('monatlich').tagName).toBe('B')
    expect(screen.getByText(/Geht/)).toBeInTheDocument()
    expect(screen.getByText(/ab\./)).toBeInTheDocument()
  })

  test('fills a token at the very start and the very end', () => {
    render(<div>{fillSentence('{a} und {b}', { a: <b>A</b>, b: <i>B</i> })}</div>)
    expect(screen.getByText('A').tagName).toBe('B')
    expect(screen.getByText('B').tagName).toBe('I')
  })

  test('leaves a token with no matching word as literal text', () => {
    render(<div>{fillSentence('Geht {rhythm} ab.', {})}</div>)
    expect(screen.getByText('Geht {rhythm} ab.')).toBeInTheDocument()
  })

  test('a template with no tokens renders unchanged', () => {
    render(<div>{fillSentence('Nichts zu tun.', {})}</div>)
    expect(screen.getByText('Nichts zu tun.')).toBeInTheDocument()
  })
})
