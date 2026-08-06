import {
  Bar,
  CartesianGrid,
  ComposedChart,
  Line,
  ReferenceLine,
  XAxis,
  YAxis,
} from 'recharts'

import { Card } from '@/components/ui/card'
import { QueryState } from '@/components/QueryState'
import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from '@/components/ui/chart'
import {
  daysInMonth,
  euro,
  OWN_SCOPE,
  type BalanceHistory,
  type BookScope,
} from '@/lib/domain'
import { useBalanceHistory } from '@/lib/queries'

/**
 * What went up and down on each day, with the spendable balance on top.
 *
 * Bars and line share **one** picture on **one** euro scale:
 *
 * * bars — the movement of the day. Income upwards, expenses downwards and stacked
 *   there by needs, wants and money put aside.
 * * line — the spendable balance, carried forward across the month.
 *
 * This is **not** a second axis. Both series are euros and share the same zero line;
 * the line is the running total of the bars. That is precisely why they may be laid
 * over each other — with two axes the shape of the line would depend on a chosen
 * scaling and would mean nothing.
 *
 * ## Why Recharts
 *
 * This used to be hand-drawn SVG. Axes, labels, tooltip and resizing had to be
 * rebuilt every time — and a sign error in the bar height meant the expenses did
 * not appear at all for one revision. Recharts sits underneath shadcn
 * `ChartContainer`, which turns the colours from `ChartConfig` into CSS variables.
 * The charts therefore inherit the design tokens by themselves, theme switch
 * included.
 *
 * ## Why the **spendable** balance
 *
 * It is the only way the arithmetic adds up. A transfer to savings is neutral in
 * the overall balance but shows up as an expense bar — the line would drift away
 * from the bars. In the spendable pot the money leaves the pot and the line drops
 * by exactly the bar it produces.
 *
 * It is also the number that matters day to day: what is still within reach.
 *
 * ## Why always the 1st to the end of the month
 *
 * The axis shows the whole month, including the days without movement. Otherwise it
 * would not line up with the calendar: the gap between the 3rd and the 28th would
 * equal the gap between the 3rd and the 4th, and a month with three bookings would
 * look full.
 *
 * ## Why by calendar date
 *
 * The book assigns a booking to the month of its position — income received on 30
 * July belongs to August. For a time axis that would be wrong: it has to be
 * chronological and match the account balance. That the August income arrived in
 * July is a question the plan answers, not the book.
 */

type Props = {
  year: number
  month: number
  /** Whose book: your own, one person, or the household. */
  scope?: BookScope
}

/**
 * Series and colours in one. `ChartContainer` turns this into `--color-<key>`, which
 * is why the bars reference `var(--color-needs)` rather than the colour itself — the
 * design tokens stay the single source.
 */
const CONFIG = {
  income: { label: 'Einnahmen', color: 'var(--chart-3)' },
  needs: { label: 'Bedarf', color: 'var(--chart-1)' },
  wants: { label: 'Wünsche', color: 'var(--chart-2)' },
  savings: { label: 'Weggelegt', color: 'var(--chart-4)' },
  saldo: { label: 'Verfügbarer Saldo', color: 'var(--foreground)' },
} satisfies ChartConfig

/** The expense blocks from the zero line downwards, in a fixed order. */
const SPENDING = ['needs', 'wants', 'savings'] as const

type Row = {
  tag: number
  income: number
  /** Kept negative: Recharts stacks positive upwards and negative downwards. */
  needs: number
  wants: number
  savings: number
  saldo: number
}

export function BookFlow({ year, month, scope = OWN_SCOPE }: Props) {
  // onlyAvailable: see above — without it the line drifts away from the bars.
  const history = useBalanceHistory(year, month, scope, true)

  return (
    <QueryState isPending={history.isPending} error={history.error} rows={2}>
      {history.data && <Chart data={history.data} year={year} month={month} />}
    </QueryState>
  )
}

