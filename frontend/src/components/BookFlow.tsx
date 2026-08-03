import { QueryState } from '@/components/QueryState'
import {
  euro,
  OWN_SCOPE,
  type BalanceHistory,
  type BalancePoint,
  type BookScope,
} from '@/lib/domain'
import { useBalanceHistory } from '@/lib/queries'

/**
 * Was an jedem Tag hoch und was runter ging — das Diagramm des Buchs.
 *
 * Zwei Felder, eine gemeinsame Datumsachse:
 *
 * * **oben** der verfügbare Saldo als Stufenlinie
 * * **unten** die Bewegung des Tages, Einnahmen nach oben, Ausgaben nach unten
 *   und dort nach Block gestapelt
 *
 * ## Warum zwei Felder und keine zweite Achse
 *
 * Auf einer gemeinsamen Euro-Skala von −1.600 bis 3.200 wäre ein Tag mit 3,14 €
 * ein Balken unter einem Pixel. Eine zweite Y-Achse wäre der andere Ausweg und
 * der schlechtere: sie lässt zwei Kurven vergleichbar aussehen, die es nicht
 * sind, weil die Form der einen allein von der gewählten Skalierung abhängt.
 *
 * ## Warum der **verfügbare** Saldo
 *
 * Sonst gehen Linie und Balken nicht auf. Eine Umbuchung aufs Tagesgeld ist im
 * Gesamtstand neutral, erscheint aber als Ausgabenbalken — am Monatsende fehlte
 * die Differenz. Im verfügbaren Topf verlässt sie den Topf und senkt die Linie
 * um genau den Balken, den sie erzeugt.
 *
 * Das ist zugleich die Zahl, die im Alltag zählt: was noch greifbar ist.
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

/** Feldhöhen in Pixeln. Nur x skaliert mit dem Container, y bleibt fest. */
const SALDO_H = 84
const BARS_H = 150
const AXIS_H = 10
const GAP = 14

export function BookFlow({ year, month, scope = OWN_SCOPE }: Props) {
  // onlyAvailable: siehe oben — ohne das gehen Linie und Balken nicht auf.
  const history = useBalanceHistory(year, month, scope, true)

  return (
    <QueryState isPending={history.isPending} error={history.error} rows={2}>
      {history.data && <Chart data={history.data} />}
    </QueryState>
  )
}

function raus(point: BalancePoint): number {
  return BLOCKS.reduce((sum, b) => sum + Number(point.moves[b.key]), 0)
}

