import { useState } from 'react'

import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
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
 * am 1. dasein muss — ohne dass jemand monatlich einen Saldo pflegt. Der echte
 * Kontoverlauf gehört ins Haushaltsbuch, nicht in die Planung.
 *
 * Stufen statt Linie: Geld bewegt sich in Sprüngen. Eine weiche Kurve würde
 * Zwischenwerte behaupten, die es nicht gibt.
 *
 * ## Warum Prozent-Koordinaten
 *
 * Die `viewBox` ist 0–100 in beiden Richtungen und wird mit
 * `preserveAspectRatio="none"` auf den Container gezogen. Dadurch sind
 * SVG-Koordinaten und CSS-Prozente **dieselbe Zahl** — Marker und
 * Beschriftungen liegen als HTML darüber und behalten ihre echte Schriftgröße.
 *
 * Die Vorgängerfassung hatte eine feste 720×160-Box: dort skalierte alles mit,
 * eine 2-px-Linie wurde auf breiten Bildschirmen zu 3,3 px und 11-px-Text zu
 * 18 px. `vector-effect="non-scaling-stroke"` hält die Striche jetzt konstant.
 */

type Props = {
  positions: PlanPosition[]
  year: number
  month: number
}

type DayStep = {
  day: number
  /** Saldo **nach** allen Bewegungen dieses Tages. */
  balance: number
  change: number
  labels: string[]
  /** Kam an diesem Tag unterm Strich Geld herein? */
  incoming: boolean
}

function buildSteps(
  positions: PlanPosition[],
  year: number,
  month: number
): DayStep[] {
  const byDay = new Map<number, { change: number; labels: string[] }>()

  for (const position of positions) {
    const day = effectiveDueDay(position.dueDay, year, month)
    const amount = Number(position.amountPlanned)
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
      return {
        day,
        balance,
        change: entry.change,
        labels: entry.labels,
        incoming: entry.change > 0,
      }
    })
}

type FlowRow = {
  day: number
  label: string
  amount: number
  balance: number
}

/**
 * Eine Zeile je Posten, mit laufendem Saldo.
 *
 * Innerhalb eines Tages gibt es keine echte Reihenfolge. Einnahmen kommen
 * deshalb zuerst — sonst zeigte die Saldo-Spalte einen Einbruch, den es nie
 * gab: Miete und Gehalt am selben Tag sähen nach Loch aus, obwohl das Geld
 * bereits da war.
 */
function buildRows(
  positions: PlanPosition[],
  year: number,
  month: number
): FlowRow[] {
  const sorted = positions
    .map((position) => ({
      day: effectiveDueDay(position.dueDay, year, month),
      label: position.label,
      amount:
        position.block === 'income'
          ? Number(position.amountPlanned)
          : -Number(position.amountPlanned),
    }))
    .sort((a, b) => a.day - b.day || b.amount - a.amount)

  let balance = 0
  return sorted.map((row) => {
    balance += row.amount
    return { ...row, balance }
  })
}

