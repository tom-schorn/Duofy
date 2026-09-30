import { useState, type ReactNode } from 'react'
import { Trans, useTranslation } from 'react-i18next'

import { BookingDialog } from '@/components/BookingDialog'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { errorText } from '@/lib/api'
import {
  euro,
  isPaid,
  OWN_SCOPE,
  type BookScope,
  type PlanPosition,
} from '@/lib/domain'
import { positionHasBookings } from '@/lib/paid'
import { useAccounts, useTogglePaid, useTransactions } from '@/lib/queries'

/**
 * Ticking a position off, the same in every plan (#251).
 *
 * The own plan, another person's plan and the household plan all tick through this
 * one flow: the dialog with date and amount, the question before a self-created
 * booking disappears, and the toasts. What differs between the plans is only who
 * may tick (the caller decides that through `readOnly` on the rows) and where the
 * bookings are read from (`scope`). The backend books on the owner's behalf.
 */
export function usePaidFlow(
  year: number,
  month: number,
  scope: BookScope = OWN_SCOPE
): { toggle: (position: PlanPosition) => void; dialogs: ReactNode } {
  const { t } = useTranslation()
  const togglePaid = useTogglePaid()
  // The position whose booking dialog is currently open.
  const [booking, setBooking] = useState<PlanPosition | null>(null)
  /** The position whose self-created booking is about to disappear. */
  const [confirming, setConfirming] = useState<PlanPosition | null>(null)

  // For the confirmation when un-ticking: which booking hangs off which position.
  const transactions = useTransactions(year, month, scope)
  // To name the account the tick books on.
  const accounts = useAccounts(scope).data ?? []

  const bookingsUnknown = transactions.isError

  const autoBookedOf = (position: PlanPosition) =>
    transactions.data?.find(
      (entry) => entry.positionId === position.id && entry.autoBooked
    )

  /**
   * Ticking and un-ticking are not symmetric:
   *
   * Ticking quietly creates a booking — unless there is no account, in which case a
   * hint follows. Un-ticking **removes** the booking again, and that is a loss of
   * data one wants to know about.
   */
  function toggle(position: PlanPosition) {
    if (isPaid(position)) {
      // Bookings that are not there (yet) or cannot be read (no view on the
      // accounts) may still hang off the tick, so the question comes anyway, just
      // without an amount.
      if (transactions.data === undefined || autoBookedOf(position)) {
        setConfirming(position)
        return
      }
      togglePaid.mutate({ id: position.id, paid: false })
      return
    }

    // Always ask, even for a position that already has bookings: the dialog says
    // that date and amount are not used then, and shows a rejected tick in place.
    setBooking(position)
  }

  const dialogs = (
    <>
      {booking && (
        <BookingDialog
          key={booking.id}
          accounts={accounts}
          positions={[{ year, month, positions: [booking] }]}
          viewedMonth={{ year, month }}
          onClose={() => {
            setBooking(null)
            togglePaid.reset()
          }}
          start={{
            kind: 'tick',
            position: booking,
            onConfirm: ({ occurredOn, amount }) =>
              togglePaid.mutate(
                {
                  id: booking.id,
                  paid: true,
                  occurredOn,
                  amount,
                  inlineError: true,
                  hasBookings: positionHasBookings(booking.id, transactions.data),
                },
                { onSuccess: () => setBooking(null) }
              ),
            pending: togglePaid.isPending,
            hasBookings: positionHasBookings(booking.id, transactions.data),
            bookingsUnknown,
            error: togglePaid.isError ? errorText(togglePaid.error) : null,
          }}
        />
      )}

      {/* Enthaken entfernt die vom Haken erzeugte Buchung. Der Betrag steht
          in der Frage, damit man sieht, was verloren geht — falls er nach dem
          Abhaken von Hand korrigiert wurde. */}
      <AlertDialog
        open={confirming !== null}
        onOpenChange={(open) => !open && setConfirming(null)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('plan.untickTitle')}</AlertDialogTitle>
            <AlertDialogDescription>
              {confirming &&
                !autoBookedOf(confirming) &&
                t('plan.untickTextUnknown')}
              {confirming && autoBookedOf(confirming) && (
                <Trans
                  i18nKey="plan.untickText"
                  values={{
                    amount: euro.format(
                      Number(autoBookedOf(confirming)?.amount ?? 0)
                    ),
                  }}
                  components={{
                    amount: (
                      <span className="text-foreground font-mono font-medium" />
                    ),
                  }}
                />
              )}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t('common.cancel')}</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                if (confirming) {
                  togglePaid.mutate({ id: confirming.id, paid: false })
                }
                setConfirming(null)
              }}
            >
              {t('plan.untick')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  )

  return { toggle, dialogs }
}
