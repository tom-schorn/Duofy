import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, test, vi } from 'vitest'

import { BookingDialog } from '@/components/BookingDialog'
import type { PositionMonth } from '@/components/PositionPicker'
import type { Account, PlanPosition, Transaction } from '@/lib/domain'
import { today } from '@/lib/dates'
import { i18n } from '@/lib/i18n'

/**
 * One dialog for every booking (#254): a new one, a changed one and the one a
 * tick creates. The new start is covered through its button in
 * `AddBookingButton.test.tsx`, where it saves for itself.
 */

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
  planYear: 2026,
  planMonth: 9,
  autoBooked: false,
  externalRef: null,
} as Transaction

const accounts = [{ id: 'a1', name: 'Giro', active: true }] as Account[]
const rent = { id: 'p1', label: 'Miete', amountPlanned: '500.00', accountId: null } as PlanPosition
const september: PositionMonth[] = [{ year: 2026, month: 9, positions: [rent] }]

function renderEdit(
  transaction: Transaction,
  extra: {
    pending?: boolean
    onClose?: () => void
    positions?: PositionMonth[]
  } = {}
) {
  const onSave = vi.fn()
  render(
    <BookingDialog
      accounts={accounts}
      positions={extra.positions ?? september}
      viewedMonth={{ year: 2026, month: 9 }}
      onClose={extra.onClose ?? (() => {})}
      start={{
        kind: 'edit',
        transaction,
        onSave,
        pending: extra.pending ?? false,
        error: null,
      }}
    />
  )
  return onSave
}

function renderTick(
  extra: {
    onConfirm?: (values: { occurredOn: string; amount: string }) => void
    hasBookings?: boolean
    bookingsUnknown?: boolean
    viewedMonth?: { year: number; month: number }
    position?: PlanPosition
  } = {}
) {
  const position = extra.position ?? rent
  const viewedMonth = extra.viewedMonth ?? { year: 2026, month: 9 }
  render(
    <BookingDialog
      accounts={accounts}
      positions={[{ ...viewedMonth, positions: [position] }]}
      viewedMonth={viewedMonth}
      onClose={() => {}}
      start={{
        kind: 'tick',
        position,
        onConfirm: extra.onConfirm ?? (() => {}),
        pending: false,
        error: null,
        hasBookings: extra.hasBookings ?? false,
        bookingsUnknown: extra.bookingsUnknown ?? false,
      }}
    />
  )
}

