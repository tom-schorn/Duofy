import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
} from '@/components/ui/card'
import { euro } from '@/lib/domain'

/**
 * Eine Kennzahl als Karte.
 *
 * Eigene Datei, weil Plan und Buch dieselbe Karte benutzen, aber andere Zahlen
 * hineinstellen: der Plan zeigt Soll-Werte, das Buch Ist-Werte.
 *
 * Baut auf `Card` aus dem Theme statt auf eigenem Rahmen. Vorher stand hier ein
 * handgeschriebenes `bg-card border rounded-lg p-4` — dieselbe Absicht, aber
 * andere Rundung, anderer Rand und andere Abstände als im Rest der Oberfläche.
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
    <Card size="sm" className="gap-2">
      <CardHeader>
        <CardDescription className="text-[11px] font-semibold tracking-widest uppercase">
          {label}
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-0.5">
        <span
          className={`font-mono tabular-nums ${strong ? 'text-xl font-semibold' : 'text-lg font-medium'} ${
            tone === 'over' ? 'text-destructive' : ''
          }`}
        >
          {euro.format(value)}
        </span>
        {hint && (
          <span className="text-muted-foreground text-xs">{hint}</span>
        )}
      </CardContent>
    </Card>
  )
}
