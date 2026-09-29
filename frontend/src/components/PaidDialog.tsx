import { useEffect, useId, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { AmountField } from '@/components/AmountField'
import { DialogFrame } from '@/components/DialogFrame'
import { Calendar } from '@/components/ui/calendar'
import { Label } from '@/components/ui/label'
import { SentenceWord } from '@/components/SentenceWord'
import { SentencePanel } from '@/components/SentencePanel'
import { fillSentence } from '@/lib/sentence'
import { fromIsoDay, longDate, toIsoDay, today } from '@/lib/dates'
import { monthLabel, type PlanPosition } from '@/lib/domain'

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
 * what is meant; the dialog only says so in one line.
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
  /** The bookings could not be loaded, so nobody knows whether date and amount count. */
  bookingsUnknown?: boolean
  /** The plan month, to say when the booking date falls outside it. */
  planMonth?: { year: number; month: number }
  /** The sentence for a rejected tick; the dialog stays open and shows it. */
  error?: string | null
}

export function PaidDialog({
  position,
  onClose,
  onConfirm,
  pending,
  hasBookings = false,
  bookingsUnknown = false,
  planMonth,
  error = null,
}: Props) {
  const { t } = useTranslation()
  const [occurredOn, setOccurredOn] = useState(today())
  const [amount, setAmount] = useState('')
  const [dateOpen, setDateOpen] = useState(false)
  const dateWordRef = useRef<HTMLButtonElement | null>(null)
  // Read out with the word and the opened field, so a screen reader hears the
  // whole sentence, not just the one word (issue #202, review D-215-3, fix 1).
  const sentenceId = useId()

  // Back to the defaults on every open. Without this, the second position would
  // still show the amount of the first.
  useEffect(() => {
    if (position) {
      setOccurredOn(today())
      setAmount(position.amountPlanned)
      setDateOpen(false)
    }
  }, [position])

  if (!position) return null

  function closeDateWord() {
    setDateOpen(false)
    requestAnimationFrame(() => dateWordRef.current?.focus())
  }

  // A payment on 25.06. may belong to the July plan; only say so, never block it.
  const outsideMonth =
    !hasBookings &&
    planMonth !== undefined &&
    occurredOn !== '' &&
    occurredOn.slice(0, 7) !==
      `${planMonth.year}-${String(planMonth.month).padStart(2, '0')}`

  function submit(event: React.FormEvent) {
    event.preventDefault()
    onConfirm({ occurredOn, amount })
  }

  return (
    <DialogFrame
      open
      onOpenChange={(open) => !open && onClose()}
      title={t('paidDialog.title', { label: position.label })}
      submitLabel={t('paidDialog.submit')}
      pendingLabel={t('paidDialog.pending')}
      onSubmit={submit}
      dirty={occurredOn !== today() || amount !== position.amountPlanned}
      pending={pending}
      error={error}
    >
      <div className="flex flex-col gap-2">
        <Label htmlFor="paid-amount" className="text-muted-foreground text-sm">
          {t('common.amount')}
        </Label>
        <AmountField
          id="paid-amount"
          value={amount}
          onChange={setAmount}
          required
          aria-describedby={hasBookings ? 'paid-bookings-note' : undefined}
          disabled={hasBookings}
          inputClassName="h-auto border-0 bg-transparent px-0 pr-7 text-xl font-semibold placeholder:text-muted-foreground md:text-xl"
        />
      </div>

      <div
        className="flex flex-col gap-3"
        onKeyDownCapture={(event) => {
          if (event.key !== 'Escape' || !dateOpen) return
          event.stopPropagation()
          event.preventDefault()
          closeDateWord()
        }}
      >
        <p id={sentenceId} className="text-base leading-relaxed">
          {fillSentence(t('paidDialog.sentence'), {
            date: hasBookings ? (
              <span className="font-medium">{longDate(occurredOn)}</span>
            ) : (
              <SentenceWord
                ref={dateWordRef}
                open={dateOpen}
                onClick={() => setDateOpen((open) => !open)}
                describedBy={sentenceId}
              >
                {longDate(occurredOn)}
              </SentenceWord>
            ),
          })}
        </p>
        {!hasBookings && dateOpen && (
          <SentencePanel label={t('paidDialog.dateLabel')}>
            <Calendar
              mode="single"
              selected={fromIsoDay(occurredOn)}
              defaultMonth={fromIsoDay(occurredOn)}
              onSelect={(date) => {
                if (!date) return
                setOccurredOn(toIsoDay(date))
                closeDateWord()
              }}
              aria-describedby={sentenceId}
              autoFocus
            />
          </SentencePanel>
        )}
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

      {bookingsUnknown && (
        <p className="text-muted-foreground text-sm" role="status">
          {t('paidDialog.bookingsUnknown')}
        </p>
      )}

      {outsideMonth && planMonth && (
        <p className="text-muted-foreground text-sm" role="status">
          {t('paidDialog.outsideMonth', {
            month: `${monthLabel(planMonth.month)} ${planMonth.year}`,
          })}
        </p>
      )}
    </DialogFrame>
  )
}
