import { fireEvent, render, screen } from '@testing-library/react'
import { useState } from 'react'
import { describe, expect, test } from 'vitest'

import { AmountField } from '@/components/AmountField'

/** The field as a form uses it: the API value is what the parent holds. */
function Holder({ start = '', required = true }: { start?: string; required?: boolean }) {
  const [value, setValue] = useState(start)
  return (
    <form>
      <label htmlFor="a">Betrag</label>
      <AmountField id="a" value={value} onChange={setValue} required={required} />
      <output data-testid="api">{value}</output>
    </form>
  )
}

function Ext({ value, allowZero = false }: { value: string; allowZero?: boolean }) {
  return (
    <form>
      <label htmlFor="a">Betrag</label>
      <AmountField id="a" value={value} onChange={() => {}} allowZero={allowZero} />
    </form>
  )
}

const field = () => screen.getByLabelText('Betrag') as HTMLInputElement
const api = () => screen.getByTestId('api').textContent

describe('AmountField', () => {
  test('an invalid text in an optional field blocks the form', () => {
    render(<Holder required={false} />)
    fireEvent.change(field(), { target: { value: '12e3' } })
    expect(field().form?.checkValidity()).toBe(false)
  })

  test('an empty optional field does not block the form', () => {
    render(<Holder required={false} />)
    expect(field().form?.checkValidity()).toBe(true)
  })

  test('a value changed from outside replaces the text', () => {
    const { rerender } = render(<Ext value="10" />)
    expect(field().value).toBe('10,00')
    rerender(<Ext value="25.5" />)
    expect(field().value).toBe('25,50')
  })

  test('zero is only accepted with allowZero', () => {
    const { rerender } = render(<Ext value="" />)
    fireEvent.change(field(), { target: { value: '0' } })
    expect(field().form?.checkValidity()).toBe(false)
    rerender(<Ext value="" allowZero />)
    expect(field().form?.checkValidity()).toBe(true)
  })

  test('focuses the first invalid field when submitting', () => {
    render(<Holder />)
    fireEvent.submit(field().form as HTMLFormElement)
    field().form?.reportValidity()
    expect(field()).toHaveFocus()
  })

  test('is a text field with a decimal keypad and a euro sign', () => {
    render(<Holder />)
    expect(field().type).toBe('text')
    expect(field().inputMode).toBe('decimal')
    expect(screen.getByText('€')).toBeInTheDocument()
  })

  test('hands the API a decimal string and formats it on leaving the field', () => {
    render(<Holder />)
    fireEvent.change(field(), { target: { value: '1.234,5' } })
    expect(api()).toBe('1234.50')
    fireEvent.blur(field())
    expect(field().value).toBe('1.234,50')
  })

  test('shows an API value the German way', () => {
    render(<Holder start="80.5" />)
    expect(field().value).toBe('80,50')
  })

  test('refuses 12e3 in Duofys words under the field, not in a browser bubble', () => {
    render(<Holder />)
    fireEvent.change(field(), { target: { value: '12e3' } })
    fireEvent.blur(field())
    expect(api()).toBe('')
    expect(screen.getByRole('alert')).toHaveTextContent('Bitte einen Betrag wie 1.234,56 eingeben.')
    expect(field()).toHaveAttribute('aria-invalid', 'true')
  })

  test('refuses zero and a third decimal', () => {
    render(<Holder />)
    fireEvent.change(field(), { target: { value: '0' } })
    fireEvent.blur(field())
    expect(screen.getByRole('alert')).toHaveTextContent('mindestens 0,01')
    fireEvent.change(field(), { target: { value: '0,005' } })
    fireEvent.blur(field())
    expect(screen.getByRole('alert')).toHaveTextContent('zwei Nachkommastellen')
  })

  test('blocks the form and says so when a required field is empty', () => {
    render(<Holder />)
    const form = field().closest('form') as HTMLFormElement
    expect(form.checkValidity()).toBe(false)
    fireEvent.invalid(field())
    expect(screen.getByRole('alert')).toHaveTextContent('Bitte einen Betrag eingeben.')
  })

  test('an optional field may stay empty', () => {
    render(<Holder required={false} />)
    const form = field().closest('form') as HTMLFormElement
    expect(form.checkValidity()).toBe(true)
  })
})
