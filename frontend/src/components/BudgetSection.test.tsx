import '@/lib/i18n'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, test, vi } from 'vitest'

import { BudgetSection } from '@/components/BudgetSection'
import type { PlanPosition } from '@/lib/domain'

const position = {
  id: 'p1',
  label: 'Miete',
  amountPlanned: '500.00',
  amountActual: null,
  category: 'rent',
  budget: 'needs',
  dueDay: 1,
  isLimit: false,
  passThrough: false,
  counterAccountId: null,
  paymentMethod: null,
  householdId: null,
  commitmentId: null,
  paidAt: null,
} as unknown as PlanPosition

function renderSection(props: Partial<React.ComponentProps<typeof BudgetSection>> = {}) {
  const handlers = { onEdit: vi.fn(), onAdd: vi.fn(), onTogglePaid: vi.fn() }
  render(
    <BudgetSection
      budget="needs"
      target={1000}
      positions={[position]}
      householdNames={{}}
      {...handlers}
      {...props}
    />
  )
  return handlers
}

describe('BudgetSection rows', () => {
  test('a click on the row opens it, the tick box only ticks', async () => {
    const user = userEvent.setup()
    const h = renderSection()
    await user.click(screen.getByRole('checkbox'))
    expect(h.onTogglePaid).toHaveBeenCalledTimes(1)
    expect(h.onEdit).not.toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: /^Miete(?!:)/ }))
    expect(h.onEdit).toHaveBeenCalledTimes(1)
  })

  test('a row has no menu: nothing named "weitere Aktionen" anywhere', () => {
    renderSection()
    expect(screen.queryByRole('button', { name: /weitere Aktionen/ })).not.toBeInTheDocument()
  })

  test('a read-only row has no row button', () => {
    renderSection({ readOnly: true })
    expect(screen.queryByRole('button', { name: /^Miete(?!:)/ })).not.toBeInTheDocument()
  })

  test('the section heading can take the focus after a delete', () => {
    renderSection()
    const heading = screen.getByRole('heading', { name: /Grundbedarf/ })
    heading.focus()
    expect(heading).toHaveFocus()
  })

  test('a read-only paid row shows its state as a plain symbol, not as a greyed-out box', () => {
    renderSection({ readOnly: true, positions: [{ ...position, paidAt: '2026-10-01' } as PlanPosition] })
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
    expect(screen.getByRole('img', { name: 'bezahlt' })).toBeInTheDocument()
  })

  test('a read-only open row shows no state control at all', () => {
    renderSection({ readOnly: true })
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
    expect(screen.queryByRole('img')).not.toBeInTheDocument()
  })

  test('an overdue hint shows its text at the position it belongs to', () => {
    renderSection({
      hints: [
        {
          code: 'position_overdue',
          severity: 'warning',
          positionId: 'p1',
          params: { due_date: '2026-09-01', days_overdue: 3 },
        },
      ],
    })
    expect(screen.getByText(/^Seit 3 Tagen/)).toBeInTheDocument()
  })

  test('a hint for another position is not shown here', () => {
    renderSection({
      hints: [
        { code: 'position_overdue', severity: 'warning', positionId: 'other', params: { days_overdue: 1 } },
      ],
    })
    expect(screen.queryByText(/^Seit /)).not.toBeInTheDocument()
  })

  test('an unknown hint code is ignored, never shown raw', () => {
    renderSection({
      hints: [{ code: 'from_the_future', severity: 'info', positionId: 'p1', params: {} }],
    })
    expect(screen.queryByText(/from_the_future|hints\./)).not.toBeInTheDocument()
  })

  test('a hint cannot be dismissed', () => {
    renderSection({
      hints: [{ code: 'position_overdue', severity: 'warning', positionId: 'p1', params: { days_overdue: 1 } }],
    })
    expect(screen.getByText(/^Seit einem Tag/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /schließen|ausblenden|verwerfen/i })).not.toBeInTheDocument()
  })
})
