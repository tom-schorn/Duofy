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
 * Was an jedem Tag hoch und was runter ging, mit dem verfügbaren Saldo darüber.
 *
 * Balken und Linie liegen in **einem** Bild auf **einer** Euro-Skala:
 *
 * * Balken — die Bewegung des Tages. Einnahmen nach oben, Ausgaben nach unten
 *   und dort gestapelt nach Bedarf, Wünschen und Weggelegtem.
 * * Linie — der verfügbare Saldo, fortgeschrieben über den Monat.
 *
 * Das ist **keine** zweite Achse. Beide Reihen sind Euro und teilen dieselbe
 * Nulllinie; die Linie ist die laufende Summe der Balken. Genau deshalb darf man
 * sie übereinanderlegen — bei zwei Achsen hinge die Form der Linie an einer
 * gewählten Skalierung und wäre bedeutungslos.
 *
 * ## Warum Recharts
 *
 * Vorher war das hier von Hand gezeichnetes SVG. Achsen, Beschriftung, Tooltip
 * und Größenanpassung mussten dabei jedes Mal neu gebaut werden — und ein
 * Vorzeichenfehler in der Balkenhöhe hat dafür gesorgt, dass die Ausgaben eine
 * Fassung lang gar nicht erschienen. Recharts steckt unter shadcns
 * `ChartContainer`, der die Farben aus `ChartConfig` als CSS-Variablen setzt.
 * Damit erben die Diagramme Duofys Tokens von allein, auch beim Themenwechsel.
 *
 * ## Warum der **verfügbare** Saldo
 *
 * Nur so geht die Rechnung auf. Eine Umbuchung aufs Tagesgeld ist im
 * Gesamtstand neutral, erscheint aber als Ausgabenbalken — die Linie liefe
 * auseinander. Im verfügbaren Topf verlässt das Geld den Topf und die Linie
 * sinkt um genau den Balken, den sie erzeugt.
 *
 * Das ist zugleich die Zahl, die im Alltag zählt: was noch greifbar ist.
 *
 * ## Warum immer der 1. bis Monatsende
 *
 * Die Achse zeigt den ganzen Monat, auch die Tage ohne Bewegung. Sonst läge sie
 * nicht auf dem Kalender: zwischen dem 3. und dem 28. wäre derselbe Abstand wie
 * zwischen dem 3. und dem 4., und ein Monat mit drei Buchungen sähe voll aus.
 *
 * ## Warum kalendarisch
 *
 * Das Buch ordnet eine Buchung dem Monat ihres Postens zu — ALG1 vom 30. Juli
 * steht im August. Für eine Zeitachse wäre das falsch, sie muss chronologisch
 * sein und zum Kontostand passen. Dass die August-Einnahmen im Juli kamen,
 * beantwortet der Plan, nicht das Buch.
 */

type Props = {
  year: number
  month: number
  /** Wessen Buch: das eigene, das einer Person oder das des Haushalts. */
  scope?: BookScope
}

/**
 * Reihen und Farben in einem. `ChartContainer` schreibt daraus
 * `--color-<key>`, deshalb steht in den Balken `var(--color-needs)` und nicht
 * die Farbe selbst — Duofys Tokens bleiben die einzige Quelle.
 */
const CONFIG = {
  income: { label: 'Einnahmen', color: 'var(--chart-3)' },
  needs: { label: 'Bedarf', color: 'var(--chart-1)' },
  wants: { label: 'Wünsche', color: 'var(--chart-2)' },
  savings: { label: 'Weggelegt', color: 'var(--chart-4)' },
  saldo: { label: 'Verfügbarer Saldo', color: 'var(--foreground)' },
} satisfies ChartConfig

/** Die Ausgabenblöcke von der Nulllinie nach unten, in fester Reihenfolge. */
const SPENDING = ['needs', 'wants', 'savings'] as const

type Row = {
  tag: number
  income: number
  /** Negativ gehalten: Recharts stapelt Positives nach oben, Negatives nach unten. */
  needs: number
  wants: number
  savings: number
  saldo: number
}

export function BookFlow({ year, month, scope = OWN_SCOPE }: Props) {
  // onlyAvailable: siehe oben — ohne das läuft die Linie von den Balken weg.
  const history = useBalanceHistory(year, month, scope, true)

  return (
    <QueryState isPending={history.isPending} error={history.error} rows={2}>
      {history.data && <Chart data={history.data} year={year} month={month} />}
    </QueryState>
  )
}

/**
 * Eine Zeile je Tag des Monats — auch ohne Bewegung.
 *
 * Der Saldo wird fortgeschrieben, die Bewegung bleibt null. Ohne diese Tage
 * läge die x-Achse nicht auf dem Kalender.
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

/** Kurz für die Achse: 2.000 statt 2.000,00 €. */
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
                // Ausgaben stehen negativ in den Daten, damit sie nach unten
                // stapeln. Im Tooltip will man den Betrag, nicht das Vorzeichen.
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
