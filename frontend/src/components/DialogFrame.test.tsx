import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, test } from 'vitest'

import { DialogFrame } from '@/components/DialogFrame'
import { Input } from '@/components/ui/input'

function Harness({ dirtyAfterTyping = true }: { dirtyAfterTyping?: boolean }) {
  const [open, setOpen] = useState(false)
  const [value, setValue] = useState('')
  return (
    <>
      <button onClick={() => setOpen(true)}>Öffnen</button>
      <DialogFrame
        open={open}
        onOpenChange={setOpen}
        title="Titel"
        submitLabel="Anlegen"
        onSubmit={() => {}}
        dirty={dirtyAfterTyping && value !== ''}
      >
        <Input aria-label="Name" value={value} onChange={(e) => setValue(e.target.value)} />
      </DialogFrame>
    </>
  )
}

describe('DialogFrame', () => {
  test('puts the focus into the first field and back on the trigger after Esc', async () => {
    const user = userEvent.setup()
    render(<Harness />)
    const trigger = screen.getByRole('button', { name: 'Öffnen' })
    await user.click(trigger)
    expect(screen.getByLabelText('Name')).toHaveFocus()
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(trigger).toHaveFocus()
  })

  test('closes on Esc when nothing was changed', async () => {
    const user = userEvent.setup()
    render(<Harness />)
    await user.click(screen.getByRole('button', { name: 'Öffnen' }))
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  test('asks before discarding on Esc after a change, and keeps the input on "keep editing"', async () => {
    const user = userEvent.setup()
    render(<Harness />)
    await user.click(screen.getByRole('button', { name: 'Öffnen' }))
    await user.type(screen.getByLabelText('Name'), 'Miete')
    await user.keyboard('{Escape}')
    expect(screen.getByText('Änderungen verwerfen?')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Weiter bearbeiten' }))
    expect(screen.getByLabelText('Name')).toHaveValue('Miete')
    await user.keyboard('{Escape}')
    await user.click(screen.getByRole('button', { name: 'Verwerfen' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  test('Abbrechen closes without asking', async () => {
    const user = userEvent.setup()
    render(<Harness />)
    await user.click(screen.getByRole('button', { name: 'Öffnen' }))
    await user.type(screen.getByLabelText('Name'), 'Miete')
    await user.click(screen.getByRole('button', { name: 'Abbrechen' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  test('cannot be closed and cannot cancel while the server has not answered', async () => {
    const user = userEvent.setup()
    render(
      <DialogFrame open onOpenChange={() => { throw new Error('closed') }} title="T" submitLabel="Anlegen" onSubmit={() => {}} pending>
        <Input aria-label="Name" />
      </DialogFrame>
    )
    expect(screen.getByRole('button', { name: 'Abbrechen' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Speichert…' })).toBeDisabled()
    await user.keyboard('{Escape}')
    expect(screen.getByRole('dialog')).toBeInTheDocument()
  })
})