/**
 * One row per day of the month, movement or not.
 *
 * The balance is carried forward, the movement stays zero. Without those days the
 * x-axis would not line up with the calendar.
 */
function buildRows(data: BalanceHistory, year: number, month: number): Row[] {
  const byDay = new Map(data.points.map((p) => [Number(p.day.slice(8, 10)), p]))
  const rows: Row[] = []
  let saldo = Number(data.openingBalance)

  for (let tag = 1; tag <= daysInMonth(year, month); tag += 1) {
    const hit = byDay.get(tag)
    if (hit) saldo = Number(hit.balance)
    rows.push({
      tag,
      income: Number(hit?.moves.income ?? 0),
      needs: -Number(hit?.moves.needs ?? 0),
      wants: -Number(hit?.moves.wants ?? 0),
      savings: -Number(hit?.moves.savings ?? 0),
      saldo,
    })
  }
  return rows
}

/** Short form for the axis: 2,000 rather than 2,000.00. */
const kompakt = new Intl.NumberFormat('de-DE', { maximumFractionDigits: 0 })

function Chart({
  data,
  year,
  month,
}: {
  data: BalanceHistory
  year: number
  month: number
}) {
  const rows = buildRows(data, year, month)
  const closing = Number(data.closingBalance)

  const einSumme = rows.reduce((s, r) => s + r.income, 0)
  const ausSumme = rows.reduce(
    (s, r) => s - (r.needs + r.wants + r.savings),
    0
  )

  return (
    <Card className="gap-4 px-5 py-5">
      <header className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
        <span
          className={`font-mono text-2xl font-semibold tabular-nums ${closing < 0 ? 'text-destructive' : ''}`}
        >
          {euro.format(closing)}
        </span>
        <span className="text-muted-foreground text-sm">
          verfügbar · {euro.format(einSumme)} herein, {euro.format(ausSumme)}{' '}
          hinaus
        </span>
      </header>

      <ChartContainer config={CONFIG} className="h-72 w-full">
        <ComposedChart data={rows} margin={{ left: 4, right: 4, top: 8 }}>
          <CartesianGrid vertical={false} />
          <XAxis
            dataKey="tag"
            tickLine={false}
            axisLine={false}
            tickMargin={8}
            interval={4}
            tickFormatter={(t) => `${t}.`}
          />
          <YAxis
            tickLine={false}
            axisLine={false}
            width={52}
            tickFormatter={(v) => kompakt.format(v)}
          />
          {/* Die Null ist der Bezugspunkt der Balken — sie muss sichtbar sein,
              auch wenn das Gitter sie zufällig nicht trifft. */}
          <ReferenceLine y={0} className="stroke-border" strokeWidth={1} />

          <ChartTooltip
            content={
              <ChartTooltipContent
                labelFormatter={(t) => `${t}. ${MONAT[month - 1]}`}
                // Expenses are negative in the data so they stack downwards. The
                // tooltip wants the amount, not the sign.
                formatter={(value, name) => (
                  <span className="flex w-full justify-between gap-3">
                    <span>{CONFIG[name as keyof typeof CONFIG].label}</span>
                    <span className="font-mono tabular-nums">
                      {euro.format(Math.abs(Number(value)))}
                    </span>
                  </span>
                )}
              />
            }
          />

          <Bar dataKey="income" stackId="tag" fill="var(--color-income)" />
          {SPENDING.map((key) => (
            <Bar
              key={key}
              dataKey={key}
              stackId="tag"
              fill={`var(--color-${key})`}
            />
          ))}

          <Line
            type="stepAfter"
            dataKey="saldo"
            stroke="var(--color-saldo)"
            strokeWidth={2}
            dot={false}
            activeDot={{ r: 4 }}
          />

          <ChartLegend content={<ChartLegendContent />} />
        </ComposedChart>
      </ChartContainer>
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
