type Props = {
  label: string
  children: React.ReactNode
}

/**
 * The choice for one sentence word (issue #215, decision 28): opens right below
 * the sentence, never floating over it. Esc closes it — handled once, by the
 * sentence container around every panel, since it must work no matter whether the
 * focus is still on the word or has moved into the panel.
 */
export function SentencePanel({ label, children }: Props) {
  return (
    <div role="group" aria-label={label} className="bg-muted flex flex-col gap-3 rounded-lg p-4">
      <div className="text-muted-foreground text-xs font-medium">{label}</div>
      {children}
    </div>
  )
}
