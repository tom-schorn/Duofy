import {
  createContext,
  useCallback,
  useContext,
  useState,
  type ComponentProps,
} from 'react'
import { useTranslation } from 'react-i18next'
import type { TFunction } from 'i18next'

/** Marks a control that explains its own mistake (the amount field). */
export const OWN_ERROR_ATTRIBUTE = 'data-own-error'

type Control = HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement

const ErrorsContext = createContext<Record<string, string>>({})

/** The sentence a form shows under the field with this id, if it has one. */
export function useFieldError(id: string | undefined): string | undefined {
  const errors = useContext(ErrorsContext)
  return id ? errors[id] : undefined
}

/** Duofy's own sentence for what the browser found wrong with a control. */
function sentence(control: Control, t: TFunction): string {
  const v = control.validity
  if (v.valueMissing) return t('formErrors.required')
  if (v.typeMismatch && control.type === 'email') return t('formErrors.email')
  if (v.tooShort) return t('formErrors.tooShort', { min: control.getAttribute('minlength') })
  if (v.rangeUnderflow || v.rangeOverflow || v.stepMismatch || v.badInput) {
    return t('formErrors.range', { min: control.getAttribute('min'), max: control.getAttribute('max') })
  }
  return control.validationMessage || t('formErrors.required')
}

/**
 * Finds every invalid control of a form, in page order. Controls that explain
 * themselves are asked to (`checkValidity` makes them report); the others get a
 * sentence from the catalog.
 */
function collect(form: HTMLFormElement, t: TFunction) {
  const found: { control: Control; message: string | null }[] = []
  for (const element of Array.from(form.elements)) {
    if (!(element instanceof HTMLInputElement || element instanceof HTMLSelectElement || element instanceof HTMLTextAreaElement)) continue
    if (element.disabled || element.validity.valid) continue
    const own = element.hasAttribute(OWN_ERROR_ATTRIBUTE)
    // Rule: a sentence is shown under a field only through its `id` (see Form).
    // An invalid control without an id (and not explaining itself) would fail silently.
    if (!own && !element.id && import.meta.env.DEV) {
      console.warn('Form: invalid field without an id gets no error sentence', element.name || element.type)
    }
    if (own) element.checkValidity()
    found.push({ control: element, message: own ? null : sentence(element, t) })
  }
  return found
}

/**
 * The one `<form>` of the app (UI guideline rules 21, 22): the browser's own bubble
 * is switched off; on submit every mistake is written under its field in the
 * catalog's words, all at once, and the focus goes to the first. A field that has
 * an `id` shows its sentence through {@link Input}.
 */
export function Form({
  onSubmit,
  onInput,
  ...props
}: Omit<ComponentProps<'form'>, 'noValidate'>) {
  const { t } = useTranslation()
  const [errors, setErrors] = useState<Record<string, string>>({})

  const handleSubmit = useCallback(
    (event: Parameters<NonNullable<ComponentProps<'form'>['onSubmit']>>[0]) => {
      const found = collect(event.currentTarget, t)
      if (found.length === 0) {
        setErrors({})
        onSubmit?.(event)
        return
      }
      event.preventDefault()
      const next: Record<string, string> = {}
      for (const { control, message } of found) {
        if (control.id && message) next[control.id] = message
      }
      setErrors(next)
      found[0].control.focus()
    },
    [onSubmit, t]
  )

  return (
    <ErrorsContext.Provider value={errors}>
      <form
        {...props}
        noValidate
        onSubmit={handleSubmit}
        onInput={(event) => {
          // A field that is being corrected loses its sentence.
          const id = (event.target as Control).id
          if (id && errors[id]) {
            setErrors(({ [id]: _gone, ...rest }) => rest)
          }
          onInput?.(event)
        }}
      />
    </ErrorsContext.Provider>
  )
}