describe('BookingDialog edit', () => {
  test('saving sends only what was changed', () => {
    const onSave = renderEdit(booking)
    fireEvent.change(screen.getByLabelText('Betrag'), { target: { value: '80.00' } })
    fireEvent.click(screen.getByRole('button', { name: 'Speichern' }))
    expect(onSave).toHaveBeenCalledWith({ id: 't1', amount: '80.00' })
  })

  test('a pure transfer keeps its stored purpose when only the amount changes', () => {
    const transfer = { ...booking, counterAccountId: 'a2', category: null, budget: null }
    const onSave = renderEdit(transfer)
    fireEvent.change(screen.getByLabelText('Betrag'), { target: { value: '12.00' } })
    fireEvent.click(screen.getByRole('button', { name: 'Speichern' }))
    expect(onSave).toHaveBeenCalledWith({ id: 't1', amount: '12.00' })
  })

  test('a booking made by ticking off keeps its position out of the request', () => {
    const onSave = renderEdit({ ...booking, autoBooked: true, positionId: 'p1' })
    fireEvent.change(screen.getByLabelText('Betrag'), { target: { value: '60.00' } })
    fireEvent.click(screen.getByRole('button', { name: 'Speichern' }))
    expect(onSave).toHaveBeenCalledWith({ id: 't1', amount: '60.00' })
  })

  test('a booking made by ticking off cannot be moved off its position', () => {
    renderEdit({ ...booking, autoBooked: true, positionId: 'p1' })
    expect(screen.getByText('Miete')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Miete' })).not.toBeInTheDocument()
  })

  test('stays open and locked until the server has answered', () => {
    const onClose = vi.fn()
    renderEdit(booking, { pending: true, onClose })
    expect(screen.getByRole('button', { name: 'Abbrechen' })).toBeDisabled()
    fireEvent.keyDown(screen.getByRole('dialog'), { key: 'Escape' })
    expect(onClose).not.toHaveBeenCalled()
  })

  test('offers delete only when the caller passes it', () => {
    renderEdit(booking)
    expect(screen.queryByRole('button', { name: i18n.t('common.delete') })).not.toBeInTheDocument()
  })
})

describe('BookingDialog edit sentence', () => {
  test('names date, account and position in its sentence, each a clickable word', () => {
    renderEdit(booking)
    expect(screen.getByRole('button', { name: 'Giro' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: i18n.t('monthBook.noPosition') })).toBeInTheDocument()
  })

  test('picking a position closes its panel and gives the focus back to the word', async () => {
    const user = userEvent.setup()
    renderEdit(booking)
    const positionWord = screen.getByRole('button', { name: i18n.t('monthBook.noPosition') })
    await user.click(positionWord)
    const panel = screen.getByRole('group', { name: i18n.t('monthBook.positionLabel') })
    await user.click(within(panel).getByRole('combobox'))
    await user.click(await screen.findByRole('option', { name: 'Miete' }))
    await waitFor(() => expect(screen.getByRole('button', { name: 'Miete' })).toHaveFocus())
    expect(screen.queryByRole('group', { name: i18n.t('monthBook.positionLabel') })).not.toBeInTheDocument()
  })

  test('the second sentence names the category and the note', () => {
    renderEdit(booking)
    expect(screen.getByText(/Streaming/)).toBeInTheDocument()
  })

  test('the note is named as unset by default', () => {
    renderEdit({ ...booking, note: null })
    expect(screen.getByRole('button', { name: i18n.t('monthBook.noNote') })).toBeInTheDocument()
  })

  test('the date word changes only the day of the booking (#237)', async () => {
    const user = userEvent.setup()
    const onSave = renderEdit(booking)
    await user.click(screen.getByRole('button', { name: /05\. September 2026/ }))
    await user.click(within(screen.getByRole('group', { name: i18n.t('monthBook.dateLabel') })).getByRole('button', { name: /15\. September 2026/ }))
    await user.click(screen.getByRole('button', { name: 'Speichern' }))
    expect(onSave).toHaveBeenCalledWith({ id: 't1', occurredOn: '2026-09-15' })
  })
})

describe('BookingDialog edit plan month', () => {
  test('names the plan month as a clickable word when there is no position', () => {
    renderEdit(booking)
    expect(screen.getByRole('button', { name: 'September' })).toBeInTheDocument()
  })

  test('picking the next month sends it with the year', async () => {
    const onSave = renderEdit(booking)
    await userEvent.click(screen.getByRole('button', { name: 'September' }))
    await userEvent.click(screen.getByRole('button', { name: 'Oktober (Folgemonat)' }))
    fireEvent.click(screen.getByRole('button', { name: 'Speichern' }))
    expect(onSave).toHaveBeenCalledWith({ id: 't1', planYear: 2026, planMonth: 10 })
  })

  test('the choice crosses the turn of the year', async () => {
    const december = { ...booking, occurredOn: '2026-12-29', planYear: 2026, planMonth: 12 }
    const onSave = renderEdit(december)
    await userEvent.click(screen.getByRole('button', { name: 'Dezember' }))
    await userEvent.click(screen.getByRole('button', { name: 'Januar 2027 (Folgemonat)' }))
    fireEvent.click(screen.getByRole('button', { name: 'Speichern' }))
    expect(onSave).toHaveBeenCalledWith({ id: 't1', planYear: 2027, planMonth: 1 })
  })

  test('with a position the month is the position plan and cannot be picked', () => {
    renderEdit({ ...booking, autoBooked: true, positionId: 'p1', planMonth: 10 })
    expect(screen.getByText('September')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'September' })).not.toBeInTheDocument()
  })

  test('a position from another month counts the booking in that month', () => {
    const october = { ...rent, id: 'p2', label: 'Strom' }
    renderEdit(
      { ...booking, autoBooked: true, positionId: 'p2' },
      { positions: [...september, { year: 2026, month: 10, positions: [october] }] }
    )
    expect(screen.getByText('Oktober')).toBeInTheDocument()
  })

  test('a change of amount alone leaves the plan month out of the request', () => {
    const onSave = renderEdit({ ...booking, planMonth: 10 })
    fireEvent.change(screen.getByLabelText('Betrag'), { target: { value: '80.00' } })
    fireEvent.click(screen.getByRole('button', { name: 'Speichern' }))
    expect(onSave).toHaveBeenCalledWith({ id: 't1', amount: '80.00' })
  })
})