export function MonthFlow({ positions, year, month }: Props) {
  const [hoverDay, setHoverDay] = useState<number | null>(null)

  const steps = buildSteps(positions, year, month)
  const rows = buildRows(positions, year, month)
  if (steps.length === 0) {
    return (
      <p className="text-muted-foreground bg-card border-border rounded-lg border p-6 text-sm">
        Noch keine Posten — sobald welche da sind, zeigt der Verlauf, wann im
        Monat es eng wird.
      </p>
    )
  }

  const lastDay = daysInMonth(year, month)
  const balances = steps.map((step) => step.balance)
  const low = Math.min(0, ...balances)
  const high = Math.max(0, ...balances)
  const lowStep = steps.find((step) => step.balance === low)
  const span = high - low || 1

  // Etwas Luft oben und unten, damit Marker nicht am Rand kleben.
  const x = (day: number) => ((day - 1) / (lastDay - 1)) * 100
  const y = (value: number) => 6 + ((high - value) / span) * 88

  const points: [number, number][] = [[1, 0]]
  for (const step of steps) {
    points.push([step.day, points[points.length - 1][1]])
    points.push([step.day, step.balance])
  }
  points.push([lastDay, points[points.length - 1][1]])

  const line = points
    .map(([day, value], i) => `${i === 0 ? 'M' : 'L'}${x(day)},${y(value)}`)
    .join(' ')

  const zeroY = y(0)
  const area = `${line} L100,${zeroY} L0,${zeroY} Z`

  const shortfall = low < 0 ? Math.abs(low) : 0
  const active = hoverDay === null ? null : steps.find((s) => s.day === hoverDay)

  // Nur der Tiefpunkt und die Geldeingänge bekommen einen Punkt — ein Marker
  // auf jedem Ereignis wäre ein Punktefeld ohne Aussage.
  const marked = steps.filter((s) => s.incoming || s.day === lowStep?.day)

  const dayTicks = [1, 10, 20, lastDay].filter(
    (day, index, all) => all.indexOf(day) === index
  )

  function pickDay(event: React.PointerEvent<HTMLDivElement>) {
    const box = event.currentTarget.getBoundingClientRect()
    const ratio = (event.clientX - box.left) / box.width
    const day = 1 + ratio * (lastDay - 1)
    const nearest = steps.reduce((best, step) =>
      Math.abs(step.day - day) < Math.abs(best.day - day) ? step : best
    )
    setHoverDay(nearest.day)
  }

  return (
    <section className="flex flex-col gap-5">
      <header className="flex flex-col gap-1">
        {shortfall > 0 ? (
          <>
            <p className="text-2xl font-semibold">
              Du brauchst am 1. mindestens{' '}
              <span className="font-mono tabular-nums">
                {euro.format(shortfall)}
              </span>
            </p>
            <p className="text-muted-foreground text-sm">
              Tiefpunkt am {lowStep?.day}. — davor fällt mehr an, als bis dahin
              hereinkommt.
            </p>
          </>
        ) : (
          <>
            <p className="text-2xl font-semibold">
              Der Monat trägt sich durchgehend selbst.
            </p>
            <p className="text-muted-foreground text-sm">
              Zu keinem Zeitpunkt steht mehr aus, als bis dahin hereingekommen
              ist.
            </p>
          </>
        )}
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
          aria-label={
            shortfall > 0
              ? `Verlauf im Monat. Tiefpunkt am ${lowStep?.day}. mit ${euro.format(low)}. Am 1. werden mindestens ${euro.format(shortfall)} gebraucht.`
              : 'Verlauf im Monat. Der Saldo bleibt durchgehend positiv.'
          }
        >
          <defs>
            <linearGradient id="flow-plus" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" className="text-foreground" stopColor="currentColor" stopOpacity="0.14" />
              <stop offset="100%" className="text-foreground" stopColor="currentColor" stopOpacity="0" />
            </linearGradient>
            <linearGradient id="flow-minus" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" className="text-destructive" stopColor="currentColor" stopOpacity="0" />
              <stop offset="100%" className="text-destructive" stopColor="currentColor" stopOpacity="0.22" />
            </linearGradient>
            <clipPath id="flow-above">
              <rect x="0" y="0" width="100" height={zeroY} />
            </clipPath>
            <clipPath id="flow-below">
              <rect x="0" y={zeroY} width="100" height={100 - zeroY} />
            </clipPath>
          </defs>

          <path d={area} fill="url(#flow-plus)" clipPath="url(#flow-above)" />
          <path d={area} fill="url(#flow-minus)" clipPath="url(#flow-below)" />

          {/* Nulllinie: der einzige Bezugspunkt, den diese Kurve braucht. */}
          <line
            x1="0"
            x2="100"
            y1={zeroY}
            y2={zeroY}
            className="stroke-border"
            strokeWidth={1}
            vectorEffect="non-scaling-stroke"
          />

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
            className="stroke-foreground"
            strokeWidth={2}
            strokeLinejoin="round"
            strokeLinecap="round"
            vectorEffect="non-scaling-stroke"
          />
        </svg>

        {/* Marker als HTML: im gestreckten SVG würden Kreise zu Ellipsen. */}
        {marked.map((step) => {
          const isLow = step.day === lowStep?.day && low < 0
          return (
            <span
              key={step.day}
              style={{ left: `${x(step.day)}%`, top: `${y(step.balance)}%` }}
              className={`pointer-events-none absolute -translate-x-1/2 -translate-y-1/2 rounded-full ring-2 ${
                isLow
                  ? 'bg-destructive ring-card size-2.5'
                  : 'bg-chart-4 ring-card size-2'
              } ${hoverDay === step.day ? 'scale-125' : ''} transition-transform`}
            />
          )
        })}

        {/* Direkte Beschriftung am Tiefpunkt — die wichtigste Stelle soll man
            ohne Hover sehen. Nach links gekippt, wenn sie sonst rausläuft. */}
        {lowStep && low < 0 && (
          <span
            style={{
              left: `${x(lowStep.day)}%`,
              top: `${y(lowStep.balance)}%`,
              transform:
                x(lowStep.day) > 70
                  ? 'translate(calc(-100% - 10px), 6px)'
                  : 'translate(10px, 6px)',
            }}
            className="text-destructive pointer-events-none absolute font-mono text-xs font-semibold tabular-nums"
          >
            {euro.format(low)}
          </span>
        )}
      </div>

      <div className="text-muted-foreground relative h-4 text-[11px]">
        {dayTicks.map((day) => (
          <span
            key={day}
            style={{ left: `${x(day)}%` }}
            className={`absolute ${day === 1 ? '' : day === lastDay ? '-translate-x-full' : '-translate-x-1/2'}`}
          >
            {day}.
          </span>
        ))}
      </div>

      {/* Die Tabelle ist zugleich die Textfassung des Diagramms — für
          Screenreader und zum Nachrechnen. Zeile und Kurve zeigen beim
          Überfahren denselben Tag. */}
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="w-12">Tag</TableHead>
            <TableHead>Posten</TableHead>
            <TableHead className="text-right">Betrag</TableHead>
            <TableHead className="text-right">Saldo</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.map((row, index) => (
            <TableRow
              key={`${row.day}-${row.label}-${index}`}
              onPointerEnter={() => setHoverDay(row.day)}
              onPointerLeave={() => setHoverDay(null)}
              // Nur der Saldo entscheidet über die Markierung, nicht der
              // Betrag: eine Ausgabe ist normal, ein negativer Stand nicht.
              className={row.balance < 0 ? 'bg-destructive/5' : undefined}
            >
              <TableCell className="text-muted-foreground tabular-nums">
                {row.day}.
              </TableCell>
              <TableCell className="font-medium">{row.label}</TableCell>
              <TableCell className="text-right font-mono tabular-nums">
                {row.amount > 0 ? '+' : ''}
                {euro.format(row.amount)}
              </TableCell>
              <TableCell
                className={`text-right font-mono font-medium tabular-nums ${
                  row.balance < 0 ? 'text-destructive' : ''
                }`}
              >
                {euro.format(row.balance)}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>

      {/* Feste Zeile statt schwebendem Kasten — sie springt nicht und verdeckt
          die Kurve nicht. */}
      <p className="text-muted-foreground border-border min-h-5 border-t pt-3 text-xs">
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
