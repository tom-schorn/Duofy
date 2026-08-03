import { euro } from '@/lib/domain'

/**
 * Eine Kennzahl als Karte.
 *
 * Eigene Datei, weil Plan und Buch dieselbe Karte benutzen, aber andere Zahlen
 * hineinstellen: der Plan zeigt Soll-Werte, das Buch Ist-Werte.
 */
export function Metric({
  label,
  value,
  hint,
  strong = false,
  tone = 'neutral',
}: {
  label: string
  value: number
  hint?: string
  strong?: boolean
  tone?: 'neutral' | 'over'
}) {
  return (
    <div className="bg-card border-border flex flex-col gap-1 rounded-lg border p-4">
      <span className="text-muted-foreground text-[11px] font-semibold tracking-widest uppercase">
        {label}
      </span>
      <span
        className={`font-mono tabular-nums ${strong ? 'text-xl font-semibold' : 'text-lg font-medium'} ${
          tone === 'over' ? 'text-destructive' : ''
        }`}
      >
        {euro.format(value)}
      </span>
      {hint && <span className="text-muted-foreground text-xs">{hint}</span>}
    </div>
  )
}
