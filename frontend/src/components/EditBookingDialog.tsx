import { useState } from 'react'
import { useTranslation } from 'react-i18next'

import { CategoryPicker } from '@/components/CategoryPicker'
import { DateField } from '@/components/DateField'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { errorText } from '@/lib/api'
import {
  BUDGET_SUGGESTION,
  type Account,
  type Category,
  type PlanPosition,
  type Transaction,
} from '@/lib/domain'

/**
 * Change one booking: amount, date, account, category, position and note.
 *
 * The dialog stays open until the server has said yes (the caller closes it on
 * success); a refusal is shown inside. A booking made by ticking off keeps its
 * position — the backend refuses to detach it, so the field is locked. A plain
 * dialog for now; it moves onto the shared dialog frame once that exists.
 */
export function EditBookingDialog({
  transaction,
  accounts,
  positions,
  open,
  onOpenChange,
  onSave,
  pending,
  error,
}: {
  transaction: Transaction
  accounts: Account[]
  positions: PlanPosition[]
  open: boolean
  onOpenChange: (open: boolean) => void
  onSave: (draft: Partial<Transaction> & { id: string }) => void
  pending: boolean
  error: unknown
}) {
  const { t } = useTranslation()
  const [amount, setAmount] = useState(transaction.amount)
  const [occurredOn, setOccurredOn] = useState(transaction.occurredOn)
  const [accountId, setAccountId] = useState(transaction.accountId)
  const [category, setCategory] = useState<Category | null>(transaction.category)
  const [positionId, setPositionId] = useState(transaction.positionId ?? 'none')
  const [note, setNote] = useState(transaction.note ?? '')

  const chosen = positions.find((position) => position.id === positionId)
  const isTransfer = transaction.counterAccountId !== null

  function submit(event: React.FormEvent) {
    event.preventDefault()
    // As in the quick entry: a position gives the booking its category and budget,
    // otherwise the picked category decides. A pure transfer has neither.
    const purpose = chosen
      ? { category: chosen.category, budget: chosen.budget }
      : !isTransfer && category
        ? { category, budget: BUDGET_SUGGESTION[category] }
        : null
    onSave({
      id: transaction.id,
      accountId,
      occurredOn,
      amount,
      note: note || null,
      category: purpose?.category ?? null,
      budget: purpose?.budget ?? null,
      positionId: chosen ? chosen.id : null,
    })
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <form onSubmit={submit} className="flex flex-col gap-5">
          <DialogHeader>
            <DialogTitle className="font-heading text-xl">
              {t('monthBook.editTitle')}
            </DialogTitle>
            <DialogDescription>{t('monthBook.editHint')}</DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-2">
              <Label htmlFor="edit-amount">{t('common.amount')}</Label>
              <Input
                id="edit-amount"
                type="number"
                step="0.01"
                min="0.01"
                inputMode="decimal"
                value={amount}
                onChange={(event) => setAmount(event.target.value)}
                required
              />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="edit-date">{t('common.date')}</Label>
              <DateField id="edit-date" value={occurredOn} onChange={setOccurredOn} />
            </div>
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="edit-account">{t('common.account')}</Label>
            <Select value={accountId} onValueChange={setAccountId}>
              <SelectTrigger id="edit-account">
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

          <div className="flex flex-col gap-2">
            <Label htmlFor="edit-position">{t('monthBook.position')}</Label>
            <Select
              value={positionId}
              onValueChange={setPositionId}
              disabled={transaction.autoBooked}
            >
              <SelectTrigger id="edit-position">
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

          {!chosen && !isTransfer && (
            <div className="flex flex-col gap-2">
              <Label>{t('common.category')}</Label>
              <CategoryPicker
                value={category ?? 'household.groceries'}
                onChange={setCategory}
              />
            </div>
          )}

          <div className="flex flex-col gap-2">
            <Label htmlFor="edit-note">{t('monthBook.note')}</Label>
            <Input
              id="edit-note"
              value={note}
              onChange={(event) => setNote(event.target.value)}
            />
          </div>

          {Boolean(error) && (
            <p role="alert" className="text-destructive text-sm">
              {errorText(error)}
            </p>
          )}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              {t('common.cancel')}
            </Button>
            <Button type="submit" disabled={pending}>
              {pending ? t('monthBook.pending') : t('common.save')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
