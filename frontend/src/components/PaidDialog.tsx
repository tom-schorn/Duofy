import { useEffect, useState } from 'react'

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { DateField } from '@/components/DateField'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { today } from '@/lib/dates'
import { euro, type PlanPosition } from '@/lib/domain'

/**
 * What gets booked when a position is ticked off.
 *
 * The tick creates a booking, and that needs a date and an amount. Both are
 * prefilled — today, as planned — and most of the time one just hits Enter. The
 * dialog exists for the cases in between: the payment went out two days ago, or the
 * instalment came out differently than expected.
 *
 * The **month of the position does not change.** Ticking off an August position
 * with a July date gives a July booking on an August position — that is exactly
 * what is meant, and exactly how it then appears in the book.
 */

type Props = {
  /** The position being ticked off, or null while the dialog is closed. */
  position: PlanPosition | null
  onClose: () => void
  onConfirm: (values: { occurredOn: string; amount: string }) => void
  pending: boolean
}

export function PaidDialog({ position, onClose, onConfirm, pending }: Props) {
  const [occurredOn, setOccurredOn] = useState(today())
  const [amount, setAmount] = useState('')

  // Back to the defaults on every open. Without this, the second position would
  // still show the amount of the first.
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
              <DateField
                id="paid-date"
                value={occurredOn}
                onChange={setOccurredOn}
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
