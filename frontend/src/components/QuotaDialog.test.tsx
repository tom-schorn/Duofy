import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, test, vi } from 'vitest'

import { QuotaDialog } from '@/components/QuotaDialog'
import { i18n } from '@/lib/i18n'

const INITIAL = {
  targetNeeds: '50.00',
  targetWants: '30.00',
  targetSavings: '20.00',
}

function open(onSave = vi.fn()) {
  render(
    <QuotaDialog
      open
      onOpenChange={() => undefined}
      title="Richtwerte des Haushalts"
      description="Beschreibung"
      initial={INITIAL}
      pending={false}
      error={null}
      onSave={onSave}
    />
  )
  return onSave
}

describe('quota dialog', () => {
  test('shows the stored values as sliders', () => {
    open()
    expect(screen.getByLabelText(i18n.t('quota.needs'))).toHaveValue('50')
    expect(screen.getByLabelText(i18n.t('quota.wants'))).toHaveValue('30')
    expect(screen.getByLabelText(i18n.t('quota.savings'))).toHaveValue('20')
  })

  test('moving a slider takes the difference from the others', async () => {
    const user = userEvent.setup()
    const onSave = open()
    const needs = screen.getByLabelText(i18n.t('quota.needs'))
    // A native range input; the arrow keys change its value in the browser and
    // arrive here as this change event (jsdom does not press keys on a slider).
    fireEvent.change(needs, { target: { value: '60' } })
    await user.click(screen.getByRole('button', { name: 'Speichern' }))

    // 60 needs; the remaining 40 split 30:20 → 24 / 16.
    expect(onSave).toHaveBeenCalledWith({
      targetNeeds: '60',
      targetWants: '24',
      targetSavings: '16',
    })
  })

  test('shows one decimal once a value is fractional, so all three still add up to 100', () => {
    open()
    const needs = screen.getByLabelText(i18n.t('quota.needs'))
    fireEvent.change(needs, { target: { value: '33' } })

    // 33 needs; the remaining 67 split 30:20 → 40,2 / 26,8.
    expect(screen.getByText('33,0 %')).toBeInTheDocument()
    expect(screen.getByText('40,2 %')).toBeInTheDocument()
    expect(screen.getByText('26,8 %')).toBeInTheDocument()
    expect(needs).toHaveAttribute('aria-valuetext', '33,0 Prozent')
  })
})
