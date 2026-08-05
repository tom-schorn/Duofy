import { Plus, User, Users } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Progress } from '@/components/ui/progress'
import {
  BLOCK_DOT,
  BLOCK_LABEL,
  CATEGORY_LABEL,
  PAYMENT_LABEL,
  euro,
  isPaid,
  type Block,
  type PlanPosition,
} from '@/lib/domain'

/**
 * Ein Budget mit Soll gegen Ist und seinen Posten.
 *
 * Bewusst **eine** Komponente für „Mein Plan" und den Haushalt: die Regeln
 * sind dieselben, nur die Datenquelle wechselt. Welchen Plan man sieht,
 * entscheidet der Umschalter in der Sidebar.
 *
 * **Keine Untergruppen.** Wünsche ist eine flache Liste — Investitionen sind
 * ganz normale Posten der Kategorie „Investition", nicht mehr.
 */

/**
 * Balkenfarbe je Block — **ausgeschrieben**, nicht zusammengesetzt.
 *
 * Tailwind liest die Klassen aus dem Quelltext. Ein
 * `[&_...]:${BLOCK_DOT[block]}` steht dort nie als fertige Klasse und würde
 * deshalb nicht erzeugt: der Balken bliebe grau. Der Indikator von `Progress`
 * ist auf `bg-primary` festgelegt, also muss er überschrieben werden.
 */
const BAR: Record<Block, string> = {
  income: '[&_[data-slot=progress-indicator]]:bg-chart-3',
  needs: '[&_[data-slot=progress-indicator]]:bg-chart-1',
  wants: '[&_[data-slot=progress-indicator]]:bg-chart-2',
  savings: '[&_[data-slot=progress-indicator]]:bg-chart-4',
}

const BAR_OVER = '[&_[data-slot=progress-indicator]]:bg-destructive'

/** Ab wieviel Prozent über der Quote es rot wird. */
const OVER_QUOTA = 100

type Props = {
  block: Block
  /** Soll aus der Quote — null bei Einnahmen, die haben keine. */
  target: number | null
  positions: PlanPosition[]
  householdNames: Record<string, string>
  onEdit: (position: PlanPosition) => void
  onAdd: (block: Block) => void
  onTogglePaid: (position: PlanPosition) => void
  /** Gemeinsame Sicht: fremde Posten zeigt man, ändert sie aber nicht. */
  readOnly?: boolean
  /**
   * Getrennt von `readOnly`, weil Stufe „darf ändern" genau das erlaubt und
   * nicht mehr: einen Posten in einem fremden Plan **anzulegen** wäre keine
   * Vertretung, sondern ein neuer Vertrag im Namen des anderen.
   */
  canAdd?: boolean
  /** Liefert den Vornamen der Person hinter dem Posten, sonst null. */
  ownerName?: (position: PlanPosition) => string | null
}

export function BudgetSection({
  block,
  target,
  positions,
  householdNames,
  onEdit,
  onAdd,
  onTogglePaid,
  readOnly = false,
  canAdd = true,
  ownerName,
}: Props) {
  // Durchlaufende Posten stehen in der Liste, aber nicht in der Summe: sie
  // gehören nicht zum Budget, also darf die Quote sie nicht sehen.
  const total = positions.reduce(
    (sum, position) =>
      position.passThrough ? sum : sum + Number(position.amountPlanned),
    0
  )
  const percent = target && target > 0 ? (total / target) * 100 : 0
  const isOver = percent > OVER_QUOTA

  return (
    <section className="flex flex-col gap-3">
      <header className="flex flex-col gap-2">
        <div className="flex items-baseline justify-between gap-4">
          <h2 className="flex items-center gap-2 text-sm font-semibold tracking-wide uppercase">
            <span className={`size-2.5 rounded-sm ${BLOCK_DOT[block]}`} />
            {BLOCK_LABEL[block]}
          </h2>
          <span className="text-sm tabular-nums">
            <span className={isOver ? 'text-destructive font-semibold' : 'font-semibold'}>
              {euro.format(total)}
            </span>
            {target !== null && (
              <span className="text-muted-foreground">
                {' '}
                von {euro.format(target)}
              </span>
            )}
          </span>
        </div>

        {target !== null && (
          // Der Indikator von `Progress` ist auf `bg-primary` festgelegt; die
          // Blockfarbe ist bei uns aber Identität, also wird sie überschrieben.
          <Progress
            value={Math.min(percent, 100)}
            aria-label={`${BLOCK_LABEL[block]}: ${Math.round(percent)} % der Quote`}
            className={`h-1.5 ${isOver ? BAR_OVER : BAR[block]}`}
          />
        )}
      </header>

      <ul className="flex flex-col">
        {positions.map((position) => (
          <PositionRow
            key={position.id}
            position={position}
            householdNames={householdNames}
            onEdit={onEdit}
            onTogglePaid={onTogglePaid}
            readOnly={readOnly}
            ownerName={ownerName?.(position) ?? null}
          />
        ))}
      </ul>

      {/* Anlegen direkt am Budget — dann stimmt die Zuordnung schon, ohne
          dass man sie im Formular suchen muss. */}
      {!readOnly && canAdd && (
      <Button
        type="button"
        variant="ghost"
        size="sm"
        onClick={() => onAdd(block)}
        className="text-muted-foreground hover:text-foreground w-fit"
      >
        <Plus className="size-4" />
        Posten in {BLOCK_LABEL[block]}
      </Button>
      )}
    </section>
  )
}

