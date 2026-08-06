import { useState } from 'react'
import { Layer, Rectangle, Sankey, Tooltip } from 'recharts'

import {
  Empty,
  EmptyDescription,
  EmptyHeader,
} from '@/components/ui/empty'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { ChartContainer, type ChartConfig } from '@/components/ui/chart'
import { euro, type PlanPosition } from '@/lib/domain'

/**
 * Where the budget goes — read from left to right.
 *
 * Income → budget → the three blocks → individual positions. Not a timeline but a
 * balance sheet: the chart answers "where does the money go", not "does the month
 * hold up over time". The book answers the latter.
 *
 * The **budget in the middle is the bottleneck**, and that is exactly where the
 * buffer sits. A pass-through position visibly bypasses it: it appears in none of
 * the columns, because it never was budget.
 *
 * ## Why bands are bundled
 *
 * 25 positions would be 25 bands, and a band worth 2.99 would be thinner than its
 * own label. Only what makes up at least `OWN_BAND` of the budget gets a band of its
 * own; the rest is merged into one band per block. The threshold is relative so it
 * works in any household.
 *
 * ## What the chart makes visible
 *
 * Above all what has **not** been allocated. "Not yet allocated" is a band like any
 * other and runs into nothing — the figures carry the same number, but there one
 * cannot see how big it is.
 */

type Props = {
  positions: PlanPosition[]
  /** Income minus buffer — comes from the server. */
  budget: string
  /**
   * From which share of the budget a position gets a band of its own.
   *
   * Coarser on paper than on screen: the plot area is flatter there, and eleven
   * nodes in the last column write their names on top of each other. Fewer but
   * thicker bands read better in print than complete ones.
   */
  threshold?: number
  /**
   * Height of the plot area as a Tailwind class.
   *
   * Flatter for printing so that page one does not overflow. Has to be a prop and
   * not a `print:` class: Recharts measures before printing, and inside the print
   * medium it would be too late.
   */
  height?: string
}

/** From which share of the budget a position gets a band of its own. */
const OWN_BAND = 0.02

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
  /** 0 income · 1 budget · 2 block · 3 position. Decides which side the label sits on. */
  spalte: number
}

type Kante = { source: number; target: number; value: number; farbe: string }

/**
 * Builds nodes and links from the plan.
 *
 * Recharts expects links as **indices** into the node array, which is why this
 * counts along instead of linking by name. Bands worth 0 are dropped — they would
 * distort the layout and would be invisible anyway.
 */
function build(
  positions: PlanPosition[],
  budget: number,
  anteil: number
) {
  const zaehlend = positions.filter((p) => !p.passThrough)
  const threshold = budget * anteil

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

    const gross = posten.filter((p) => p.betrag >= threshold)
    const klein = posten.filter((p) => p.betrag < threshold)

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
        name:
          klein.length === 1
            ? '1 weiterer Posten'
            : `${klein.length} weitere Posten`,
        betrag: rest,
        farbe: b.color,
        spalte: 3,
      })
      links.push({ source: blockIndex, target: i, value: rest, farbe: b.color })
    }
  }

  // Whatever is left gets a band of its own. It runs into nothing, and that is
  // exactly the statement.
  const offen = budget - verteilt
  if (offen > 0) {
    const i = add({
      name: 'Noch nicht verplant',
      betrag: offen,
      farbe: 'var(--muted-foreground)',
      // Column 3, not 2: the node has no outgoing link, and Recharts pushes sinks
      // into the last column. With 2 here the label would sit to its right, which
      // is outside the picture.
      spalte: 3,
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

export function PlanSankey({
  positions,
  budget,
  threshold = OWN_BAND,
  height = 'h-[26rem]',
}: Props) {
  const [inProzent, setInProzent] = useState(false)
  const summe = Number(budget)
  const { nodes, links, offen } = build(positions, summe, threshold)

  const zeige = (v: number) =>
    inProzent
      ? `${((v / summe) * 100).toFixed(1).replace('.', ',')} %`
      : euro.format(v)

  if (links.length === 0) {
    return (
      <Empty className="border-border rounded-xl border border-dashed">
        <EmptyHeader>
          <EmptyDescription>Für dieses Diagramm braucht der Monat Einnahmen und Posten. Sobald etwas
        geplant ist, steht hier, wohin es geht.</EmptyDescription>
        </EmptyHeader>
      </Empty>
    )
  }

  return (
    <Card className="gap-4 px-5 py-5">
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
        <div className="flex gap-1" data-print="hide">
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

      <ChartContainer
        config={CONFIG}
        className={`${height} w-full min-w-[42rem]`}
      >
        <Sankey
          data={{ nodes, links }}
          nodeWidth={11}
          nodePadding={16}
          margin={{ left: 4, right: 4, top: 8, bottom: 8 }}
          // Passed through explicitly rather than spread: Recharts attaches
          // hundreds of SVG attributes to every call, and the extra fields on the
          // payload are unknown to its types.
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
    </Card>
  )
}

/**
 * One band. Recharts supplies the control points; what is drawn is a stroke whose
 * width is the band width — that way the bands meet the nodes perpendicularly
 * instead of kinking.
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

/** One node including its label. Which side it sits on depends on the column. */
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
  // Label the last column inwards, otherwise the text runs out of the picture.
  const links = knoten.spalte === 3
  const tx = links ? x - 8 : x + width + 8
  const anker = links ? 'end' : 'start'
  // Two lines need height. On a thin band the amount goes behind the name — it
  // used to be dropped, and the small positions are exactly the ones where one
  // wants to know it.
  const zweizeilig = height > 22

  /**
   * Block names sit **above** their node, not beside it.
   *
   * Beside it they collided with the position names in the last column: a block and
   * its first position share the same height, because the first position starts at
   * the top edge of its block. Above the node there is room and nothing else.
   */
  if (knoten.spalte === 2) {
    return (
      <Layer>
        <Rectangle
          x={x}
          y={y}
          width={width}
          height={height}
          fill={knoten.farbe}
          radius={1}
        />
        <text
          x={x + width + 6}
          y={y + 9}
          textAnchor="start"
          className="fill-foreground text-[12px]"
          style={{ paintOrder: 'stroke', stroke: 'var(--card)', strokeWidth: 3 }}
        >
          {knoten.name}
          <tspan className="fill-muted-foreground font-mono tabular-nums">
            {'  '}
            {zeige(knoten.betrag)}
          </tspan>
        </text>
      </Layer>
    )
  }

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
        y={y + height / 2 + (zweizeilig ? -2 : 4)}
        textAnchor={anker}
        className="fill-foreground text-[12px]"
        style={{ paintOrder: 'stroke', stroke: 'var(--card)', strokeWidth: 3 }}
      >
        {knoten.name}
        {!zweizeilig && (
          <tspan className="fill-muted-foreground font-mono tabular-nums">
            {'  '}
            {zeige(knoten.betrag)}
          </tspan>
        )}
      </text>
      {zweizeilig && (
        <text
          x={tx}
          y={y + height / 2 + 12}
          textAnchor={anker}
          className="fill-muted-foreground font-mono text-[11px] tabular-nums"
          style={{ paintOrder: 'stroke', stroke: 'var(--card)', strokeWidth: 3 }}
        >
          {zeige(knoten.betrag)}
        </text>
      )}
    </Layer>
  )
}

/** Tooltip for bands and nodes — both arrive in the same payload. */
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
