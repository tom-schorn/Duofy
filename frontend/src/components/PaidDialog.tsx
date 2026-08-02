import { useEffect, useState } from 'react'

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { euro, type PlanPosition } from '@/lib/domain'

/**
 * Was beim Abhaken gebucht wird.
 *
 * Der Haken legt eine Buchung an, und die braucht ein Datum und einen Betrag.
 * Beides ist vorbelegt — heute, wie geplant — und in den meisten Fällen tippt
 * man nur Enter. Nötig ist das Fenster für die Fälle dazwischen: die Zahlung
 * lief vorgestern, oder der Abschlag kam anders als gedacht.
 *
 * Der **Monat des Postens ändert sich dadurch nicht.** Wer den August-Posten
 * mit dem 30. Juli abhakt, hat eine Juli-Buchung an einem August-Posten —
 * genau so ist es gemeint, und genau so steht es dann auch im Buch.
 */

type Props = {
  /** Der abzuhakende Posten, oder null wenn das Fenster zu ist. */
  position: PlanPosition | null
  onClose: () => void
  onConfirm: (values: { occurredOn: string; amount: string }) => void
  pending: boolean
}

function today(): string {
  return new Date().toISOString().slice(0, 10)
}

export function PaidDialog({ position, onClose, onConfirm, pending }: Props) {
  const [occurredOn, setOccurredOn] = useState(today())
  const [amount, setAmount] = useState('')

  // Bei jedem Öffnen zurück auf die Vorbelegung. Ohne das stünde beim zweiten
  // Posten noch der Betrag des ersten im Feld.
  useEffect(() => {
    if (position) {
      setOccurredOn(today())
      setAmount(position.amountPlanned)
    }
  }, [position])

  if (!position) return null

  const planned = Number(position.amountPlanned)
  const entered = Number(amount)
  const differs = Number.isFinite(entered) && entered !== planned

  function submit(event: React.FormEvent) {
    event.preventDefault()
    onConfirm({ occurredOn, amount })
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-md">
        <form onSubmit={submit} className="flex flex-col gap-5">
          <DialogHeader>
            <DialogTitle>{position.label} abhaken</DialogTitle>
            <DialogDescription>
              Das legt zugleich die Buchung im Haushaltsbuch an.
            </DialogDescription>
          </DialogHeader>

          <div className="flex gap-3">
            <div className="flex flex-1 flex-col gap-1.5">
              <Label htmlFor="paid-date">Datum</Label>
              <Input
                id="paid-date"
                type="date"
                value={occurredOn}
                onChange={(event) => setOccurredOn(event.target.value)}
                required
              />
            </div>

            <div className="flex w-36 flex-col gap-1.5">
              <Label htmlFor="paid-amount">Betrag</Label>
              <Input
                id="paid-amount"
                type="number"
                step="0.01"
                min="0.01"
                inputMode="decimal"
                value={amount}
                onChange={(event) => setAmount(event.target.value)}
                required
              />
            </div>
          </div>

          {/* Nur wenn abweichend: sonst wäre es eine Zeile, die immer dasselbe
              sagt wie das Feld daneben. */}
          {differs && (
            <p className="text-muted-foreground text-sm" role="status">
              Geplant waren {euro.format(planned)} — der Posten steht danach auf{' '}
              {euro.format(entered)}.
            </p>
          )}

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose}>
              Abbrechen
            </Button>
            <Button type="submit" disabled={pending}>
              {pending ? 'Bucht…' : 'Abhaken'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
