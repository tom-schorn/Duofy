import { forwardRef } from 'react'

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
}

/**
 * One clickable word in a „Satz statt Formular“ sentence (issue #215, decision 28):
 * a dotted underline in the primary colour, its choice opening in a panel right
 * below the sentence — never a popover that floats over the rest of the dialog.
 */
export const SentenceWord = forwardRef<HTMLButtonElement, Props>(function SentenceWord(
  { open, onClick, children, describedBy },
  ref
) {
  return (
    <button
      ref={ref}
      type="button"
      aria-expanded={open}
      aria-describedby={describedBy}
      onClick={onClick}
      className={cn(
        'rounded px-0.5 -mx-0.5 font-medium text-primary underline decoration-dotted underline-offset-4 outline-none',
        'focus-visible:ring-ring focus-visible:ring-2',
        open && 'bg-muted'
      )}
    >
      {children}
    </button>
  )
})
