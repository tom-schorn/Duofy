import {
  BLOCK_LABEL,
  BUDGETS,
  euro,
  isPaid,
  QUOTA_KEY,
  type Block,
  type PaymentMethod,
  type PlanPosition,
  type PlanSummary,
} from '@/lib/domain'

/**
 * The position list for paper — page two of the printout.
 *
 * Modelled on a spreadsheet kept for years: five columns, grouped by block, rows
 * coloured by payment method. The **running total** is the column the screen does
 * not have — at the kitchen table it tells you where you stand in the middle of a
 * block without adding anything up.
 *
 * A component of its own rather than a restyled screen list: there every row
 * carries badges, a category and a tick box to click. On paper that is ballast, and
 * 26 positions would become three pages instead of one.
 *
 * ## Colours
 *
 * The payment method, not the block colours — on paper what matters is **how**
 * something is paid:
 *
 *     withdrawal · transfer · standing order or direct debit · special
 *
 * On screen that would be a fifth colour axis too many. On paper it is the key: one
 * sees what runs by itself and what has to be triggered by hand.
 */

type Props = {
  /**
   * Deliberately only `PlanSummary` plus positions, not `PlanDetail`: the shared
   * plan is composed and has neither `id` nor `confirmedAt`. Only the budget, the
   * quotas and the positions are needed here anyway.
   */
  plan: PlanSummary & { positions: PlanPosition[] }
  /**
   * Returns the first name behind the position, otherwise null. Only set in the
   * shared plan, where "who carries what" is the point of the sheet.
   */
  ownerName?: (position: PlanPosition) => string | null
}

/** The payment-method legend. Light fills so the text on them stays readable. */
const ZAHLART: Record<PaymentMethod, { label: string; fill: string }> = {
  withdrawal: { label: 'Abhebung', fill: '#dcefe0' },
  transfer: { label: 'Überweisung', fill: '#fae3cd' },
  standing_order: { label: 'Dauerauftrag', fill: '#f7dce8' },
  direct_debit: { label: 'Lastschrift', fill: '#f7dce8' },
  special: { label: 'Besonderheit', fill: '#faf0c8' },
}

/** By due day; on the same day the larger amount first. */
function sortiert(positions: PlanPosition[]): PlanPosition[] {
  return [...positions].sort(
    (a, b) =>
      a.dueDay - b.dueDay ||
      Number(b.amountPlanned) - Number(a.amountPlanned)
  )
}

export function PlanPrintout({ plan, ownerName }: Props) {
  const budget = Number(plan.budget)

  const gruppen = [
    { block: 'income' as Block, quote: null },
    ...BUDGETS.map((block) => ({
      block,
      quote: Number(plan[QUOTA_KEY[block as keyof typeof QUOTA_KEY]]),
    })),
  ]

  return (
    <div className="hidden print:block">
      {/* Erzwungener Umbruch: Seite 1 ist Übersicht und Diagramm, Seite 2 sind
          die Posten. Ohne das entscheidet der Browser, und dann reißt es mitten
          in einem Block.

          Die Regel sitzt an der Überschrift, nicht an einem leeren `div` —
          leere Elemente überspringen manche Browser beim Umbruch. */}
      <h2 className="mb-3 break-before-page text-base font-semibold">
        Posten — {plan.positions.length} Stück
      </h2>

      {gruppen.map(({ block, quote }) => {
        const zeilen = sortiert(
          plan.positions.filter((p) => p.block === block)
        )
        if (zeilen.length === 0) return null

        const zaehlend = zeilen.filter((p) => !p.passThrough)
        const soll = zaehlend.reduce((s, p) => s + Number(p.amountPlanned), 0)
        const ist = zaehlend.reduce((s, p) => s + Number(p.amountActual ?? 0), 0)
        let laufend = 0

        return (
          <section key={block} className="mb-2.5 break-inside-avoid">
            <div className="flex items-baseline justify-between border-b border-black/40 pb-0.5 text-[11px] font-semibold">
              <span className="uppercase">
                {BLOCK_LABEL[block]}
                {quote !== null && (
                  <span className="font-normal"> · Soll {quote} %</span>
                )}
              </span>
              <span className="tabular-nums">
                {euro.format(soll)}
                {quote !== null && budget > 0 && (
                  <span className="font-normal">
                    {' '}
                    · Ist {((soll / budget) * 100).toFixed(1).replace('.', ',')} %
                  </span>
                )}
              </span>
            </div>

            <table className="w-full border-collapse text-[10px] leading-tight">
              <thead>
                <tr className="text-left text-[9px] uppercase">
                  <th className="w-8 py-px font-medium">Tag</th>
                  <th className="py-px font-medium">Bezeichnung</th>
                  {ownerName && (
                    <th className="w-16 py-px font-medium">Wer</th>
                  )}
                  <th className="w-20 py-px text-right font-medium">Betrag</th>
                  <th className="w-20 py-px text-right font-medium">Summe</th>
                  <th className="w-20 py-px text-right font-medium">Ist</th>
                </tr>
              </thead>
              <tbody>
                {zeilen.map((p) => {
                  const betrag = Number(p.amountPlanned)
                  if (!p.passThrough) laufend += betrag
                  const art = p.paymentMethod
                    ? ZAHLART[p.paymentMethod]
                    : undefined
                  return (
                    <tr
                      key={p.id}
                      style={art ? { backgroundColor: art.fill } : undefined}
                      // Struck through when done: on paper one does not tick
                      // boxes, one sees at a glance what is still outstanding.
                      // `line-through` on the row hits every cell including the
                      // amounts — exactly like a pen would.
                      className={`border-b border-black/10 ${
                        isPaid(p) ? 'text-black/55 line-through' : ''
                      }`}
                    >
                      <td className="py-px tabular-nums">{p.dueDay}.</td>
                      <td className="py-px">
                        {p.label}
                        {p.passThrough && (
                          <span className="text-[9px]"> · durchlaufend</span>
                        )}
                      </td>
                      {ownerName && (
                        <td className="py-px">{ownerName(p) ?? ''}</td>
                      )}
                      <td className="py-px text-right tabular-nums">
                        {euro.format(betrag)}
                      </td>
                      {/* Durchlaufendes zählt nicht mit — sonst behauptete die
                          Spalte eine Summe, die es nie gab. */}
                      <td className="py-px text-right tabular-nums">
                        {p.passThrough ? '—' : euro.format(laufend)}
                      </td>
                      <td className="py-px text-right tabular-nums">
                        {p.amountActual !== null
                          ? euro.format(Number(p.amountActual))
                          : ''}
                      </td>
                    </tr>
                  )
                })}
                {ist > 0 && (
                  <tr className="font-semibold">
                    <td />
                    <td className="py-px">Ist zusammen</td>
                    {ownerName && <td />}
                    <td />
                    <td />
                    <td className="py-px text-right tabular-nums">
                      {euro.format(ist)}
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </section>
        )
      })}

      <div className="mt-3 flex flex-wrap gap-x-4 gap-y-1 text-[9px]">
        {Object.entries(ZAHLART)
          // Standing order and direct debit share a colour — show it once.
          .filter(([key]) => key !== 'direct_debit')
          .map(([key, { label, fill }]) => (
            <span key={key} className="flex items-center gap-1">
              <span
                className="inline-block size-2 border border-black/20"
                style={{ backgroundColor: fill }}
              />
              {key === 'standing_order' ? 'Dauerauftrag / Lastschrift' : label}
            </span>
          ))}
      </div>
    </div>
  )
}
