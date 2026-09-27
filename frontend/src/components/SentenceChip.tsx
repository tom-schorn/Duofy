import { cn } from '@/lib/utils'

type Props = {
  selected: boolean
  onClick: () => void
  children: React.ReactNode
}

/** One choice inside a sentence word's panel (issue #215). */
export function SentenceChip({ selected, onClick, children }: Props) {
  return (
    <button
      type="button"
      aria-pressed={selected}
      onClick={onClick}
      className={cn(
        'flex h-10 items-center gap-2 rounded-md border px-3.5 text-sm',
        selected
          ? 'border-primary bg-primary text-primary-foreground'
          : 'border-border bg-background text-foreground hover:bg-muted'
      )}
    >
      {children}
    </button>
  )
}
