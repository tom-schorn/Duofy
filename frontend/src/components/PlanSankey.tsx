import { useState } from 'react'
import { Layer, Rectangle, Sankey, Tooltip } from 'recharts'

import { Button } from '@/components/ui/button'
import { ChartContainer, type ChartConfig } from '@/components/ui/chart'
import { euro, type PlanPosition } from '@/lib/domain'

/**
 * Wohin das Budget geht — von links nach rechts gelesen.
 *
 * Einnahmen → Budget → die drei Blöcke → einzelne Posten. Kein Verlauf, sondern
 * eine Bilanz: das Diagramm beantwortet „wohin geht das Geld", nicht „trägt der
 * Monat zeitlich". Für das Zeitliche gibt es das Buch.
 *
 * Das **Budget in der Mitte ist die Engstelle**, und genau dort sitzt in Duofy
 * der Puffer. Ein durchlaufender Posten geht sichtbar daran vorbei: er kommt in
 * keiner der Spalten vor, weil er nie Budget war.
 *
 * ## Warum gebündelt wird
 *
 * 25 Posten wären 25 Bänder, und ein Band über 2,99 € wäre dünner als seine
 * Beschriftung. Einzeln steht deshalb nur, was mindestens `EIGENES_BAND` des
 * Budgets ausmacht; der Rest läuft je Block als ein Band zusammen. Die Grenze
 * ist relativ, damit sie in jedem Haushalt trägt.
 *
 * ## Was das Diagramm sichtbar macht
 *
 * Vor allem, was **nicht** verteilt ist. „Noch nicht verplant" ist ein Band wie
 * jedes andere und geht ins Leere — in den Kennzahlen steht dieselbe Zahl als
 * „Verplanbar", aber dort sieht man ihre Größe nicht.
 */

type Props = {
  positions: PlanPosition[]
  /** Einnahmen minus Puffer — kommt vom Server. */
  budget: string
}

/** Ab welchem Anteil am Budget ein Posten ein eigenes Band bekommt. */
const EIGENES_BAND = 0.02

const BLOCKS = [
  { key: 'needs', label: 'Bedarf', color: 'var(--chart-1)' },
  { key: 'wants', label: 'Wünsche', color: 'var(--chart-2)' },
  { key: 'savings', label: 'Sparen', color: 'var(--chart-4)' },
] as const

const CONFIG = {
  income: { label: 'Einnahmen', color: 'var(--chart-3)' },
  needs: { label: 'Bedarf', color: 'var(--chart-1)' },
  wants: { label: 'Wünsche', color: 'var(--chart-2)' },
  savings: { label: 'Sparen', color: 'var(--chart-4)' },
} satisfies ChartConfig

type Knoten = {
  name: string
  betrag: number
  farbe: string
  /** 0 Einnahmen · 1 Budget · 2 Block · 3 Posten. Bestimmt die Textseite. */
  spalte: number
}

type Kante = { source: number; target: number; value: number; farbe: string }

/**
 * Baut Knoten und Kanten aus dem Plan.
 *
 * Recharts erwartet Kanten als **Indizes** in das Knotenfeld, deshalb wird hier
 * mitgezählt statt mit Namen verknüpft. Bänder mit Wert 0 fallen heraus — sie
 * würden die Aufteilung verzerren und wären ohnehin unsichtbar.
 */
