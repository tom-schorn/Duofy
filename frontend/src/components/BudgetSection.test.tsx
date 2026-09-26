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
    const heading = screen.getByRole('heading', { name: /Fixkosten/ })
    heading.focus()
    expect(heading).toHaveFocus()
  })
})
