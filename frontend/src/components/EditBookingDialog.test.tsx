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

function renderDialog(
  transaction: Transaction,
  extra: { pending?: boolean; onOpenChange?: (open: boolean) => void } = {}
) {
  const onSave = vi.fn()
  render(
    <EditBookingDialog
      transaction={transaction}
      accounts={accounts}
      positions={positions}
      open
      onOpenChange={extra.onOpenChange ?? (() => {})}
      onSave={onSave}
      pending={extra.pending ?? false}
      error={null}
    />
  )
  return onSave
}

describe('EditBookingDialog', () => {
  test('saving sends only what was changed', () => {
    const onSave = renderDialog(booking)
    fireEvent.change(screen.getByLabelText('Betrag'), { target: { value: '80.00' } })
    fireEvent.click(screen.getByRole('button', { name: 'Speichern' }))
    expect(onSave).toHaveBeenCalledWith({ id: 't1', amount: '80.00' })
  })

  test('a pure transfer keeps its stored purpose when only the amount changes', () => {
    const transfer = { ...booking, counterAccountId: 'a2', category: null, budget: null }
    const onSave = renderDialog(transfer)
    fireEvent.change(screen.getByLabelText('Betrag'), { target: { value: '12.00' } })
    fireEvent.click(screen.getByRole('button', { name: 'Speichern' }))
    expect(onSave).toHaveBeenCalledWith({ id: 't1', amount: '12.00' })
  })

  test('a booking made by ticking off keeps its position out of the request', () => {
    const onSave = renderDialog({ ...booking, autoBooked: true, positionId: 'p1' })
    fireEvent.change(screen.getByLabelText('Betrag'), { target: { value: '60.00' } })
    fireEvent.click(screen.getByRole('button', { name: 'Speichern' }))
    expect(onSave).toHaveBeenCalledWith({ id: 't1', amount: '60.00' })
  })

  test('a booking made by ticking off cannot be moved off its position', () => {
    renderDialog({ ...booking, autoBooked: true, positionId: 'p1' })
    expect(screen.getByRole('combobox', { name: 'Posten' })).toBeDisabled()
  })

  test('stays open and locked until the server has answered', () => {
    const onOpenChange = vi.fn()
    renderDialog(booking, { pending: true, onOpenChange })
    expect(screen.getByRole('button', { name: 'Abbrechen' })).toBeDisabled()
    fireEvent.keyDown(screen.getByRole('dialog'), { key: 'Escape' })
    expect(onOpenChange).not.toHaveBeenCalled()
  })
})
