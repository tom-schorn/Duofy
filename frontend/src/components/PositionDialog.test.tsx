import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, test, vi } from 'vitest'

import { PositionDialog } from '@/components/PositionDialog'
import { budgetHeadingId, budgetLabel, type PlanPosition } from '@/lib/domain'
import { ApiError } from '@/lib/api'
import { i18n } from '@/lib/i18n'

// A parent the way the pages are: it closes on success only, and hands the error
// back to the dialog when the server says no.
function Page() {
  const [open, setOpen] = useState(true)
  const [error, setError] = useState<unknown>(null)
  return (
    <QueryClientProvider client={new QueryClient()}>
      <PositionDialog
        position={null}
        budget="needs"
        planId="p1"
        open={open}
        onOpenChange={setOpen}
        onSave={() => setError(new ApiError('not_allowed', 403))}
        error={error}
      />
    </QueryClientProvider>
  )
}

describe('PositionDialog', () => {
  test('stays open, shows the error box and keeps the input when saving fails', async () => {
    const user = userEvent.setup()
    render(<Page />)
    await user.type(screen.getByLabelText('Bezeichnung'), 'Miete')
    await user.type(screen.getByLabelText('Betrag'), '500')
    await user.click(screen.getByRole('button', { name: 'Anlegen' }))
    expect(screen.getByRole('dialog')).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('Dazu fehlt dir die Berechtigung.')
    expect(screen.getByLabelText('Bezeichnung')).toHaveValue('Miete')
  })
})

const existing = {
  id: 'p1',
  label: 'Miete',
  amountPlanned: '500.00',
  amountActual: null,
  category: 'housing.rent',
  budget: 'needs',
  dueDay: 1,
  accountId: null,
  isLimit: false,
  counterAccountId: null,
  passThrough: false,
  paymentMethod: null,
  householdId: null,
  commitmentId: null,
  paidAt: null,
} as PlanPosition

function EditPage({ onDelete }: { onDelete?: (position: PlanPosition) => void }) {
  const [open, setOpen] = useState(true)
  return (
    <QueryClientProvider client={new QueryClient()}>
      <h2 id={budgetHeadingId('needs')} tabIndex={-1}>
        {budgetLabel('needs')}
      </h2>
      <h2 id={budgetHeadingId('wants')} tabIndex={-1}>
        {budgetLabel('wants')}
      </h2>
      <PositionDialog
        position={existing}
        budget="needs"
        planId="p1"
        open={open}
        onOpenChange={setOpen}
        onSave={() => {}}
        onDelete={onDelete}
      />
    </QueryClientProvider>
  )
}

describe('PositionDialog delete', () => {
  test('offers Delete in the footer when allowed, and calls it with the position', async () => {
    const user = userEvent.setup()
    const onDelete = vi.fn()
    render(<EditPage onDelete={onDelete} />)
    await user.click(screen.getByRole('button', { name: i18n.t('common.delete') }))
    expect(onDelete).toHaveBeenCalledWith(expect.objectContaining({ id: 'p1' }))
  })

  test('has no Delete without the right to delete', () => {
    render(<EditPage />)
    expect(screen.queryByRole('button', { name: i18n.t('common.delete') })).not.toBeInTheDocument()
  })

  test('has no Delete when creating', () => {
    render(
      <QueryClientProvider client={new QueryClient()}>
        <PositionDialog
          position={null}
          budget="needs"
          planId="p1"
          open
          onOpenChange={() => {}}
          onSave={() => {}}
          onDelete={() => {}}
        />
      </QueryClientProvider>
    )
    expect(screen.queryByRole('button', { name: i18n.t('common.delete') })).not.toBeInTheDocument()
  })

  test('after a delete the focus lands on the section heading', async () => {
    const user = userEvent.setup()
    render(<EditPage onDelete={() => {}} />)
    await user.click(screen.getByRole('button', { name: i18n.t('common.delete') }))
    await waitFor(() =>
      expect(screen.getByRole('heading', { name: budgetLabel('needs') })).toHaveFocus()
    )
  })

  test('after a delete the focus lands on the section the row was in, even if its budget was changed', async () => {
    const user = userEvent.setup()
    render(<EditPage onDelete={() => {}} />)
    const budgetSelect = screen
      .getAllByRole('combobox')
      .find((select) => select.textContent === budgetLabel('needs'))!
    await user.click(budgetSelect)
    await user.click(screen.getByRole('option', { name: budgetLabel('wants') }))
    await user.click(screen.getByRole('button', { name: i18n.t('common.delete') }))
    await waitFor(() =>
      expect(screen.getByRole('heading', { name: budgetLabel('needs') })).toHaveFocus()
    )
  })
})