function build(positions: PlanPosition[], budget: number) {
  const zaehlend = positions.filter((p) => !p.passThrough)
  const grenze = budget * EIGENES_BAND

  const nodes: Knoten[] = []
  const links: Kante[] = []
  const add = (k: Knoten) => nodes.push(k) - 1

  const einnahmen = zaehlend
    .filter((p) => p.block === 'income')
    .map((p) => ({ label: p.label, betrag: Number(p.amountPlanned) }))
    .filter((p) => p.betrag > 0)
    .sort((a, b) => b.betrag - a.betrag)

  const budgetIndex = add({
    name: 'Budget',
    betrag: budget,
    farbe: 'var(--muted-foreground)',
    spalte: 1,
  })

  for (const e of einnahmen) {
    const i = add({
      name: e.label,
      betrag: e.betrag,
      farbe: 'var(--chart-3)',
      spalte: 0,
    })
    links.push({
      source: i,
      target: budgetIndex,
      value: e.betrag,
      farbe: 'var(--chart-3)',
    })
  }

  let verteilt = 0
  for (const b of BLOCKS) {
    const posten = zaehlend
      .filter((p) => p.block === b.key)
      .map((p) => ({ label: p.label, betrag: Number(p.amountPlanned) }))
      .filter((p) => p.betrag > 0)
      .sort((a, b2) => b2.betrag - a.betrag)
    const summe = posten.reduce((s, p) => s + p.betrag, 0)
    if (summe <= 0) continue
    verteilt += summe

    const blockIndex = add({
      name: b.label,
      betrag: summe,
      farbe: b.color,
      spalte: 2,
    })
    links.push({
      source: budgetIndex,
      target: blockIndex,
      value: summe,
      farbe: b.color,
    })

    const gross = posten.filter((p) => p.betrag >= grenze)
    const klein = posten.filter((p) => p.betrag < grenze)

    for (const p of gross) {
      const i = add({
        name: p.label,
        betrag: p.betrag,
        farbe: b.color,
        spalte: 3,
      })
      links.push({ source: blockIndex, target: i, value: p.betrag, farbe: b.color })
    }
    if (klein.length > 0) {
      const rest = klein.reduce((s, p) => s + p.betrag, 0)
      const i = add({
        name: `Weitere ${klein.length} ${klein.length === 1 ? 'Posten' : 'Posten'}`,
        betrag: rest,
        farbe: b.color,
        spalte: 3,
      })
      links.push({ source: blockIndex, target: i, value: rest, farbe: b.color })
    }
  }

  // Was übrig ist, bekommt ein eigenes Band. Es geht ins Leere, und genau das
  // ist die Aussage.
  const offen = budget - verteilt
  if (offen > 0) {
    const i = add({
      name: 'Noch nicht verplant',
      betrag: offen,
      farbe: 'var(--muted-foreground)',
      spalte: 2,
    })
    links.push({
      source: budgetIndex,
      target: i,
      value: offen,
      farbe: 'var(--muted-foreground)',
    })
  }

  return { nodes, links, offen }
}

export function PlanSankey({ positions, budget }: Props) {
  const [inProzent, setInProzent] = useState(false)
  const summe = Number(budget)
  const { nodes, links, offen } = build(positions, summe)

  const zeige = (v: number) =>
    inProzent
      ? `${((v / summe) * 100).toFixed(1).replace('.', ',')} %`
      : euro.format(v)

  if (links.length === 0) {
    return (
      <p className="text-muted-foreground bg-card border-border rounded-lg border p-6 text-sm">
        Für dieses Diagramm braucht der Monat Einnahmen und Posten. Sobald etwas
        geplant ist, steht hier, wohin es geht.
      </p>
    )
  }

  return (
    <section className="bg-card border-border flex flex-col gap-4 rounded-lg border p-5">
      <header className="flex flex-wrap items-baseline justify-between gap-3">
        <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
          <span className="font-mono text-2xl font-semibold tabular-nums">
            {euro.format(summe)}
          </span>
          <span className="text-muted-foreground text-sm">
            Budget · davon {euro.format(offen)} noch nicht verplant
          </span>
        </div>

        {/* Prozent, weil 50/30/20 eine Prozentregel ist — in Euro liest man die
            Quote nicht ab. */}
        <div className="flex gap-1">
          {[
            { label: '€', wert: false },
            { label: '%', wert: true },
          ].map((o) => (
            <Button
              key={o.label}
              type="button"
              size="sm"
              variant={inProzent === o.wert ? 'secondary' : 'ghost'}
              aria-pressed={inProzent === o.wert}
              onClick={() => setInProzent(o.wert)}
            >
              {o.label}
            </Button>
          ))}
        </div>
      </header>

      <ChartContainer config={CONFIG} className="h-[26rem] w-full min-w-[42rem]">
        <Sankey
          data={{ nodes, links }}
          nodeWidth={11}
          nodePadding={16}
          margin={{ left: 4, right: 4, top: 8, bottom: 8 }}
          // Ausdrücklich durchgereicht statt gespreadet: Recharts hängt jedem
          // Aufruf hunderte SVG-Attribute an, und meine Zusatzfelder am
          // Payload kennen seine Typen nicht.
          link={(props) => (
            <Band
              sourceX={props.sourceX}
              sourceY={props.sourceY}
              sourceControlX={props.sourceControlX}
              targetX={props.targetX}
              targetY={props.targetY}
              targetControlX={props.targetControlX}
              linkWidth={props.linkWidth}
              payload={props.payload}
            />
          )}
          node={(props) => (
            <Kachel
              x={props.x}
              y={props.y}
              width={props.width}
              height={props.height}
              payload={props.payload}
              zeige={zeige}
            />
          )}
        >
          <Tooltip content={<Hinweis zeige={zeige} />} />
        </Sankey>
      </ChartContainer>
    </section>
  )
}

