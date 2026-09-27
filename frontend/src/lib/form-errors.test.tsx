import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

import { AmountField } from '@/components/AmountField'
import { Input } from '@/components/ui/input'
import { Form } from '@/lib/form-errors'
import de from '@/locales/de.json'

import { i18n } from '@/lib/i18n'

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

  it('submits when every field is fine', async () => {
    const onSubmit = vi.fn((event: React.FormEvent) => event.preventDefault())
    render(
      <Form onSubmit={onSubmit}>
        <Input id="name" aria-label="Name" required defaultValue="Miete" />
        <button type="submit">Los</button>
      </Form>
    )
    await userEvent.click(screen.getByText('Los'))
    expect(onSubmit).toHaveBeenCalledTimes(1)
  })

  it('submits with Enter in a field once all is fine', async () => {
    const onSubmit = vi.fn((event: React.FormEvent) => event.preventDefault())
    render(
      <Form onSubmit={onSubmit}>
        <Input id="name" aria-label="Name" required />
        <button type="submit">Los</button>
      </Form>
    )
    await userEvent.type(screen.getByLabelText('Name'), 'Miete{Enter}')
    expect(onSubmit).toHaveBeenCalledTimes(1)
  })

  it('shows the range sentence for a number field out of range', async () => {
    const onSubmit = vi.fn()
    render(
      <Form onSubmit={onSubmit}>
        <Input id="n" aria-label="Zahl" type="number" min={1} max={120} defaultValue="500" />
        <button type="submit">Los</button>
      </Form>
    )
    await userEvent.click(screen.getByText('Los'))
    expect(onSubmit).not.toHaveBeenCalled()
    expect(screen.getByText(i18n.t('formErrors.range', { min: 1, max: 120 }))).toBeTruthy()
  })

  it('shows exactly one sentence for an empty amount field', async () => {
    render(
      <Form onSubmit={() => {}}>
        <AmountField id="sum" value="" onChange={() => {}} required />
        <button type="submit">Los</button>
      </Form>
    )
    await userEvent.click(screen.getByText('Los'))
    expect(screen.getAllByRole('alert')).toHaveLength(1)
    expect(screen.getAllByText(de.amountField.empty)).toHaveLength(1)
  })
})
