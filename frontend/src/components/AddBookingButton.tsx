import { useState } from 'react'
import { useTranslation } from 'react-i18next'

import { AmountField } from '@/components/AmountField'
import { CategoryPicker } from '@/components/CategoryPicker'
import { DialogFrame } from '@/components/DialogFrame'
import { OptionalMark } from '@/components/OptionalMark'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { shiftMonth, today } from '@/lib/dates'
import {
  BUDGET_SUGGESTION,
  OWN_SCOPE,
  monthText,
  type BookScope,
  type Account,
  type Category,
  type PlanPosition,
  type Transaction,
} from '@/lib/domain'
import { useAccounts, useSaveTransaction } from '@/lib/queries'

/**
 * "Buchung hinzufügen" at the top of the plan page (#241).
 *
 * The book used to carry its own quick-entry form above the list. The book is now
 * one tab of the plan page, and adding a booking is something one does from any of
 * its tabs — hence a button on the page and a dialog, not a form inside one tab.
 *
 * Picking a position makes the booking inherit its category and budget, leaving few
 * fields. Without a position the category is asked for, because a booking with no
 * purpose would sit in the book without counting anywhere.
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
        <AddBookingDialog
          accounts={usable}
          positions={positions}
          year={year}
          month={month}
          scope={scope}
          onClose={() => setOpen(false)}
        />
      )}
    </>
  )
}

function AddBookingDialog({
  accounts,
  positions,
  year,
  month,
  scope,
  onClose,
}: {
  accounts: Account[]
  positions: PlanPosition[]
  year: number
  month: number
  scope: BookScope
  onClose: () => void
}) {
  const { t } = useTranslation()
  const save = useSaveTransaction(year, month, scope)
  const fallback = accounts.find((account) => account.isDefault) ?? accounts[0]

  const [amount, setAmount] = useState('')
  const [note, setNote] = useState('')
  const [positionId, setPositionId] = useState('none')
  const [category, setCategory] = useState<Category>('household.groceries')
  const [accountId, setAccountId] = useState(fallback.id)
  const [counterAccountId, setCounterAccountId] = useState('none')
  // Offset of the plan month to the month of today, see the choice below (#239).
  const [planOffset, setPlanOffset] = useState('0')

  const chosen = positions.find((position) => position.id === positionId)
  const isTransfer = counterAccountId !== 'none'
  const bookedOn = today()
  const planMonth = shiftMonth(bookedOn, Number(planOffset))

  function submit(event: React.FormEvent) {
    event.preventDefault()
    const draft: Partial<Transaction> = {
      accountId,
      counterAccountId: isTransfer ? counterAccountId : null,
      occurredOn: bookedOn,
      amount,
      note: note || null,
      // Inherited from the position, otherwise taken from the picker. A pure
      // transfer without a position needs no purpose — there the answer is "where
      // to", not "what for".
      category: chosen ? chosen.category : isTransfer ? null : category,
      budget: chosen ? chosen.budget : isTransfer ? null : BUDGET_SUGGESTION[category],
      positionId: chosen ? chosen.id : null,
      // Only without a position and outside a transfer can the month be chosen;
      // the server rejects a choice next to either.
      ...(chosen || isTransfer
        ? {}
        : { planYear: planMonth.year, planMonth: planMonth.month }),
    }
    save.mutate(draft, { onSuccess: onClose })
  }

  return (
    <DialogFrame
      open
      onOpenChange={(open) => !open && onClose()}
      title={t('monthBook.add')}
      description={t('monthBook.hint')}
      submitLabel={isTransfer ? t('monthBook.transfer') : t('monthBook.book')}
      onSubmit={submit}
      dirty={amount !== '' || note !== ''}
      pending={save.isPending}
      error={save.error}
    >
      <div className="flex flex-col gap-4">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="book-amount" className="text-xs">
            {t('common.amount')}
          </Label>
          <AmountField id="book-amount" value={amount} onChange={setAmount} required />
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="book-note" className="text-xs">
            {t('monthBook.note')}
            <OptionalMark />
          </Label>
          <Input
            id="book-note"
            value={note}
            onChange={(event) => setNote(event.target.value)}
            placeholder={t('monthBook.notePlaceholder')}
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <Label className="text-xs">{t('monthBook.position')}</Label>
          <Select value={positionId} onValueChange={setPositionId}>
            <SelectTrigger aria-label={t('monthBook.position')}>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="none">{t('monthBook.noPosition')}</SelectItem>
              {positions.map((position) => (
                <SelectItem key={position.id} value={position.id}>
                  {position.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {/* Nur nötig, wenn kein Posten gewählt ist — sonst erbt die Buchung
            Kategorie und Budget von dort. */}
        {!chosen && !isTransfer && (
          <div className="flex flex-col gap-1.5">
            <Label className="text-xs">{t('common.category')}</Label>
            <CategoryPicker value={category} onChange={setCategory} />
          </div>
        )}

        {/* Mit Posten zählt die Buchung im Plan des Postens, eine reine Umbuchung
            im Monat des Datums — dort gibt es nichts zu wählen. */}
        {!chosen && !isTransfer && (
          <div className="flex flex-col gap-1.5">
            <Label className="text-xs">{t('monthBook.planMonthLabel')}</Label>
            <Select value={planOffset} onValueChange={setPlanOffset}>
              <SelectTrigger aria-label={t('monthBook.planMonthLabel')}>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {([-1, 0, 1] as const).map((offset) => (
                  <SelectItem key={offset} value={String(offset)}>
                    {t(
                      `monthBook.planMonthOption.${offset < 0 ? 'previous' : offset > 0 ? 'next' : 'same'}`,
                      { month: monthText(shiftMonth(bookedOn, offset), bookedOn) }
                    )}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        )}

        <div className="flex flex-col gap-1.5">
          <Label className="text-xs">{t('common.account')}</Label>
          <Select value={accountId} onValueChange={setAccountId}>
            <SelectTrigger aria-label={t('common.account')}>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {accounts.map((account) => (
                <SelectItem key={account.id} value={account.id}>
                  {account.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {/* Gesetzt = Umbuchung. Sie verschiebt den Stand zwischen zwei
            eigenen Konten und ist weder Einnahme noch Ausgabe — es sei denn,
            ein Posten ist gewählt. Geld aufs Tagesgeld legen erfüllt so die
            Sparquote, PayPal aufladen dagegen nicht. */}
        <div className="flex flex-col gap-1.5">
          <Label className="text-xs">{t('monthBook.transferTo')}</Label>
          <Select value={counterAccountId} onValueChange={setCounterAccountId}>
            <SelectTrigger aria-label={t('monthBook.transferTo')}>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="none">{t('monthBook.noTransfer')}</SelectItem>
              {accounts
                .filter((account) => account.id !== accountId)
                .map((account) => (
                  <SelectItem key={account.id} value={account.id}>
                    {account.name}
                  </SelectItem>
                ))}
            </SelectContent>
          </Select>
        </div>
      </div>
    </DialogFrame>
  )
}
