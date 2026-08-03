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
 * Die Postenliste für Papier — Seite 2 des Ausdrucks.
 *
 * Nachgebaut nach Toms Excel, das er seit Jahren führt: fünf Spalten, nach
 * Block gruppiert, Zeilen nach Zahlungsart eingefärbt. Die **laufende Summe**
 * ist die Spalte, die auf dem Bildschirm fehlt — am Küchentisch liest man daran
 * ab, wo man mitten im Block steht, ohne zu addieren.
 *
 * Eigene Komponente statt umgestylter Bildschirmliste: dort trägt jede Zeile
 * Abzeichen, Kategorie und einen Haken zum Klicken. Auf Papier ist das Ballast,
 * und aus 26 Posten würden drei Seiten statt einer.
 *
 * ## Farben
 *
 * Toms Legende, nicht Duofys Blockfarben — hier zählt, **wie** gezahlt wird:
 *
 *     Abhebung · Überweisung · Dauerauftrag oder Lastschrift · Besonderheit
 *
 * Am Bildschirm wäre das eine fünfte Farbachse zu viel. Auf Papier ist es der
 * Schlüssel: man sieht, was von allein läuft und was man selbst anstoßen muss.
 */

type Props = {
  /**
   * Absichtlich nur `PlanSummary` plus Posten, nicht `PlanDetail`: der
   * gemeinsame Plan ist zusammengesetzt und hat weder `id` noch `confirmedAt`.
   * Gebraucht werden hier ohnehin nur Budget, Quoten und die Posten.
   */
  plan: PlanSummary & { positions: PlanPosition[] }
  /**
   * Liefert den Vornamen hinter dem Posten, sonst null. Nur im gemeinsamen
   * Plan gesetzt — dort ist „wer trägt was" der Zweck des Blattes.
   */
  ownerName?: (position: PlanPosition) => string | null
}

/** Toms Legende. Helle Flächen, damit der Text darauf lesbar bleibt. */
const ZAHLART: Record<PaymentMethod, { label: string; fill: string }> = {
  withdrawal: { label: 'Abhebung', fill: '#dcefe0' },
  transfer: { label: 'Überweisung', fill: '#fae3cd' },
  standing_order: { label: 'Dauerauftrag', fill: '#f7dce8' },
  direct_debit: { label: 'Lastschrift', fill: '#f7dce8' },
  special: { label: 'Besonderheit', fill: '#faf0c8' },
}

/** Nach Fälligkeit, bei gleichem Tag der größere Betrag zuerst. */
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
          <section key={block} className="mb-4 break-inside-avoid">
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

            <table className="w-full border-collapse text-[10.5px]">
              <thead>
                <tr className="text-left text-[9px] uppercase">
                  <th className="w-8 py-0.5 font-medium">Tag</th>
                  <th className="py-0.5 font-medium">Bezeichnung</th>
                  {ownerName && (
                    <th className="w-16 py-0.5 font-medium">Wer</th>
                  )}
                  <th className="w-20 py-0.5 text-right font-medium">Betrag</th>
                  <th className="w-20 py-0.5 text-right font-medium">Summe</th>
                  <th className="w-20 py-0.5 text-right font-medium">Ist</th>
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
                      // Durchgestrichen, wenn erledigt: auf Papier hakt man
                      // nichts an, man sieht auf einen Blick was noch aussteht.
                      // `line-through` auf der Zeile trifft alle Zellen, auch
                      // die Beträge — genau so wie mit dem Stift.
                      className={`border-b border-black/10 ${
                        isPaid(p) ? 'text-black/55 line-through' : ''
                      }`}
                    >
                      <td className="py-0.5 tabular-nums">{p.dueDay}.</td>
                      <td className="py-0.5">
                        {p.label}
                        {p.passThrough && (
                          <span className="text-[9px]"> · durchlaufend</span>
                        )}
                      </td>
                      {ownerName && (
                        <td className="py-0.5">{ownerName(p) ?? ''}</td>
                      )}
                      <td className="py-0.5 text-right tabular-nums">
                        {euro.format(betrag)}
                      </td>
                      {/* Durchlaufendes zählt nicht mit — sonst behauptete die
                          Spalte eine Summe, die es nie gab. */}
                      <td className="py-0.5 text-right tabular-nums">
                        {p.passThrough ? '—' : euro.format(laufend)}
                      </td>
                      <td className="py-0.5 text-right tabular-nums">
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
                    <td className="py-0.5">Ist zusammen</td>
                    {ownerName && <td />}
                    <td />
                    <td />
                    <td className="py-0.5 text-right tabular-nums">
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
          // Dauerauftrag und Lastschrift teilen eine Farbe — einmal zeigen.
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
