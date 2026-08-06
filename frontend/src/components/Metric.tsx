import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
} from '@/components/ui/card'
import { euro } from '@/lib/domain'

/**
 * One figure as a card.
 *
 * Its own file because the plan and the book use the same card with different
 * numbers in it: the plan shows targets, the book shows actuals.
 *
 * Built on `Card` from the theme rather than on a hand-rolled frame. What stood
 * here before was `bg-card border rounded-lg p-4` — the same intent, but a
 * different radius, border and spacing than the rest of the interface.
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
