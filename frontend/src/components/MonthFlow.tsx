import { useState } from 'react'

import {
  daysInMonth,
  effectiveDueDay,
  euro,
  type PlanPosition,
} from '@/lib/domain'

/**
 * Der Geldverlauf über den Monat — wann kommt was rein, wann geht was raus.
 *
 * Die Frage dahinter: reicht das, was am Monatsanfang da ist? Eine Miete am 1.
 * und ein Gehalt am 28. gehen in der Summe auf, im Verlauf aber nicht.
 *
 * **Bewusst ohne Kontostand.** Die Kurve startet bei null und zeigt die
 * Veränderung, nicht den Stand. Der Tiefpunkt ist dann genau der Betrag, der
 * am 1. dasein muss — und den kann man ausrechnen, ohne dass jemand monatlich
 * einen Saldo pflegt.
 *
 * Stufen statt Linie: Geld bewegt sich in Sprüngen. Eine weiche Kurve würde
 * Zwischenwerte behaupten, die es nicht gibt.
 */

const HEIGHT = 160
const WIDTH = 720
const PAD = { top: 16, right: 12, bottom: 24, left: 12 }

type Props = {
  positions: PlanPosition[]
  year: number
  month: number
}

type DayStep = {
  day: number
  /** Saldo **nach** allen Bewegungen dieses Tages. */
  balance: number
  /** Summe der Bewegungen an diesem Tag — für den Tooltip. */
  change: number
  labels: string[]
}

/** Alle Bewegungen auf Tage verdichtet, kumuliert. */
function buildSteps(
  positions: PlanPosition[],
  year: number,
  month: number
): DayStep[] {
  const byDay = new Map<number, { change: number; labels: string[] }>()

  for (const position of positions) {
    const day = effectiveDueDay(position.dueDay, year, month)
    const amount = Number(position.amountPlanned)
    // Einnahmen heben den Verlauf, alles andere senkt ihn.
    const change = position.block === 'income' ? amount : -amount

    const entry = byDay.get(day) ?? { change: 0, labels: [] }
    entry.change += change
    entry.labels.push(position.label)
    byDay.set(day, entry)
  }

  let balance = 0
  return [...byDay.keys()]
    .sort((a, b) => a - b)
    .map((day) => {
      const entry = byDay.get(day)!
      balance += entry.change
      return { day, balance, change: entry.change, labels: entry.labels }
    })
}

