import { render, screen } from '@testing-library/react'
import { describe, expect, test } from 'vitest'

import { SentenceWord } from '@/components/SentenceWord'

describe('SentenceWord', () => {
  test('is described by the sentence it is part of', () => {
    render(
      <>
        <p id="the-sentence">Fällig am 1.</p>
        <SentenceWord open={false} onClick={() => {}} describedBy="the-sentence">
          1.
        </SentenceWord>
      </>
    )
    expect(screen.getByRole('button', { name: '1.' })).toHaveAccessibleDescription('Fällig am 1.')
  })

  test('has no description when none is given', () => {
    render(
      <SentenceWord open={false} onClick={() => {}}>
        1.
      </SentenceWord>
    )
    expect(screen.getByRole('button', { name: '1.' })).not.toHaveAttribute('aria-describedby')
  })
})
