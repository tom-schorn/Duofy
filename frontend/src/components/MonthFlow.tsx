import {
  Area,
  AreaChart,
  CartesianGrid,
  ReferenceDot,
  ReferenceLine,
  XAxis,
  YAxis,
} from 'recharts'

import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from '@/components/ui/chart'
import { Card } from '@/components/ui/card'
import {
  Empty,
  EmptyDescription,
  EmptyHeader,
} from '@/components/ui/empty'
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
 * The planned flow of money across the month — when does what come in and go out.
 *
 * The question behind it: is what is there at the start of the month enough? Rent
 * on the 1st and salary on the 28th add up in the total, but not in the flow.
 *
 * **Deliberately without an account balance.** The curve starts at zero and shows
 * the change, not the balance. The low point is then exactly the amount that has to
 * be there on the 1st — without anyone maintaining a running balance. The real
 * balance history belongs in the book, not in the planning.
 *
 * It follows that this curve and the one in the book **cannot** coincide: this one
 * starts at zero and uses due dates, that one starts at the real balance and uses
 * booking dates.
 *
 * Steps rather than a smooth line: money moves in jumps. A curve would claim
 * intermediate values that do not exist.
 *
 * ## Why an axis with amounts is necessary
 *
 * This used to be hand-drawn SVG without a y-axis. When most commitments fall due
 * on the 1st, the curve is a cliff at the left edge and flat afterwards. Without
 * labels that reads like a bug; with an axis and a grid one sees that the line sits
 * at 750 and not at zero.
 */

type Props = {
  positions: PlanPosition[]
  year: number
  month: number
  /** Height of the plot area. Flatter for printing — see `PlanSankey`. */
  height?: string
}

type DayStep = {
  day: number
  /** Balance **after** every movement of that day. */
  balance: number
  change: number
  labels: string[]
  /** Did money come in on balance that day? */
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

/**
 * One row per day of the month, movement or not.
 *
 * `change` then stays zero and the balance is carried forward. Without those days
 * the axis would not line up with the calendar.
 */
function buildDays(steps: DayStep[], lastDay: number) {
  const byDay = new Map(steps.map((s) => [s.day, s]))
  const out: { day: number; saldo: number; change: number }[] = []
  let saldo = 0

  for (let day = 1; day <= lastDay; day += 1) {
    const hit = byDay.get(day)
    if (hit) saldo = hit.balance
    out.push({ day, saldo, change: hit?.change ?? 0 })
  }
  return out
}

const CONFIG = {
  saldo: { label: 'Geplanter Saldo', color: 'var(--foreground)' },
} satisfies ChartConfig

/** Short form for the axis: 2,000 rather than 2,000.00. */
const kompakt = new Intl.NumberFormat('de-DE', { maximumFractionDigits: 0 })

export function MonthFlow({
  positions,
  year,
  month,
  height = 'h-60',
}: Props) {
  const steps = buildSteps(positions, year, month)
  const rows = buildRows(positions, year, month)

  if (steps.length === 0) {
    return (
      <Empty className="border-border rounded-xl border border-dashed">
        <EmptyHeader>
          <EmptyDescription>Noch keine Posten — sobald welche da sind, zeigt der Verlauf, wann im
        Monat es eng wird.</EmptyDescription>
        </EmptyHeader>
      </Empty>
    )
  }

  const lastDay = daysInMonth(year, month)
  const days = buildDays(steps, lastDay)
  const balances = steps.map((s) => s.balance)
  const low = Math.min(0, ...balances)
  const lowStep = steps.find((s) => s.balance === low)
  const shortfall = low < 0 ? Math.abs(low) : 0

  return (
    <Card className="gap-5 px-5 py-5">
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

      <ChartContainer config={CONFIG} className={`${height} w-full`}>
        <AreaChart data={days} margin={{ left: 4, right: 4, top: 8 }}>
          <defs>
            <linearGradient id="flow-fill" x1="0" y1="0" x2="0" y2="1">
              <stop
                offset="0%"
                stopColor="var(--color-saldo)"
                stopOpacity={0.16}
              />
              <stop
                offset="100%"
                stopColor="var(--color-saldo)"
                stopOpacity={0.02}
              />
            </linearGradient>
          </defs>

          <CartesianGrid vertical={false} />
          <XAxis
            dataKey="day"
            tickLine={false}
            axisLine={false}
            tickMargin={8}
            interval={4}
            tickFormatter={(d) => `${d}.`}
          />
          <YAxis
            tickLine={false}
            axisLine={false}
            width={52}
            tickFormatter={(v) => kompakt.format(v)}
          />
          {/* Die Null ist der Bezug: darunter fehlt Geld, das am 1. dasein muss. */}
          <ReferenceLine y={0} className="stroke-border" strokeWidth={1} />

          <ChartTooltip
            content={
              <ChartTooltipContent
                labelFormatter={(d) => `${d}. ${MONAT[month - 1]}`}
                formatter={(value) => (
                  <span className="flex w-full justify-between gap-3">
                    <span>Geplanter Saldo</span>
                    <span className="font-mono tabular-nums">
                      {euro.format(Number(value))}
                    </span>
                  </span>
                )}
              />
            }
          />

          <Area
            type="stepAfter"
            dataKey="saldo"
            stroke="var(--color-saldo)"
            strokeWidth={2}
            fill="url(#flow-fill)"
            dot={false}
            activeDot={{ r: 4 }}
          />

          {/* Rot ist reserviert — ein Tiefpunkt unter null ist genau der Fall,
              für den es gedacht ist: es fehlt Geld. */}
          {shortfall > 0 && lowStep && (
            <ReferenceDot
              x={lowStep.day}
              y={low}
              r={5}
              className="fill-destructive stroke-card"
              strokeWidth={2}
            />
          )}
        </AreaChart>
      </ChartContainer>

      {/* Auf Papier weg: `PlanPrintout` bringt die Posten auf Seite 2, und
          diese Tabelle allein wären 26 Zeilen zu viel für Seite 1. */}
      <div className="w-full overflow-x-auto print:hidden">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="w-14">Tag</TableHead>
              <TableHead>Posten</TableHead>
              <TableHead className="text-right">Betrag</TableHead>
              <TableHead className="text-right">Saldo</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((row, i) => (
              <TableRow key={`${row.day}-${row.label}-${i}`}>
                <TableCell className="text-muted-foreground tabular-nums">
                  {row.day}.
                </TableCell>
                <TableCell>{row.label}</TableCell>
                <TableCell className="font-mono text-right tabular-nums">
                  {row.amount > 0 ? '+' : '−'}
                  {euro.format(Math.abs(row.amount))}
                </TableCell>
                <TableCell
                  className={`font-mono text-right tabular-nums ${row.balance < 0 ? 'text-destructive' : ''}`}
                >
                  {euro.format(row.balance)}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      <p className="text-muted-foreground max-w-[70ch] text-xs print:hidden">
        Geplante Fälligkeiten, nicht die echten Buchungstage — der Verlauf im
        Buch sieht deshalb anders aus. Der Saldo in der Tabelle läuft über die
        Zeilen mit; innerhalb eines Tages gibt es aber keine Reihenfolge, das
        Diagramm zeigt deshalb den Tagesabschluss.
      </p>
    </Card>
  )
}

const MONAT = [
  'Januar',
  'Februar',
  'März',
  'April',
  'Mai',
  'Juni',
  'Juli',
  'August',
  'September',
  'Oktober',
  'November',
  'Dezember',
]
