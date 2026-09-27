import { useFieldError } from '@/lib/form-errors'

type Props = {
  label: string
  children: React.ReactNode
  /** Same id as the word this panel belongs to — shows its server error, if any (review D-215-5). */
  id?: string
}

/**
 * The choice for one sentence word (issue #215, decision 28): opens right below
 * the sentence, never floating over it. Esc closes it — handled once, by the
 * sentence container around every panel, since it must work no matter whether the
 * focus is still on the word or has moved into the panel.
 */
export function SentencePanel({ label, children, id }: Props) {
  const error = useFieldError(id)
  return (
    <div role="group" aria-label={label} className="bg-muted flex flex-col gap-3 rounded-lg p-4">
      <div className="text-muted-foreground text-xs font-medium">{label}</div>
      {error && (
        <p id={id && `${id}-error`} role="alert" className="text-destructive text-xs">
          {error}
        </p>
      )}
      {children}
    </div>
  )
}