function Chart({ data }: { data: BalanceHistory }) {
  const points = data.points
  const opening = Number(data.openingBalance)
  const closing = Number(data.closingBalance)

  if (points.length === 0) {
    return (
      <section className="bg-card border-border flex flex-col gap-1 rounded-lg border p-5">
        <p className="font-mono text-2xl font-semibold tabular-nums">
          {euro.format(closing)}
        </p>
        <p className="text-muted-foreground text-sm">
          Verfügbar. In diesem Monat ist noch nichts gebucht — sobald etwas
          fließt, steht hier der Verlauf.
        </p>
      </section>
    )
  }

  const H = SALDO_H + GAP + BARS_H + AXIS_H
  const rein = points.map((p) => Number(p.moves.income))
  const ab = points.map(raus)
  const groesste = Math.max(...rein, ...ab, 1)

  // Die Nulllinie liegt dort, wo das Verhältnis von rein zu raus es verlangt —
  // sonst verschenkt ein Monat ohne Einnahmen die halbe Fläche.
  const obenAnteil =
    Math.max(...rein) / (Math.max(...rein) + Math.max(...ab) || 1)
  const barsOben = SALDO_H + GAP
  const nullY = barsOben + BARS_H * obenAnteil
  const hoehe = (wert: number, auf: boolean) =>
    (wert / groesste) * (auf ? nullY - barsOben : barsOben + BARS_H - nullY)

  const salden = [opening, ...points.map((p) => Number(p.balance))]
  const hoch = Math.max(...salden)
  const tief = Math.min(...salden)
  const spanne = hoch - tief || Math.abs(hoch) || 1
  const saldoY = (v: number) => 8 + ((hoch - v) / spanne) * (SALDO_H - 18)

  const spalten = points.length
  const mitte = (i: number) => ((i + 0.5) / spalten) * 100
  const breite = Math.min(56 / spalten, 5)

  // Stufen, keine Gerade: der Stand hält bis zur nächsten Bewegung.
  const stufe = points
    .map((p, i) =>
      i === 0
        ? `M ${mitte(0)} ${saldoY(opening)} L ${mitte(0)} ${saldoY(Number(p.balance))}`
        : `L ${mitte(i)} ${saldoY(Number(points[i - 1].balance))} L ${mitte(i)} ${saldoY(Number(p.balance))}`
    )
    .join(' ')

  const einSumme = rein.reduce((s, v) => s + v, 0)
  const ausSumme = ab.reduce((s, v) => s + v, 0)
  const tag = (iso: string) => Number(iso.slice(8, 10))

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

      <div className="w-full overflow-x-auto">
        <svg
          viewBox={`0 0 100 ${H}`}
          preserveAspectRatio="none"
          className="h-[330px] w-full min-w-[32rem]"
          role="img"
          aria-label={`Verlauf im Monat. Start ${euro.format(opening)}, Ende ${euro.format(closing)}. ${euro.format(einSumme)} eingegangen, ${euro.format(ausSumme)} ausgegeben.`}
        >
          <path
            d={`${stufe} L ${mitte(spalten - 1)} ${SALDO_H} L ${mitte(0)} ${SALDO_H} Z`}
            className="fill-foreground/10"
          />
          <path
            d={stufe}
            fill="none"
            className="stroke-foreground"
            strokeWidth={2}
            strokeLinejoin="round"
            vectorEffect="non-scaling-stroke"
          />

          {/* Nulllinie der Balken — der einzige Bezugspunkt, den sie brauchen. */}
          <line
            x1="0"
            x2="100"
            y1={nullY}
            y2={nullY}
            className="stroke-border"
            strokeWidth={1}
            vectorEffect="non-scaling-stroke"
          />

          {points.map((p, i) => {
            const x = mitte(i) - breite / 2
            const ein = Number(p.moves.income)
            let y = nullY
            return (
              <g key={p.day}>
                {ein > 0 && (
                  <rect
                    x={x}
                    y={nullY - hoehe(ein, true)}
                    width={breite}
                    height={hoehe(ein, true)}
                    className="fill-chart-3"
                  >
                    <title>{`${tag(p.day)}. — ${euro.format(ein)} herein`}</title>
                  </rect>
                )}
                {BLOCKS.map((b) => {
                  const wert = Number(p.moves[b.key])
                  if (wert <= 0) return null
                  const h = hoehe(wert, false)
                  const oben = y
                  y += h
                  // Lücke zwischen den Segmenten: ohne sie liest man zwei
                  // gestapelte Flächen als eine.
                  return (
                    <rect
                      key={b.key}
                      x={x}
                      y={oben}
                      width={breite}
                      height={Math.max(h - 1, 0.5)}
                      fill={b.fill}
                    >
                      <title>{`${tag(p.day)}. — ${b.label} ${euro.format(wert)}`}</title>
                    </rect>
                  )
                })}
              </g>
            )
          })}
        </svg>
      </div>

      <div className="text-muted-foreground flex justify-between text-xs tabular-nums">
        <span>{tag(points[0].day)}.</span>
        <span>{tag(points[points.length - 1].day)}.</span>
      </div>

      {/* Legende: bei mehr als einer Serie immer da, Identität nie nur über
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
          <span className="bg-foreground size-2.5 rounded-sm" />
          verfügbarer Saldo
        </li>
      </ul>
    </section>
  )
}
