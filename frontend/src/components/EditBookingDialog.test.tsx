import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, test, vi } from 'vitest'

import { EditBookingDialog } from '@/components/EditBookingDialog'
import type { Account, PlanPosition, Transaction } from '@/lib/domain'

const booking = {
  id: 't1',
  accountId: 'a1',
  counterAccountId: null,
  occurredOn: '2026-09-05',
  amount: '50.00',
  note: 'Streaming',
  category: 'leisure.subscriptions',
  budget: 'wants',
  positionId: null,
  autoBooked: false,
  externalRef: null,
} as Transaction

const accounts = [{ id: 'a1', name: 'Giro', active: true }] as Account[]
const positions = [{ id: 'p1', label: 'Miete' }] as PlanPosition[]

function renderDialog(transaction: Transaction, onSave = vi.fn()) {
  render(
    <EditBookingDialog
      transaction={transaction}
      accounts={accounts}
      positions={positions}
      open
      onOpenChange={() => {}}
      onSave={onSave}
      pending={false}
      error={null}
    />
  )
  return onSave
}

describe('EditBookingDialog', () => {
  test('saving sends the changed amount for this booking', () => {
    const onSave = renderDialog(booking)
    fireEvent.change(screen.getByLabelText('Betrag'), { target: { value: '80.00' } })
    fireEvent.click(screen.getByRole('button', { name: 'Speichern' }))
    expect(onSave).toHaveBeenCalledWith(
      expect.objectContaining({ id: 't1', amount: '80.00', accountId: 'a1' })
    )
  })

  test('a booking made by ticking off cannot be moved off its position', () => {
    renderDialog({ ...booking, autoBooked: true, positionId: 'p1' })
    expect(screen.getByRole('combobox', { name: 'Posten' })).toBeDisabled()
  })
})
