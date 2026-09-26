import { useState } from 'react'
import { useTranslation } from 'react-i18next'

import { CategoryPicker } from '@/components/CategoryPicker'
import { DateField } from '@/components/DateField'
import { DialogFrame } from '@/components/DialogFrame'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
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
 * Only what was changed is sent, so a field the dialog does not show (the stored
 * purpose of a pure transfer, the position of a booking made by ticking off) can
 * never be overwritten by accident. The frame keeps the dialog open until the
 * server has said yes and shows a refusal inside.
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
  onSave: (changes: Partial<Transaction> & { id: string }) => void
  pending: boolean
  error: unknown
}) {
  const { t } = useTranslation()
  const [amount, setAmount] = useState(transaction.amount)
  const [occurredOn, setOccurredOn] = useState(transaction.occurredOn)
  const [accountId, setAccountId] = useState(transaction.accountId)
  // The value the picker shows, so what is shown is what is saved.
  const [category, setCategory] = useState<Category>(
    transaction.category ?? 'household.groceries'
  )
  const [positionId, setPositionId] = useState(transaction.positionId ?? 'none')
  const [note, setNote] = useState(transaction.note ?? '')

  const chosen = positions.find((position) => position.id === positionId)
  const isTransfer = transaction.counterAccountId !== null

  /** Everything that differs from the stored booking. */
  function changes(): Partial<Transaction> {
    const diff: Partial<Transaction> = {}
    if (amount !== transaction.amount) diff.amount = amount
    if (occurredOn !== transaction.occurredOn) diff.occurredOn = occurredOn
    if (accountId !== transaction.accountId) diff.accountId = accountId
    if ((note || null) !== transaction.note) diff.note = note || null
    if ((chosen?.id ?? null) !== transaction.positionId) diff.positionId = chosen?.id ?? null

    // As in the quick entry: a position gives the booking its category and budget,
    // otherwise the picked category decides. A pure transfer has neither, and one
    // with no purpose to change is left as it is.
    const purpose = chosen
      ? { category: chosen.category, budget: chosen.budget }
      : isTransfer
        ? null
        : { category, budget: BUDGET_SUGGESTION[category] }
    if (
      purpose &&
      (purpose.category !== transaction.category || purpose.budget !== transaction.budget)
    ) {
      diff.category = purpose.category
      diff.budget = purpose.budget
    }
    return diff
  }

  const diff = changes()
  const dirty = Object.keys(diff).length > 0

  function submit(event: React.SyntheticEvent) {
    event.preventDefault()
    // Nothing changed: nothing to ask the server.
    if (!dirty) return onOpenChange(false)
    onSave({ id: transaction.id, ...diff })
  }

  return (
    <DialogFrame
      open={open}
      onOpenChange={onOpenChange}
      title={t('monthBook.editTitle')}
      description={t('monthBook.editHint')}
      submitLabel={t('common.save')}
      onSubmit={submit}
      dirty={dirty}
      pending={pending}
      error={error}
    >
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
          <CategoryPicker value={category} onChange={setCategory} />
        </div>
      )}

      <div className="flex flex-col gap-2">
        <Label htmlFor="edit-note">{t('monthBook.note')}</Label>
        <Input id="edit-note" value={note} onChange={(event) => setNote(event.target.value)} />
      </div>
    </DialogFrame>
  )
}
