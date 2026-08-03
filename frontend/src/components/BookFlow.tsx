import { useState } from 'react'

import { QueryState } from '@/components/QueryState'
import { daysInMonth, euro, type BalanceHistory } from '@/lib/domain'
import { useBalanceHistory } from '@/lib/queries'

/**
 * Der echte Kontostand über den Monat — das Gegenstück zum Verlauf im Plan.
 *
 * Der Plan-Verlauf startet bei null und zeigt die geplante Veränderung. Diese
 * Kurve zeigt den **Stand**: was am jeweiligen Tag wirklich da war, über alle
 * Konten zusammen.
 *
 * **Nach Datum, nicht nach Posten.** Das Buch ordnet eine Buchung dem Monat
 * ihres Postens zu — ALG1 für August steht im August, obwohl es am 30. Juli
 * kam. Für eine Kontostandkurve wäre das falsch: das Geld lag im Juli auf dem
 * Konto. Deshalb liefert `/accounts/history` streng nach Kalendertag.
 *
 * **Umbuchungen fehlen bewusst.** Geld vom Giro aufs Tagesgeld zu legen ändert
 * den Gesamtstand nicht. In der Summe wäre es eine Stufe, die nichts bedeutet.
 *
 * Technik wie beim Plan-Verlauf: `viewBox` 0–100 mit
 * `preserveAspectRatio="none"`, damit SVG-Koordinaten und CSS-Prozente
 * dieselbe Zahl sind. Marker liegen als HTML darüber, Striche halten über
 * `vector-effect="non-scaling-stroke"` ihre Breite.
 */

type Props = {
  year: number
  month: number
  /** Fremder Besitzer für die Personenansicht. null = eigenes Buch. */
  ownerId?: string | null
}

type DayStep = {
  day: number
  balance: number
  /** Was an diesem Tag bewegt wurde. 0 = nur fortgeschrieben. */
  change: number
}

/** Luft über und unter der Kurve, damit sie den Rand nicht berührt. */
const PADDING = 0.12

export function BookFlow({ year, month, ownerId = null }: Props) {
  const history = useBalanceHistory(year, month, ownerId)

  return (
    <QueryState isPending={history.isPending} error={history.error} rows={2}>
      {history.data && (
        <Chart data={history.data} year={year} month={month} />
      )}
    </QueryState>
  )
}

/**
 * Ein Punkt je Tag des Monats.
 *
 * Auch die Tage ohne Bewegung: der Stand gilt weiter, und ohne sie läge die
 * x-Achse nicht auf dem Kalender — zwischen dem 3. und dem 28. wäre derselbe
 * Abstand wie zwischen dem 3. und dem 4.
 */
function buildSteps(data: BalanceHistory, year: number, month: number): DayStep[] {
  const byDay = new Map(
    data.points.map((point) => [
      Number(point.day.slice(8, 10)),
      { balance: Number(point.balance), change: Number(point.change) },
    ])
  )

  const steps: DayStep[] = []
  let running = Number(data.openingBalance)

  for (let day = 1; day <= daysInMonth(year, month); day += 1) {
    const hit = byDay.get(day)
    if (hit) running = hit.balance
    steps.push({ day, balance: running, change: hit?.change ?? 0 })
  }
  return steps
}