/**
 * Ein Band. Recharts liefert die Kontrollpunkte, gezeichnet wird ein Strich mit
 * der Bandbreite als Strichstärke — so treffen die Bänder senkrecht auf die
 * Knoten und knicken nicht.
 */
function Band({
  sourceX,
  sourceY,
  sourceControlX,
  targetX,
  targetY,
  targetControlX,
  linkWidth,
  payload,
}: {
  sourceX: number
  sourceY: number
  sourceControlX: number
  targetX: number
  targetY: number
  targetControlX: number
  linkWidth: number
  payload: unknown
}) {
  const { farbe } = payload as { farbe?: string }
  return (
    <path
      d={`M${sourceX},${sourceY}C${sourceControlX},${sourceY} ${targetControlX},${targetY} ${targetX},${targetY}`}
      fill="none"
      stroke={farbe ?? 'var(--muted-foreground)'}
      strokeWidth={linkWidth}
      strokeOpacity={0.32}
      className="hover:stroke-opacity-60 transition-opacity"
    />
  )
}

/** Ein Knoten samt Beschriftung. Die Seite hängt an der Spalte. */
function Kachel({
  x,
  y,
  width,
  height,
  payload,
  zeige,
}: {
  x: number
  y: number
  width: number
  height: number
  payload: unknown
  zeige: (v: number) => string
}) {
  const knoten = payload as Knoten
  // Letzte Spalte nach innen beschriften, sonst läuft der Text aus dem Bild.
  const links = knoten.spalte === 3
  const tx = links ? x - 8 : x + width + 8
  const anker = links ? 'end' : 'start'
  const platz = height > 15

  return (
    <Layer>
      <Rectangle
        x={x}
        y={y}
        width={width}
        height={height}
        fill={knoten.farbe}
        fillOpacity={knoten.spalte === 0 ? 0.6 : 1}
        radius={1}
      />
      <text
        x={tx}
        y={y + height / 2 + (platz ? -2 : 4)}
        textAnchor={anker}
        className="fill-foreground text-[12px]"
      >
        {knoten.name}
      </text>
      {platz && (
        <text
          x={tx}
          y={y + height / 2 + 12}
          textAnchor={anker}
          className="fill-muted-foreground font-mono text-[11px] tabular-nums"
        >
          {zeige(knoten.betrag)}
        </text>
      )}
    </Layer>
  )
}

/** Tooltip für Bänder und Knoten — beide kommen im selben Payload an. */
function Hinweis({
  active,
  payload,
  zeige,
}: {
  active?: boolean
  payload?: { payload?: unknown }[]
  zeige: (v: number) => string
}) {
  if (!active || !payload?.length) return null
  const roh = payload[0].payload as {
    name?: string
    betrag?: number
    value?: number
    source?: Knoten
    target?: Knoten
  }

  const text =
    roh.source && roh.target
      ? `${roh.source.name} → ${roh.target.name}`
      : (roh.name ?? '')
  const wert = roh.betrag ?? roh.value ?? 0

  return (
    <div className="bg-popover text-popover-foreground border-border rounded-md border px-2.5 py-1.5 text-xs shadow-sm">
      <p>{text}</p>
      <p className="font-mono font-medium tabular-nums">{zeige(wert)}</p>
    </div>
  )
}
