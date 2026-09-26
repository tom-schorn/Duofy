import { cn } from '@/lib/utils'

/**
 * One row of a list (UI guideline rules 1–4): the whole row opens it.
 *
 * The technique is a stretched link: the button around the text carries an
 * `after:` layer that covers the row, so name, subtitle, amount and the empty space
 * all do the same. What has its own job — the tick box in `leading` — is lifted above
 * that layer with `relative z-10`. There is no menu ⋯ (rule 5): whatever else can be
 * done with the row sits in its edit dialog.
 *
 * Without `onOpen` the row is read-only: no button, no hover, no pointer.
 */
export function ListRow({
  onOpen,
  leading,
  trailing,
  className,
  children,
}: {
  /** Absent for a row that cannot be opened (no right, or nothing to open). */
  onOpen?: () => void
  /** Own control at the left, such as the tick box. */
  leading?: React.ReactNode
  /** The amount; it is not a click area of its own, a click on it opens the row. */
  trailing?: React.ReactNode
  className?: string
  children: React.ReactNode
}) {
  return (
    <li
      className={cn(
        'border-border/60 relative flex items-center gap-3 border-b py-2.5 last:border-b-0',
        onOpen &&
          // Keyboard focus only: a mouse click must not leave the row tinted, and the
          // ring sits around the whole row, not just the text column.
          'hover:bg-muted/50 has-[[data-row-open]:focus-visible]:bg-muted/50 has-[[data-row-open]:focus-visible]:ring-ring cursor-pointer rounded-md has-[[data-row-open]:focus-visible]:ring-2',
        className
      )}
    >
      {leading && <span className="relative z-10 flex shrink-0 items-center">{leading}</span>}

      {onOpen ? (
        <button
          type="button"
          data-row-open
          onClick={onOpen}
          className="flex min-w-0 flex-1 cursor-pointer flex-col items-start gap-0.5 text-left outline-none after:absolute after:inset-0"
        >
          {children}
        </button>
      ) : (
        <div className="flex min-w-0 flex-1 flex-col items-start gap-0.5 text-left">
          {children}
        </div>
      )}

      {trailing && <span className="flex shrink-0 flex-col items-end gap-1 tabular-nums">{trailing}</span>}
    </li>
  )
}
