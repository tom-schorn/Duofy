import { cn } from '@/lib/utils'

type Props = {
  selected: boolean
  onClick: () => void
  /** A choice that cannot be taken right now (say, a month before the start). */
  disabled?: boolean
  children: React.ReactNode
}

/** One choice inside a sentence word's panel (issue #215). */
export function SentenceChip({ selected, onClick, disabled = false, children }: Props) {
  return (
    <button
      type="button"
      aria-pressed={selected}
      // aria-disabled, not disabled: the chip stays reachable by keyboard and
      // screen reader, so the hint next to it can explain why it does nothing.
      aria-disabled={disabled || undefined}
      onClick={disabled ? undefined : onClick}
      className={cn(
        'flex h-10 items-center gap-2 rounded-md border px-3.5 text-sm',
        selected
          ? 'border-primary bg-primary text-primary-foreground'
          : 'border-border bg-background text-foreground hover:bg-muted',
        disabled && 'cursor-not-allowed opacity-40 hover:bg-background'
      )}
    >
      {children}
    </button>
  )
}