function Chart({
  data,
  year,
  month,
}: {
  data: BalanceHistory
  year: number
  month: number
}) {
  const [hoverDay, setHoverDay] = useState<number | null>(null)

  const steps = buildSteps(data, year, month)
  const days = steps.length

  const values = [Number(data.openingBalance), ...steps.map((s) => s.balance)]
  const high = Math.max(...values)
  const low = Math.min(...values)
  // Bei einem völlig flachen Monat wäre die Spanne 0 und jede Division kaputt.
  const span = high - low || Math.abs(high) || 1
  const top = high + span * PADDING
  const bottom = low - span * PADDING

  const x = (day: number) => ((day - 1) / Math.max(days - 1, 1)) * 100
  const y = (value: number) => ((top - value) / (top - bottom)) * 100

  // Stufen, keine Gerade: der Stand hält bis zur nächsten Bewegung.
  const line = steps
    .map((step, index) =>
      index === 0
        ? `M ${x(step.day)} ${y(step.balance)}`
        : `L ${x(step.day)} ${y(steps[index - 1].balance)} L ${x(step.day)} ${y(step.balance)}`
    )
    .join(' ')
  const area = `${line} L 100 100 L 0 100 Z`

  const moved = steps.filter((step) => step.change !== 0)
  const lowest = steps.reduce((worst, step) =>
    step.balance < worst.balance ? step : worst
  )
  const active = hoverDay === null ? null : steps[hoverDay - 1]
  const closing = Number(data.closingBalance)
  const change = closing - Number(data.openingBalance)

  // Die Nulllinie nur zeichnen, wenn sie im Bild liegt — sonst klebte sie am
  // Rand und behauptete eine Nähe zur Null, die es nicht gibt.
  const zeroVisible = bottom < 0 && top > 0

  function pickDay(event: React.PointerEvent<HTMLDivElement>) {
    const box = event.currentTarget.getBoundingClientRect()
    const share = (event.clientX - box.left) / box.width
    setHoverDay(Math.min(days, Math.max(1, Math.round(share * (days - 1)) + 1)))
  }

  return (
    <section className="bg-card border-border flex flex-col gap-6 rounded-lg border p-5">
      <header className="flex flex-col gap-1">
        <p className="text-2xl font-semibold">
          <span className={closing < 0 ? 'text-destructive' : ''}>
            {euro.format(closing)}
          </span>
          <span className="text-muted-foreground ml-2 text-base font-normal">
            über alle Konten
          </span>
        </p>
        <p className="text-muted-foreground text-sm">
          {change === 0
            ? 'Im Monat hat sich der Stand nicht verändert.'
            : `${change > 0 ? 'Plus' : 'Minus'} ${euro.format(Math.abs(change))} seit dem 1. — Tiefpunkt am ${lowest.day}. mit ${euro.format(lowest.balance)}.`}
        </p>
      </header>

      <div
        className="relative h-48 w-full touch-none select-none"
        onPointerMove={pickDay}
        onPointerLeave={() => setHoverDay(null)}
      >
        <svg
          viewBox="0 0 100 100"
          preserveAspectRatio="none"
          className="absolute inset-0 h-full w-full overflow-visible"
          role="img"
          aria-label={`Kontostand im Monat. Start ${euro.format(Number(data.openingBalance))}, Ende ${euro.format(closing)}, Tiefpunkt am ${lowest.day}. mit ${euro.format(lowest.balance)}.`}
        >
          <defs>
            <linearGradient id="book-fill" x1="0" y1="0" x2="0" y2="1">
              <stop
                offset="0%"
                className="text-chart-4"
                stopColor="currentColor"
                stopOpacity="0.18"
              />
              <stop
                offset="100%"
                className="text-chart-4"
                stopColor="currentColor"
                stopOpacity="0"
              />
            </linearGradient>
          </defs>

          <path d={area} fill="url(#book-fill)" />

          {zeroVisible && (
            <line
              x1="0"
              x2="100"
              y1={y(0)}
              y2={y(0)}
              className="stroke-destructive/50"
              strokeWidth={1}
              strokeDasharray="3 3"
              vectorEffect="non-scaling-stroke"
            />
          )}

          {active && (
            <line
              x1={x(active.day)}
              x2={x(active.day)}
              y1="0"
              y2="100"
              className="stroke-muted-foreground/40"
              strokeWidth={1}
              strokeDasharray="2 3"
              vectorEffect="non-scaling-stroke"
            />
          )}

          <path
            d={line}
            fill="none"
            className="stroke-chart-4"
            strokeWidth={2}
            strokeLinejoin="round"
            strokeLinecap="round"
            vectorEffect="non-scaling-stroke"
          />
        </svg>

        {/* Marker als HTML: im gestreckten SVG würden Kreise zu Ellipsen. */}
        {moved.map((step) => (
          <span
            key={step.day}
            style={{ left: `${x(step.day)}%`, top: `${y(step.balance)}%` }}
            className={`bg-chart-4 ring-card pointer-events-none absolute size-2 -translate-x-1/2 -translate-y-1/2 rounded-full ring-2 transition-transform ${
              hoverDay === step.day ? 'scale-150' : ''
            }`}
          />
        ))}

        {active && (
          <span
            style={{
              left: `${x(active.day)}%`,
              transform:
                x(active.day) > 70
                  ? 'translate(calc(-100% - 8px), 0)'
                  : 'translate(8px, 0)',
            }}
            className="bg-popover text-popover-foreground border-border pointer-events-none absolute top-0 flex flex-col rounded-md border px-2.5 py-1.5 text-xs shadow-sm"
          >
            <span className="text-muted-foreground">{active.day}.</span>
            <span className="font-mono font-medium tabular-nums">
              {euro.format(active.balance)}
            </span>
            {active.change !== 0 && (
              <span
                className={`font-mono tabular-nums ${
                  active.change > 0 ? 'text-chart-4' : 'text-muted-foreground'
                }`}
              >
                {active.change > 0 ? '+' : '−'}
                {euro.format(Math.abs(active.change))}
              </span>
            )}
          </span>
        )}
      </div>

      <div className="text-muted-foreground flex justify-between text-xs">
        <span>1.</span>
        <span>{Math.round(days / 2)}.</span>
        <span>{days}.</span>
      </div>
    </section>
  )
}
