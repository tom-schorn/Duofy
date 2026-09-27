import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

import { AmountField } from '@/components/AmountField'
import { Input } from '@/components/ui/input'
import { Form } from '@/lib/form-errors'
import de from '@/locales/de.json'

import '@/lib/i18n'

function Sample({ onSubmit }: { onSubmit: () => void }) {
  return (
    <Form onSubmit={onSubmit}>
      <Input id="name" aria-label="Name" required />
      <Input id="mail" aria-label="Mail" type="email" defaultValue="kein-at" />
      <AmountField id="sum" value="" onChange={() => {}} required />
      <button type="submit">Los</button>
    </Form>
  )
}

describe('Form', () => {
  it('shows every mistake at once in the catalog words and focuses the first field', async () => {
    const onSubmit = vi.fn()
    render(<Sample onSubmit={onSubmit} />)
    await userEvent.click(screen.getByText('Los'))

    expect(onSubmit).not.toHaveBeenCalled()
    expect(screen.getByText(de.formErrors.required)).toBeTruthy()
    expect(screen.getByText(de.formErrors.email)).toBeTruthy()
    expect(screen.getByText(de.amountField.empty)).toBeTruthy()
    const name = screen.getByLabelText('Name')
    expect(name.getAttribute('aria-invalid')).toBe('true')
    expect(name.getAttribute('aria-describedby')).toBe('name-error')
    expect(document.activeElement).toBe(name)
  })

  it('removes the sentence when the field is corrected and submits once all is fine', async () => {
    const onSubmit = vi.fn()
    render(<Sample onSubmit={onSubmit} />)
    await userEvent.click(screen.getByText('Los'))
    await userEvent.type(screen.getByLabelText('Name'), 'Miete')
    expect(screen.queryByText(de.formErrors.required)).toBeNull()
  })
})
