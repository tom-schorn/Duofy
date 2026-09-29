import { fireEvent, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, test, vi } from 'vitest'

import { i18n } from '@/lib/i18n'
import { renderAt, stub, TICKED, type Tick } from '@/test/plan-stub'

/**
 * #251: ticking a position off works the same in every plan — own, another
 * person's and the household's. Only the grant decides whether the box is there.
 */

const tickBox = (label: string) => i18n.t('budget.tick', { label })

async function tickAndConfirm(label: string) {
  fireEvent.click(await screen.findByRole('checkbox', { name: tickBox(label) }))
  expect(
    await screen.findByRole('dialog', {
      name: i18n.t('paidDialog.title', { label }),
    })
  ).toBeInTheDocument()
  fireEvent.click(screen.getByRole('button', { name: i18n.t('paidDialog.submit') }))
}

afterEach(() => vi.unstubAllGlobals())

const booking = (positionId: string) => ({
  id: 'b1',
  positionId,
  autoBooked: true,
  amount: '42.00',
  occurredOn: '2026-11-03',
  accountId: 'a1',
  label: 'Miete',
})

async function untickAndConfirm(label: string) {
  const box = await screen.findByRole('checkbox', {
    name: i18n.t('budget.reopen', { label }),
  })
  fireEvent.click(box)
  return screen.findByRole('alertdialog')
}

describe('ticking off, one operation in every plan (#251)', () => {
  test('own plan: the dialog opens and the tick carries date and amount', async () => {
    const ticks: Tick[] = []
    stub('none', ticks)
    renderAt('/plan/2026/11')
    await tickAndConfirm('Miete')
    await waitFor(() => expect(ticks).toHaveLength(1))
    expect(ticks[0].url).toContain('/positions/p1/paid')
    expect(ticks[0].body).toMatchObject({ amount: '500.00' })
    expect(ticks[0].body).toHaveProperty('occurredOn')
  })

  test('another person with the edit grant: the same dialog, the same tick', async () => {
    const ticks: Tick[] = []
    stub('edit', ticks)
    renderAt('/plan/2026/11?member=u2')
    await tickAndConfirm('Miete')
    await waitFor(() => expect(ticks).toHaveLength(1))
    expect(ticks[0].url).toContain('/positions/p1/paid')
    expect(ticks[0].body).toMatchObject({ amount: '500.00' })
    expect(ticks[0].body).toHaveProperty('occurredOn')
  })

  test('another person without the edit grant: no box at all, only the view', async () => {
    stub('view', [])
    renderAt('/plan/2026/11?member=u2')
    await screen.findByText('November 2026')
    await screen.findAllByText('Miete')
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
  })

  test('household plan, own position: the same dialog', async () => {
    const ticks: Tick[] = []
    stub('view', ticks)
    renderAt('/plan/2026/11?household=h1')
    await tickAndConfirm('Miete')
    await waitFor(() => expect(ticks).toHaveLength(1))
    expect(ticks[0].url).toContain('/positions/p1/paid')
    expect(ticks[0].body).toHaveProperty('occurredOn')
  })

  test('household plan, position of somebody with the edit grant: the same dialog', async () => {
    const ticks: Tick[] = []
    stub('edit', ticks)
    renderAt('/plan/2026/11?household=h1')
    await tickAndConfirm('Strom')
    await waitFor(() => expect(ticks).toHaveLength(1))
    expect(ticks[0].url).toContain('/positions/p2/paid')
    expect(ticks[0].body).toHaveProperty('occurredOn')
  })

  test('household plan, position of somebody without the edit grant: no box', async () => {
    stub('view', [])
    renderAt('/plan/2026/11?household=h1')
    expect(
      await screen.findByRole('checkbox', { name: tickBox('Miete') })
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('checkbox', { name: tickBox('Strom') })
    ).not.toBeInTheDocument()
  })

  test.each([
    ['own plan', '/plan/2026/11', 'none', 'Miete', 'p1'],
    ['another person plan', '/plan/2026/11?member=u2', 'edit', 'Miete', 'p1'],
    ['household plan', '/plan/2026/11?household=h1', 'edit', 'Strom', 'p2'],
  ])(
    '%s: unticking asks with the amount, confirming sends the DELETE',
    async (_n, path, level, label, id) => {
      const deletes: string[] = []
      stub(level, [], { transactions: [booking(id)], paidAt: TICKED, deletes })
      renderAt(path)
      const dialog = await untickAndConfirm(label)
      await waitFor(() => expect(dialog).toHaveTextContent('42,00'))
      expect(deletes).toHaveLength(0)
      fireEvent.click(screen.getByRole('button', { name: i18n.t('plan.untick') }))
      await waitFor(() => expect(deletes).toHaveLength(1))
      expect(deletes[0]).toContain(`/positions/${id}/paid`)
      await waitFor(() =>
        expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
      )
    }
  )

  test('bookings not loadable: unticking still asks, without an amount', async () => {
    const deletes: string[] = []
    stub('edit', [], { transactions: null, paidAt: TICKED, deletes })
    renderAt('/plan/2026/11?member=u2')
    const dialog = await untickAndConfirm('Miete')
    expect(dialog).toHaveTextContent(i18n.t('plan.untickTextUnknown'))
    expect(deletes).toHaveLength(0)
    fireEvent.click(screen.getByRole('button', { name: i18n.t('plan.untick') }))
    await waitFor(() => expect(deletes).toHaveLength(1))
  })

  test('bookings not loadable: the tick dialog does not pretend date and amount count', async () => {
    stub('edit', [], { transactions: null })
    renderAt('/plan/2026/11?member=u2')
    fireEvent.click(await screen.findByRole('checkbox', { name: tickBox('Miete') }))
    expect(
      await screen.findByText(i18n.t('paidDialog.bookingsUnknown'))
    ).toBeInTheDocument()
  })
})