describe('BookingDialog tick', () => {
  test('is titled after the position and starts on the planned amount', () => {
    renderTick()
    expect(screen.getByRole('dialog', { name: i18n.t('paidDialog.title', { label: 'Miete' }) })).toBeInTheDocument()
    const amount = screen.getByLabelText('Betrag')
    expect(amount).toHaveValue('500,00')
    expect(amount).toHaveFocus()
  })

  test('passes an edited amount on submit', async () => {
    const user = userEvent.setup()
    const onConfirm = vi.fn()
    renderTick({ onConfirm })
    const amount = screen.getByLabelText('Betrag')
    await user.clear(amount)
    await user.type(amount, '450')
    await user.click(screen.getByRole('button', { name: 'Abhaken' }))
    expect(onConfirm).toHaveBeenCalledWith(expect.objectContaining({ amount: '450.00' }))
  })

  test('names position and plan month as fixed text, not as words to click', () => {
    renderTick()
    expect(screen.getByText('Miete')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Miete' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'September' })).not.toBeInTheDocument()
  })

  test('names the account of the position as fixed text', () => {
    renderTick({ position: { ...rent, accountId: 'a1' } })
    expect(screen.getByText('Giro')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Giro' })).not.toBeInTheDocument()
  })

  test('names no account when the position has none', () => {
    renderTick()
    expect(screen.queryByText('Giro')).not.toBeInTheDocument()
  })

  test('disables date and amount when the position already has bookings, focus stays inside', () => {
    renderTick({ hasBookings: true })
    expect(screen.getByLabelText('Betrag')).toBeDisabled()
    expect(screen.getByRole('status')).toHaveTextContent(i18n.t('paidDialog.hasBookings'))
    expect(screen.getByRole('dialog')).toContainElement(document.activeElement as HTMLElement)
  })

  test('says when it cannot see the bookings', () => {
    renderTick({ bookingsUnknown: true, viewedMonth: { year: Number(today().slice(0, 4)), month: Number(today().slice(5, 7)) } })
    expect(screen.getByRole('status')).toHaveTextContent(i18n.t('paidDialog.bookingsUnknown'))
  })

  test('says so in one line when the date is outside the plan month, and still books', async () => {
    const user = userEvent.setup()
    const onConfirm = vi.fn()
    renderTick({ onConfirm, viewedMonth: { year: 2000, month: 1 } })
    expect(screen.getByRole('status')).toHaveTextContent(
      i18n.t('paidDialog.outsideMonth', { month: 'Januar 2000' })
    )
    await user.click(screen.getByRole('button', { name: 'Abhaken' }))
    expect(onConfirm).toHaveBeenCalled()
  })

  test('names the date in its sentence, a clickable word that opens a calendar panel', async () => {
    const user = userEvent.setup()
    renderTick()
    const dateWord = screen.getByRole('button', { name: /\d{4}$/ })
    await user.click(dateWord)
    expect(screen.getByRole('group', { name: i18n.t('paidDialog.dateLabel') })).toBeInTheDocument()
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('group', { name: i18n.t('paidDialog.dateLabel') })).not.toBeInTheDocument()
    await waitFor(() => expect(dateWord).toHaveFocus())
  })

  test('the date is named but not clickable once the position already has bookings', () => {
    renderTick({ hasBookings: true })
    expect(screen.queryByRole('button', { name: /\d{4}$/ })).not.toBeInTheDocument()
  })

  test('shows no hint while the date is in the plan month', () => {
    const now = new Date()
    renderTick({ viewedMonth: { year: now.getFullYear(), month: now.getMonth() + 1 } })
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  test('the date word sets the day of payment and leaves the amount alone (#237)', async () => {
    const user = userEvent.setup()
    const onConfirm = vi.fn()
    renderTick({ onConfirm })
    await user.click(screen.getByRole('button', { name: /\d{4}$/ }))
    const panel = screen.getByRole('group', { name: i18n.t('paidDialog.dateLabel') })
    await user.click(within(panel).getByRole('button', { name: /(^|\s)15\. / }))
    await user.click(screen.getByRole('button', { name: 'Abhaken' }))
    expect(onConfirm).toHaveBeenCalledWith({ occurredOn: `${today().slice(0, 8)}15`, amount: '500.00' })
  })
})
