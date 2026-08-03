import { QueryState } from '@/components/QueryState'
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
 * Nulllinie; die Linie ist die laufende Summe der Balken. Genau deshalb darf
 * man sie übereinanderlegen — bei zwei Achsen hinge die Form der Linie an einer
 * gewählten Skalierung und wäre bedeutungslos.
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

/** Die drei Ausgabenblöcke in fester Reihenfolge — Farbe ist Identität. */
const BLOCKS = [
  { key: 'needs', label: 'Bedarf', fill: 'var(--chart-1)' },
  { key: 'wants', label: 'Wünsche', fill: 'var(--chart-2)' },
  { key: 'savings', label: 'Weggelegt', fill: 'var(--chart-4)' },
] as const

type Tag = {
  tag: number
  income: number
  needs: number
  wants: number
  savings: number
  /** Verfügbarer Saldo am Abend des Tages. */
  saldo: number
}

export function BookFlow({ year, month, scope = OWN_SCOPE }: Props) {
  // onlyAvailable: siehe oben — ohne das läuft die Linie von den Balken weg.
  const history = useBalanceHistory(year, month, scope, true)

  return (
    <QueryState isPending={history.isPending} error={history.error} rows={2}>
      {history.data && (
        <Chart data={history.data} year={year} month={month} />
      )}
    </QueryState>
  )
}

/**
 * Ein Eintrag je Tag des Monats — auch ohne Bewegung.
 *
 * Der Saldo wird fortgeschrieben, die Bewegung bleibt null. Ohne diese Tage
 * läge die x-Achse nicht auf dem Kalender.
 */
function buildDays(data: BalanceHistory, year: number, month: number): Tag[] {
  const byDay = new Map(
    data.points.map((p) => [Number(p.day.slice(8, 10)), p])
  )
  const out: Tag[] = []
  let saldo = Number(data.openingBalance)

  for (let tag = 1; tag <= daysInMonth(year, month); tag += 1) {
    const treffer = byDay.get(tag)
    if (treffer) saldo = Number(treffer.balance)
    out.push({
      tag,
      income: Number(treffer?.moves.income ?? 0),
      needs: Number(treffer?.moves.needs ?? 0),
      wants: Number(treffer?.moves.wants ?? 0),
      savings: Number(treffer?.moves.savings ?? 0),
      saldo,
    })
  }
  return out
}

/** Runde Schrittweiten für die Beträge links. */
const STEPS = [
  5, 10, 20, 25, 50, 100, 200, 250, 500, 1000, 2000, 2500, 5000, 10000,
]

