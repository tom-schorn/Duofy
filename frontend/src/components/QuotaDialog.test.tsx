import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, test, vi } from 'vitest'

import { QuotaDialog } from '@/components/QuotaDialog'
import { i18n } from '@/lib/i18n'

const INITIAL = {
  targetNeeds: '50.00',
  targetWants: '30.00',
  targetSavings: '20.00',
  bufferPercent: '0.00',
}

function open(onSave = vi.fn()) {
  render(
    <QuotaDialog
      open
      onOpenChange={() => undefined}
      title="Meine Richtwerte"
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
  test('shows the stored values without trailing zeros and the sum', () => {
    open()
    expect(screen.getByLabelText('Grundbedarf in %')).toHaveValue('50')
    expect(screen.getByText('Zusammen: 100 %')).toBeInTheDocument()
  })

  test('refuses to save while the three quotas do not add up to 100', async () => {
    const user = userEvent.setup()
    const onSave = open()
    const needs = screen.getByLabelText('Grundbedarf in %')
    await user.clear(needs)
    await user.type(needs, '60')

    expect(screen.getByRole('alert')).toHaveTextContent('zusammen 100 %')
    expect(screen.getByRole('button', { name: 'Speichern' })).toBeDisabled()
    expect(onSave).not.toHaveBeenCalled()
  })

  test('saves 65/20/15 with the buffer', async () => {
    const user = userEvent.setup()
    const onSave = open()
    for (const [label, value] of [
      ['Grundbedarf in %', '65'],
      [i18n.t('quota.wants'), '20'],
      ['Sparen in %', '15'],
      ['Puffer in %', '5,5'],
    ]) {
      const field = screen.getByLabelText(label)
      await user.clear(field)
      await user.type(field, value)
    }
    await user.click(screen.getByRole('button', { name: 'Speichern' }))

    expect(onSave).toHaveBeenCalledWith({
      targetNeeds: '65',
      targetWants: '20',
      targetSavings: '15',
      bufferPercent: '5.5',
    })
  })

  test('adds 33,33 + 33,33 + 33,34 to exactly 100 without floating point noise', async () => {
    const user = userEvent.setup()
    open()
    for (const [label, value] of [
      ['Grundbedarf in %', '33,33'],
      [i18n.t('quota.wants'), '33,33'],
      [i18n.t('quota.savings'), '33,34'],
    ]) {
      const field = screen.getByLabelText(label)
      await user.clear(field)
      await user.type(field, value)
    }
    expect(screen.getByText('Zusammen: 100 %')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Speichern' })).toBeEnabled()
  })

  test.each(['1e2', '0x10', '50,555'])('does not accept %s as a percent', async (text) => {
    const user = userEvent.setup()
    open()
    const needs = screen.getByLabelText('Grundbedarf in %')
    await user.clear(needs)
    await user.type(needs, text)
    expect(screen.getByRole('button', { name: 'Speichern' })).toBeDisabled()
  })
})
