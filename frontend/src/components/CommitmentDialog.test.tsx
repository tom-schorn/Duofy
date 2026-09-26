import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, test } from 'vitest'

import { CommitmentDialog } from '@/components/CommitmentDialog'

function renderDialog(onOpenChange: (open: boolean) => void) {
  render(
    <QueryClientProvider client={new QueryClient()}>
      <CommitmentDialog commitment={null} open onOpenChange={onOpenChange} onSave={() => {}} />
    </QueryClientProvider>
  )
}

describe('CommitmentDialog', () => {
  test('offers Anlegen and puts the focus into the first field', () => {
    renderDialog(() => {})
    expect(screen.getByRole('button', { name: 'Anlegen' })).toBeInTheDocument()
    expect(screen.getAllByRole('textbox')[0]).toHaveFocus()
  })

  test('asks before discarding after a change', async () => {
    const user = userEvent.setup()
    let closed = false
    renderDialog(() => {
      closed = true
    })
    await user.type(screen.getAllByRole('textbox')[0], 'Miete')
    await user.keyboard('{Escape}')
    expect(screen.getByText(/verwerfen[?]/)).toBeInTheDocument()
    expect(closed).toBe(false)
  })
})

describe('CommitmentDialog interval', () => {
  test('a click on the type that is already chosen does not make the dialog dirty', async () => {
    const user = userEvent.setup()
    let closed = false
    renderDialog(() => {
      closed = true
    })
    await user.click(screen.getByRole('button', { name: /weiter/ }))
    await user.keyboard('{Escape}')
    expect(screen.queryByText(/verwerfen[?]/)).not.toBeInTheDocument()
    expect(closed).toBe(true)
  })
})

describe('CommitmentDialog invalid interval', () => {
  test('blocks saving with interval 0 and says why', async () => {
    const user = userEvent.setup()
    renderDialog(() => {})
    await user.click(screen.getByRole('combobox', { name: /Abstand/ }))
    await user.click(await screen.findByRole('option', { name: 'Anderer Abstand' }))
    const field = await screen.findByLabelText('Abstand in Monaten')
    await user.clear(field)
    await user.type(field, '0')
    expect(screen.getByRole('button', { name: 'Anlegen' })).toBeDisabled()
    expect(screen.getByText(/Ganze Monate von 1 bis 120/)).toBeInTheDocument()
    expect(field).toHaveAttribute('aria-invalid', 'true')
  })
})
