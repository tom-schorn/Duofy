import { useState } from 'react'
import { useTranslation } from 'react-i18next'

import { AmountField } from '@/components/AmountField'
import { DialogFrame } from '@/components/DialogFrame'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import { euro, type Account, type Transaction } from '@/lib/domain'
import {
  useCarryOverSuggestion,
  useDeleteTransaction,
  useSaveTransaction,
} from '@/lib/queries'

/**
 * The carry-over of the default account for one month (#94): where the account
 * goes into the month. Optional — without one the curve starts at zero.
 *
 * Set, change and remove all happen here; the book only lists it. The dialog
 * suggests the book balance at the end of the month before, and the person takes
 * it or types the balance from the bank statement instead.
 */
export function CarryOverCard({
  account,
  carryOver,
  year,
  month,
}: {
  account: Account
  carryOver: Transaction | undefined
  year: number
  month: number
}) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)

  return (
    <Card className="flex-row flex-wrap items-center justify-between gap-3 px-5 py-4">
      <div className="flex flex-col gap-0.5">
        <p className="font-medium">
          {carryOver
            ? t('carryOver.set', {
                account: account.name,
                amount: euro.format(Number(carryOver.amount)),
              })
            : t('carryOver.notSet', { account: account.name })}
        </p>
        <p className="text-muted-foreground text-sm">
          {carryOver ? t('carryOver.setHint') : t('carryOver.notSetHint')}
        </p>
      </div>
      <Button variant="outline" onClick={() => setOpen(true)}>
        {carryOver ? t('carryOver.change') : t('carryOver.setButton')}
      </Button>
      {open && (
        <CarryOverDialog
          account={account}
          carryOver={carryOver}
          year={year}
          month={month}
          onClose={() => setOpen(false)}
        />
      )}
    </Card>
  )
}

function CarryOverDialog({
  account,
  carryOver,
  year,
  month,
  onClose,
}: {
  account: Account
  carryOver: Transaction | undefined
  year: number
  month: number
  onClose: () => void
}) {
  const { t } = useTranslation()
  const suggestion = useCarryOverSuggestion(
    carryOver ? undefined : account.id,
    year,
    month
  )
  const save = useSaveTransaction(year, month)
  const remove = useDeleteTransaction(year, month)
  const [typed, setTyped] = useState<string | null>(null)

  // Until the person types, the field shows what is stored, or the suggestion.
  const amount = typed ?? carryOver?.amount ?? suggestion.data?.amount ?? ''
  const firstOfMonth = `${year}-${String(month).padStart(2, '0')}-01`

  function submit(event: React.SyntheticEvent) {
    event.preventDefault()
    if (amount === '') return
    save.mutate(
      carryOver
        ? { id: carryOver.id, amount }
        : {
            kind: 'carry_over',
            accountId: account.id,
            occurredOn: firstOfMonth,
            amount,
          },
      { onSuccess: onClose }
    )
  }

  return (
    <DialogFrame
      open
      onOpenChange={(next) => !next && onClose()}
      title={t('carryOver.title', { account: account.name })}
      description={t('carryOver.description')}
      submitLabel={t('common.save')}
      onSubmit={submit}
      submitDisabled={amount === ''}
      dirty={typed !== null}
      pending={save.isPending}
      error={save.error}
      start={
        carryOver ? (
          <Button
            type="button"
            variant="ghost"
            className="text-destructive hover:text-destructive"
            disabled={save.isPending}
            onClick={() => {
              remove(carryOver)
              onClose()
            }}
          >
            {t('common.delete')}
          </Button>
        ) : undefined
      }
    >
      <div className="flex flex-col gap-2">
        <Label htmlFor="carry-over-amount">{t('carryOver.amount')}</Label>
        <AmountField
          id="carry-over-amount"
          value={amount}
          onChange={setTyped}
          required
          allowZero
          allowNegative
        />
        {!carryOver && suggestion.data && (
          <p className="text-muted-foreground text-sm">
            {t('carryOver.suggested', {
              amount: euro.format(Number(suggestion.data.amount)),
            })}
          </p>
        )}
      </div>
    </DialogFrame>
  )
}