export function MonthFlow({ positions, year, month }: Props) {
  const [hoverDay, setHoverDay] = useState<number | null>(null)

  const steps = buildSteps(positions, year, month)
  if (steps.length === 0) return null

  const lastDay = daysInMonth(year, month)
  const balances = steps.map((step) => step.balance)
  const low = Math.min(0, ...balances)
  const high = Math.max(0, ...balances)
  const lowStep = steps.find((step) => step.balance === low)

  // Ohne Spanne gäbe es eine Division durch null — etwa wenn alles auf
  // denselben Tag fällt und sich glatt aufhebt.
  const span = high - low || 1

  const x = (day: number) =>
    PAD.left + ((day - 1) / (lastDay - 1)) * (WIDTH - PAD.left - PAD.right)
  const y = (value: number) =>
    PAD.top + ((high - value) / span) * (HEIGHT - PAD.top - PAD.bottom)

  // Treppe: waagerecht bis zum Fälligkeitstag, dann senkrecht auf den neuen
  // Stand. Beginnt am 1. bei null.
  const points: [number, number][] = [[1, 0]]
  for (const step of steps) {
    points.push([step.day, points[points.length - 1][1]])
    points.push([step.day, step.balance])
  }
  points.push([lastDay, points[points.length - 1][1]])

  const path = points
    .map(([day, value], index) => `${index === 0 ? 'M' : 'L'}${x(day)},${y(value)}`)
    .join(' ')

  // Fläche unterhalb der Nulllinie — nur der negative Teil wird eingefärbt.
  const zeroY = y(0)
  const areaPath = `${path} L${x(lastDay)},${zeroY} L${x(1)},${zeroY} Z`

  const shortfall = low < 0 ? Math.abs(low) : 0
  const active = hoverDay === null ? null : steps.find((s) => s.day === hoverDay)

  return (
    <section className="bg-card border-border flex flex-col gap-3 rounded-lg border p-5">
      <header className="flex flex-col gap-1">
        <h2 className="text-muted-foreground text-[11px] font-semibold tracking-widest uppercase">
          Verlauf im Monat
        </h2>
        {/* Die Zahl steht über dem Diagramm, nicht darunter: sie ist die
            Aussage, das Diagramm ist die Begründung. */}
        {shortfall > 0 ? (
          <p className="text-lg font-medium">
            Du brauchst am 1. mindestens{' '}
            <span className="text-destructive font-mono font-semibold tabular-nums">
              {euro.format(shortfall)}
            </span>
            {lowStep && (
              <span className="text-muted-foreground text-sm font-normal">
                {' '}
                — Tiefpunkt am {lowStep.day}.
              </span>
            )}
          </p>
        ) : (
          <p className="text-lg font-medium">
            Der Monat trägt sich durchgehend selbst.
          </p>
        )}
      </header>

      <svg
        viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
        className="h-40 w-full touch-none"
        role="img"
        aria-label={
          shortfall > 0
            ? `Verlauf im Monat. Tiefpunkt am ${lowStep?.day}. mit ${euro.format(low)}. Am 1. werden mindestens ${euro.format(shortfall)} gebraucht.`
            : 'Verlauf im Monat. Der Saldo bleibt durchgehend positiv.'
        }
        onPointerMove={(event) => {
          const box = event.currentTarget.getBoundingClientRect()
          const ratio = (event.clientX - box.left) / box.width
          const day = 1 + ratio * (lastDay - 1)
          // Nächstliegender Tag mit einer Bewegung — Zwischentage tragen
          // keine Information, dort passiert nichts.
          const nearest = steps.reduce((best, step) =>
            Math.abs(step.day - day) < Math.abs(best.day - day) ? step : best
          )
          setHoverDay(nearest.day)
        }}
        onPointerLeave={() => setHoverDay(null)}
      >
        {low < 0 && (
          <>
            <defs>
              <clipPath id="below-zero">
                <rect
                  x={0}
                  y={zeroY}
                  width={WIDTH}
                  height={Math.max(HEIGHT - zeroY, 0)}
                />
              </clipPath>
            </defs>
            <path
              d={areaPath}
              className="fill-destructive/15"
              clipPath="url(#below-zero)"
            />
          </>
        )}

        {/* Nulllinie zurückhaltend, aber sichtbar — sie ist der Bezugspunkt. */}
        <line
          x1={PAD.left}
          x2={WIDTH - PAD.right}
          y1={zeroY}
          y2={zeroY}
          className="stroke-border"
          strokeWidth={1}
        />

        <path
          d={path}
          fill="none"
          className="stroke-foreground"
          strokeWidth={2}
          strokeLinejoin="round"
        />

        {steps.map((step) => (
          <circle
            key={step.day}
            cx={x(step.day)}
            cy={y(step.balance)}
            r={hoverDay === step.day ? 5 : 3.5}
            className={
              step.balance < 0
                ? 'fill-destructive stroke-card'
                : 'fill-foreground stroke-card'
            }
            strokeWidth={2}
          />
        ))}

        {active && (
          <line
            x1={x(active.day)}
            x2={x(active.day)}
            y1={PAD.top}
            y2={HEIGHT - PAD.bottom}
            className="stroke-border"
            strokeWidth={1}
            strokeDasharray="3 3"
          />
        )}

        <text
          x={PAD.left}
          y={HEIGHT - 6}
          className="fill-muted-foreground text-[11px]"
        >
          1.
        </text>
        <text
          x={WIDTH - PAD.right}
          y={HEIGHT - 6}
          textAnchor="end"
          className="fill-muted-foreground text-[11px]"
        >
          {lastDay}.
        </text>
      </svg>

      {/* Statt eines schwebenden Kastens eine feste Zeile — sie springt nicht
          und verdeckt die Kurve nicht. */}
      <p className="text-muted-foreground min-h-5 text-xs">
        {active ? (
          <>
            <span className="text-foreground font-medium">{active.day}.</span>{' '}
            {active.labels.join(', ')} ·{' '}
            <span className="font-mono tabular-nums">
              {active.change > 0 ? '+' : ''}
              {euro.format(active.change)}
            </span>{' '}
            · Stand{' '}
            <span
              className={`font-mono tabular-nums ${active.balance < 0 ? 'text-destructive' : ''}`}
            >
              {euro.format(active.balance)}
            </span>
          </>
        ) : (
          'Fahr über die Kurve für die Bewegungen eines Tages.'
        )}
      </p>
    </section>
  )
}
