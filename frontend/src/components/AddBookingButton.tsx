import { useState } from 'react'
import { useTranslation } from 'react-i18next'

import { BookingDialog } from '@/components/BookingDialog'
import { Button } from '@/components/ui/button'
import { OWN_SCOPE, type BookScope, type PlanPosition } from '@/lib/domain'
import { useAccounts } from '@/lib/queries'

/**
 * "Buchung anlegen" at the top of the plan page (#241).
 *
 * The book used to carry its own quick-entry form above the list. The book is now
 * one tab of the plan page, and adding a booking is something one does from any of
 * its tabs — hence a button on the page and a dialog, not a form inside one tab.
 *
 * Absent when the person may not book (no button rather than a disabled one) and
 * when there is no account to book on — the book's empty state says what to do.
 */
export function AddBookingButton({
  year,
  month,
  positions,
  scope = OWN_SCOPE,
  readOnly = false,
}: {
  year: number
  month: number
  positions: PlanPosition[]
  /** Whose book: your own, one person, or the household. */
  scope?: BookScope
  readOnly?: boolean
}) {
  const { t } = useTranslation()
  const accounts = useAccounts(scope).data ?? []
  const [open, setOpen] = useState(false)

  const usable = accounts.filter((account) => account.active)
  if (readOnly || usable.length === 0) return null

  return (
    <>
      <Button onClick={() => setOpen(true)}>{t('monthBook.add')}</Button>
      {open && (
        <BookingDialog
          accounts={usable}
          positions={[{ year, month, positions }]}
          viewedMonth={{ year, month }}
          onClose={() => setOpen(false)}
          start={{ kind: 'new', scope }}
        />
      )}
    </>
  )
}