/** Drei bis fünf runde Beschriftungen über die Spanne, immer inklusive Null. */
function ticks(low: number, high: number): number[] {
  const spanne = high - low || 1
  const step =
    STEPS.find((s) => spanne / s <= 5) ?? STEPS[STEPS.length - 1]
  const werte: number[] = []
  for (let v = Math.ceil(low / step) * step; v <= high; v += step) werte.push(v)
  if (!werte.includes(0) && low <= 0 && high >= 0) werte.push(0)
  return werte.sort((a, b) => a - b)
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
  const days = buildDays(data, year, month)
  const n = days.length
  const closing = Number(data.closingBalance)

  const raus = (d: Tag) => d.needs + d.wants + d.savings
  const einSumme = days.reduce((s, d) => s + d.income, 0)
  const ausSumme = days.reduce((s, d) => s + raus(d), 0)

  // Eine Skala für beides: Balken **und** Linie sind Euro. Die Null ist immer
  // dabei, sonst hätten die Balken keinen Bezugspunkt.
  const alle = [
    0,
    ...days.map((d) => d.income),
    ...days.map((d) => -raus(d)),
    ...days.map((d) => d.saldo),
    Number(data.openingBalance),
  ]
  const roh = { low: Math.min(...alle), high: Math.max(...alle) }
  const luft = (roh.high - roh.low || 1) * 0.08
  const low = roh.low - luft
  const high = roh.high + luft

  const x = (tag: number) => ((tag - 0.5) / n) * 100
  const y = (v: number) => ((high - v) / (high - low)) * 100
  const breite = Math.min((100 / n) * 0.62, 3)
  const nullY = y(0)

  /** Balkenhöhe als **Betrag** in viewBox-Einheiten.
   *
   *  Vorher stand hier `nullY - y(-wert)`, und das ist für Ausgaben negativ —
   *  eine negative Höhe zeichnet SVG gar nicht. Alle Ausgabenbalken fehlten
   *  deshalb komplett. Eine Länge hat kein Vorzeichen; die Richtung entscheidet
   *  allein, ob der Balken über oder unter der Nulllinie ansetzt. */
  const laenge = (wert: number) => (wert / (high - low)) * 100

  // Stufen, keine Gerade: der Saldo hält bis zur nächsten Bewegung.
  const linie = days
    .map((d, i) =>
      i === 0
        ? `M ${x(d.tag)} ${y(d.saldo)}`
        : `L ${x(d.tag)} ${y(days[i - 1].saldo)} L ${x(d.tag)} ${y(d.saldo)}`
    )
    .join(' ')

  const beschriftung = ticks(roh.low, roh.high)
  // Beschriftete Tage: erster, letzter und dazwischen alle fünf.
  const tagLabels = days
    .map((d) => d.tag)
    .filter((t) => t === 1 || t === n || t % 5 === 0)

  return (
    <section className="bg-card border-border flex flex-col gap-4 rounded-lg border p-5">
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

      {/* Die Beträge links und die Tage unten liegen als HTML über bzw. unter
          der gestreckten Zeichenfläche. Im SVG selbst würde Text mitskaliert
          und auf breiten Fenstern riesig. */}
      <div className="flex w-full gap-2">
        <div className="text-muted-foreground relative w-14 shrink-0 text-right text-[11px] tabular-nums">
          {beschriftung.map((v) => (
            <span
              key={v}
              style={{ top: `${y(v)}%` }}
              className="absolute right-0 -translate-y-1/2 whitespace-nowrap"
            >
              {v === 0 ? '0' : Math.round(v).toLocaleString('de-DE')}
            </span>
          ))}
        </div>

        <div className="min-w-0 flex-1">
          <div className="relative h-64 w-full">
            <svg
              viewBox="0 0 100 100"
              preserveAspectRatio="none"
              className="absolute inset-0 h-full w-full overflow-visible"
              role="img"
              aria-label={`Bewegung je Tag und verfügbarer Saldo. Start ${euro.format(Number(data.openingBalance))}, Ende ${euro.format(closing)}. ${euro.format(einSumme)} eingegangen, ${euro.format(ausSumme)} ausgegeben.`}
            >
              {/* Hilfslinien zu den Beträgen — zurückhaltend, nur zur Orientierung. */}
              {beschriftung.map((v) => (
                <line
                  key={v}
                  x1="0"
                  x2="100"
                  y1={y(v)}
                  y2={y(v)}
                  className={v === 0 ? 'stroke-border' : 'stroke-border/45'}
                  strokeWidth={1}
                  vectorEffect="non-scaling-stroke"
                />
              ))}

              {days.map((d) => {
                const links = x(d.tag) - breite / 2
                let unten = nullY
                return (
                  <g key={d.tag}>
                    {d.income > 0 && (
                      <rect
                        x={links}
                        y={nullY - laenge(d.income)}
                        width={breite}
                        height={laenge(d.income)}
                        className="fill-chart-3"
                      >
                        <title>{`${d.tag}. — ${euro.format(d.income)} herein`}</title>
                      </rect>
                    )}
                    {BLOCKS.map((b) => {
                      const wert = d[b.key]
                      if (wert <= 0) return null
                      const h = laenge(wert)
                      const oben = unten
                      unten += h
                      // Lücke zwischen den Segmenten: ohne sie liest man zwei
                      // gestapelte Flächen als eine.
                      return (
                        <rect
                          key={b.key}
                          x={links}
                          y={oben}
                          width={breite}
                          height={Math.max(h - 0.4, 0.3)}
                          fill={b.fill}
                        >
                          <title>{`${d.tag}. — ${b.label} ${euro.format(wert)}`}</title>
                        </rect>
                      )
                    })}
                  </g>
                )
              })}

              <path
                d={linie}
                fill="none"
                className="stroke-foreground"
                strokeWidth={2}
                strokeLinejoin="round"
                strokeLinecap="round"
                vectorEffect="non-scaling-stroke"
              />
            </svg>
          </div>

          <div className="text-muted-foreground relative mt-1.5 h-4 text-[11px] tabular-nums">
            {tagLabels.map((t) => (
              <span
                key={t}
                style={{ left: `${x(t)}%` }}
                className="absolute -translate-x-1/2"
              >
                {t}.
              </span>
            ))}
          </div>
        </div>
      </div>

      {/* Legende: bei mehr als einer Reihe immer da, Identität nie nur über
          Farbe. Einnahmen stehen zusätzlich über der Nulllinie. */}
      <ul className="text-muted-foreground flex flex-wrap gap-x-4 gap-y-1 text-xs">
        <li className="flex items-center gap-1.5">
          <span className="bg-chart-3 size-2.5 rounded-sm" />
          Einnahmen
        </li>
        {BLOCKS.map((b) => (
          <li key={b.key} className="flex items-center gap-1.5">
            <span
              className="size-2.5 rounded-sm"
              style={{ background: b.fill }}
            />
            {b.label}
          </li>
        ))}
        <li className="flex items-center gap-1.5">
          <span className="bg-foreground h-0.5 w-4 rounded-full" />
          verfügbarer Saldo
        </li>
      </ul>
    </section>
  )
}
