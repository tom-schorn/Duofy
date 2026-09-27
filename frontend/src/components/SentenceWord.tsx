import { forwardRef } from 'react'

import { useFieldError } from '@/lib/form-errors'
import { cn } from '@/lib/utils'

type Props = {
  open: boolean
  onClick: () => void
  children: React.ReactNode
  /**
   * The id of the sentence paragraph this word is part of — read out with the
   * word so a screen reader hears the whole sentence, not just the one word
   * (issue #202, kept through the #215 rewrite: review D-215-3, fix 1).
   */
  describedBy?: string
  /**
   * Ties the word to a server field error carried through the form-errors
   * helper (#203) — the same lookup {@link Input} uses. Set means the server
   * rejected this word's value (review D-215-5): the word gets a destructive
   * underline and is described by the message in its panel — `aria-invalid`
   * is a widget state the ARIA spec does not grant to a plain button.
   */
  id?: string
}

/** The id {@link SentencePanel} gives the message it shows for this word. */
function errorId(id: string | undefined): string | undefined {
  return id ? `${id}-error` : undefined
}

/**
 * One clickable word in a „Satz statt Formular“ sentence (issue #215, decision 28):
 * a dotted underline in the primary colour, its choice opening in a panel right
 * below the sentence — never a popover that floats over the rest of the dialog.
 */
export const SentenceWord = forwardRef<HTMLButtonElement, Props>(function SentenceWord(
  { open, onClick, children, describedBy, id },
  ref
) {
  const invalid = useFieldError(id) !== undefined
  return (
    <button
      ref={ref}
      id={id}
      type="button"
      aria-expanded={open}
      aria-describedby={
        [describedBy, invalid && errorId(id)].filter(Boolean).join(' ') || undefined
      }
      onClick={onClick}
      className={cn(
        'rounded px-0.5 -mx-0.5 font-medium underline underline-offset-4 outline-none',
        'focus-visible:ring-ring focus-visible:ring-2',
        invalid ? 'text-destructive decoration-solid' : 'text-primary decoration-dotted',
        open && 'bg-muted'
      )}
    >
      {children}
    </button>
  )
})