function PositionRow({
  position,
  householdNames,
  onEdit,
  onTogglePaid,
  readOnly,
  ownerName,
}: {
  position: PlanPosition
  householdNames: Record<string, string>
  onEdit: (position: PlanPosition) => void
  onTogglePaid: (position: PlanPosition) => void
  readOnly: boolean
  ownerName: string | null
}) {
  const planned = Number(position.amountPlanned)
  const actual =
    position.amountActual === null ? null : Number(position.amountActual)
  // Nur echte Überschreitungen markieren — Unterschreitungen sind kein Problem.
  const overspent = actual !== null && actual > planned
  const paid = isPaid(position)

  return (
    <li className="border-border/60 grid grid-cols-[auto_1fr_auto] items-center gap-3 border-b py-2.5 last:border-b-0">
      {/* Abhaken ist der Alltag nach dem Planen — deshalb ein eigener Knopf,
          nicht im Formular versteckt.

          Einnahmen lassen sich ebenfalls abhaken, dort heißt es „ist da" statt
          „bezahlt". Auf „Noch offen" wirkt das nicht: die Zahl klammert
          Einnahmen ohnehin aus. */}
      {position.isBudget ? (
        /* Budget-Posten hakt man nicht ab — sie füllen sich über den Monat
           aus einzelnen Buchungen. Ein Haken hätte hier keine Bedeutung.
           Statt eines toten Kästchens steht hier nichts. */
        <span className="size-6" aria-hidden />
      ) : (
        /* `Checkbox` aus dem Theme statt eigenem Knopf. „Erledigt" ist ein
           Zustand, kein Werkzeugschalter — die Checkbox sagt ihn von sich aus
           an, in der Nur-Lese-Ansicht als deaktiviert. Vorher stand hier ein
           Knopf mit `aria-pressed` und daneben ein `role="img"`-Behelf.

           Grün statt Primärfarbe: „bezahlt" gehört zu Sparen/Erledigt, nicht
           zur Markenfarbe. Die Klasse ist ausgeschrieben, weil Tailwind
           zusammengesetzte nicht erzeugt.

           Der Rahmen ist size-6, das Kästchen size-4: WCAG 2.2 SC 2.5.8
           verlangt 24 × 24 px Zielfläche, und die Checkbox bringt ihre über
           `after:-inset` mit. */
        <span className="flex size-6 items-center justify-center">
          <Checkbox
            checked={paid}
            disabled={readOnly}
            onCheckedChange={() => onTogglePaid(position)}
            aria-label={
              paid
                ? `${position.label} wieder öffnen`
                : position.block === 'income'
                  ? `${position.label} als erhalten markieren`
                  : `${position.label} abhaken`
            }
            className="data-checked:border-chart-4 data-checked:bg-chart-4 data-checked:text-background"
          />
        </span>
      )}

      <button
        type="button"
        onClick={() => onEdit(position)}
        disabled={readOnly}
        className={`flex min-w-0 flex-col items-start gap-0.5 text-left ${paid ? 'opacity-60' : ''} ${readOnly ? 'cursor-default' : ''}`}
      >
        <span className="flex flex-wrap items-center gap-2">
          <span className="font-medium">{position.label}</span>
          {/* In der gemeinsamen Sicht steht hier die Person, nicht der
              Haushalt — der ist dort in jeder Zeile derselbe und sagt nichts.
              Im eigenen Plan umgekehrt: dort ist die Person klar, und der
              Badge zeigt, dass der Posten zusätzlich in einen Haushaltsplan
              läuft. */}
          {ownerName ? (
            <Badge variant="secondary" className="gap-1 font-normal">
              <User className="size-3" />
              {ownerName}
            </Badge>
          ) : (
            position.householdId && (
              <Badge variant="secondary" className="gap-1 font-normal">
                <Users className="size-3" />
                {householdNames[position.householdId]}
              </Badge>
            )
          )}
        </span>
        <span className="text-muted-foreground truncate text-xs">
          {CATEGORY_LABEL[position.category]} · {position.dueDay}.
          {position.paymentMethod
            ? ` · ${PAYMENT_LABEL[position.paymentMethod]}`
            : ''}
          {position.commitmentId ? ' · aus Vertrag' : ''}
          {position.isBudget ? ' · Budget' : ''}
          {position.passThrough ? ' · durchlaufend' : ''}
          {position.counterAccountId ? ' · Umbuchung' : ''}
        </span>
      </button>

      <span className="flex flex-col items-end gap-1 tabular-nums">
        {position.isBudget ? (
          <>
            <span className="text-sm">
              <span
                className={`font-medium ${overspent ? 'text-destructive' : ''}`}
              >
                {euro.format(actual ?? 0)}
              </span>
              <span className="text-muted-foreground">
                {' '}
                von {euro.format(planned)}
              </span>
            </span>
            {/* Füllstand statt Haken: die Frage ist „wie viel ist weg", nicht
                „ist es erledigt". */}
            <Progress
              value={Math.min(((actual ?? 0) / (planned || 1)) * 100, 100)}
              aria-label={`${position.label}: ${euro.format(actual ?? 0)} von ${euro.format(planned)}`}
              className={`w-24 ${overspent ? BAR_OVER : BAR.needs}`}
            />
          </>
        ) : (
          <>
            <span className="font-medium">{euro.format(planned)}</span>
            {actual !== null && actual !== planned && (
              <span
                className={`text-xs ${overspent ? 'text-destructive' : 'text-muted-foreground'}`}
              >
                ist {euro.format(actual)}
              </span>
            )}
          </>
        )}
      </span>
    </li>
  )
}
