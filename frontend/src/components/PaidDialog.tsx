import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'

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
  /**
   * The position already has bookings. The tick then books nothing more, so date
   * and amount would be ignored — the fields are disabled and say so.
   */
  hasBookings?: boolean
  /** The sentence for a rejected tick; the dialog stays open and shows it. */
  error?: string | null
}

export function PaidDialog({
  position,
  onClose,
  onConfirm,
  pending,
  hasBookings = false,
  error = null,
}: Props) {
  const { t } = useTranslation()
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
  const differs = !hasBookings && Number.isFinite(entered) && entered !== planned

  function submit(event: React.FormEvent) {
    event.preventDefault()
    onConfirm({ occurredOn, amount })
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-md">
        <form onSubmit={submit} className="flex flex-col gap-5">
          <DialogHeader>
            <DialogTitle>
              {t('paidDialog.title', { label: position.label })}
            </DialogTitle>
            <DialogDescription>
              {t('paidDialog.description')}
            </DialogDescription>
          </DialogHeader>

          <div
            role="group"
            aria-describedby={hasBookings ? 'paid-bookings-note' : undefined}
            className="flex gap-3"
          >
            <div className="flex flex-1 flex-col gap-1.5">
              <Label htmlFor="paid-date">{t('common.date')}</Label>
              <DateField
                id="paid-date"
                value={occurredOn}
                onChange={setOccurredOn}
                disabled={hasBookings}
              />
            </div>

            <div className="flex w-36 flex-col gap-1.5">
              <Label htmlFor="paid-amount">{t('common.amount')}</Label>
              <Input
                id="paid-amount"
                type="number"
                step="0.01"
                min="0.01"
                inputMode="decimal"
                value={amount}
                onChange={(event) => setAmount(event.target.value)}
                required
                aria-describedby={hasBookings ? 'paid-bookings-note' : undefined}
                disabled={hasBookings}
              />
            </div>
          </div>

          {hasBookings && (
            <p
              id="paid-bookings-note"
              className="text-muted-foreground text-sm"
              role="status"
            >
              {t('paidDialog.hasBookings')}
            </p>
          )}

          {error && (
            <p className="text-destructive text-sm" role="alert">
              {error}
            </p>
          )}

          {/* Nur wenn abweichend: sonst wäre es eine Zeile, die immer dasselbe
              sagt wie das Feld daneben. */}
          {differs && (
            <p className="text-muted-foreground text-sm" role="status">
              {t('paidDialog.differs', {
                planned: euro.format(planned),
                entered: euro.format(entered),
              })}
            </p>
          )}

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose}>
              {t('common.cancel')}
            </Button>
            <Button type="submit" disabled={pending}>
              {pending ? t('paidDialog.pending') : t('paidDialog.submit')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
