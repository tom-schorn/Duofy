import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, test, vi } from 'vitest'

import { PaidDialog } from '@/components/PaidDialog'
import type { PlanPosition } from '@/lib/domain'

const position = { id: 'x', label: 'Miete', amountPlanned: '500.00' } as PlanPosition

describe('PaidDialog', () => {
  test('starts on the amount and passes an edited amount on submit', async () => {
    const user = userEvent.setup()
    const onConfirm = vi.fn()
    render(<PaidDialog position={position} onClose={() => {}} onConfirm={onConfirm} pending={false} />)
    const amount = screen.getByLabelText('Betrag')
    expect(amount).toHaveFocus()
    await user.clear(amount)
    await user.type(amount, '450')
    await user.click(screen.getByRole('button', { name: 'Abhaken' }))
    expect(onConfirm).toHaveBeenCalledWith(expect.objectContaining({ amount: '450.00' }))
  })

  test('disables date and amount when the position already has bookings, focus stays inside', () => {
    render(<PaidDialog position={position} onClose={() => {}} onConfirm={() => {}} pending={false} hasBookings />)
    expect(screen.getByLabelText('Betrag')).toBeDisabled()
    expect(screen.getByRole('dialog')).toContainElement(document.activeElement as HTMLElement)
  })
})
