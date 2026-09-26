import { useEffect, useId, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { Input } from '@/components/ui/input'
import { formatAmount, parseAmount, type AmountError } from '@/lib/amount'
import { cn } from '@/lib/utils'

/**
 * The one field for every amount (UI guideline rule 23).
 *
 * A text input with a decimal keypad that reads `1.234,56`, `1234,56` and `1234.56`,
 * refuses `12e3`, letters and a third decimal, shows the euro sign as a suffix and
 * writes two decimals when it is left. `value` and `onChange` speak the API's
 * language, a decimal string (`"1234.56"`), or `''` while nothing valid is typed.
 *
 * A wrong entry is explained under the field, in the catalog's words. The field also
 * tells the browser it is invalid (`setCustomValidity`), so a form does not submit,
 * and it swallows the browser's own bubble, showing the sentence itself instead.
 */
export function AmountField({
  id,
  value,
  onChange,
  required = false,
  allowZero = false,
  disabled,
  placeholder,
  className,
  'aria-describedby': describedBy,
}: {
  id?: string
  value: string
  onChange: (value: string) => void
  required?: boolean
  /** A planned amount may be 0,00 €; bookings and ticking may not. */
  allowZero?: boolean
  disabled?: boolean
  placeholder?: string
  className?: string
  'aria-describedby'?: string
}) {
  const { t } = useTranslation()
  const errorId = useId()
  const input = useRef<HTMLInputElement>(null)
  const [text, setText] = useState(() => formatAmount(value))
  const [error, setError] = useState<AmountError | null>(null)
  // What this field last handed up: a different `value` came from outside.
  const emitted = useRef(value)

  useEffect(() => {
    if (value !== emitted.current) {
      emitted.current = value
      setText(formatAmount(value))
      setError(null)
    }
  }, [value])

  /** What is wrong with the text as it stands; an empty optional field is fine. */
  function problem(current: string): AmountError | null {
    const parsed = parseAmount(current, { allowZero })
    if (parsed.ok) return null
    if (parsed.reason === 'empty') return required ? 'empty' : null
    return parsed.reason
  }

  const current = problem(text)
  useEffect(() => {
    input.current?.setCustomValidity(current === null ? '' : t(`amountField.${current}`))
  }, [current, t])

  function handleChange(next: string) {
    setText(next)
    const parsed = parseAmount(next, { allowZero })
    const api = parsed.ok ? parsed.value : ''
    emitted.current = api
    onChange(api)
    // Once a sentence is showing it follows the typing; a first mistake waits for
    // the blur, so nobody is scolded halfway through "1.234,".
    if (error !== null) setError(problem(next))
  }

  function handleBlur() {
    const parsed = parseAmount(text, { allowZero })
    if (parsed.ok) setText(formatAmount(parsed.value))
    setError(problem(text))
  }

  return (
    <div className={cn('flex flex-col gap-1', className)}>
      <div className="relative">
        <Input
          ref={input}
          id={id}
          type="text"
          inputMode="decimal"
          autoComplete="off"
          value={text}
          onChange={(event) => handleChange(event.target.value)}
          onBlur={handleBlur}
          // The browser's bubble would say it in its own words; ours goes below.
          onInvalid={(event) => {
            event.preventDefault()
            setError(problem(text) ?? 'empty')
            // preventDefault also stops the browser from focusing the first invalid
            // control, so do it when this is that one.
            const form = event.currentTarget.form
            if (!form || form.querySelector(':invalid') === event.currentTarget) {
              event.currentTarget.focus()
            }
          }}
          required={required}
          disabled={disabled}
          placeholder={placeholder ?? t('common.amountPlaceholder')}
          aria-invalid={error !== null}
          aria-describedby={cn(error && errorId, describedBy) || undefined}
          className="pr-8 text-right tabular-nums"
        />
        <span
          aria-hidden="true"
          className="text-muted-foreground pointer-events-none absolute inset-y-0 right-3 flex items-center text-sm"
        >
          €
        </span>
      </div>
      {error !== null && (
        <p id={errorId} role="alert" className="text-destructive text-xs">
          {t(`amountField.${error}`)}
        </p>
      )}
    </div>
  )
}
