import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { AmountField } from '@/components/AmountField'
import { CategoryPicker } from '@/components/CategoryPicker'
import { Calendar } from '@/components/ui/calendar'
import { DialogFrame } from '@/components/DialogFrame'
import { MoreDetails } from '@/components/MoreDetails'
import { SentenceWord } from '@/components/SentenceWord'
import { SentencePanel } from '@/components/SentencePanel'
import { SentenceChip } from '@/components/SentenceChip'
import { fillSentence } from '@/lib/sentence'
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
import { fromIsoDay, longDate, toIsoDay } from '@/lib/dates'
import {
  BUDGET_SUGGESTION,
  categoryLabel,
  type Account,
  type Category,
  type PlanPosition,
  type Transaction,
} from '@/lib/domain'
import { OptionalMark } from '@/components/OptionalMark'

/**
 * Change one booking: amount, date, account, category, position and note.
 *
 * Only what was changed is sent, so a field the dialog does not show (the stored
 * purpose of a pure transfer, the position of a booking made by ticking off) can
 * never be overwritten by accident. The frame keeps the dialog open until the
 * server has said yes and shows a refusal inside.
 *
 * Amount stays a form; when and where read as one sentence with clickable words
 * (issue #215, decision 28). Category and the note sit behind a text link — this
 * dialog is where „Notiz“ actually lives, unlike the others' rare-fields link.
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
  onDelete = null,
  returnFocus,
}: {
  transaction: Transaction
  accounts: Account[]
  positions: PlanPosition[]
  open: boolean
  onOpenChange: (open: boolean) => void
  onSave: (changes: Partial<Transaction> & { id: string }) => void
  pending: boolean
  error: unknown
  /**
   * Absent or null: this person may not delete (rule 3). A booking is small, so
   * there is no question first (rule 7); the caller closes the dialog.
   */
  onDelete?: (() => void) | null
  /** Where the focus goes on closing, when the opener is gone (after a delete). */
  returnFocus?: () => HTMLElement | null
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
  const detailsOpened = useRef(false)

  // Which sentence word is open — only one at a time (issue #215).
  const [openWord, setOpenWord] = useState<string | null>(null)
  const wordRefs = useRef<Record<string, HTMLButtonElement | null>>({})

  const chosen = positions.find((position) => position.id === positionId)
  const isTransfer = transaction.counterAccountId !== null
  const categoryShown = !chosen && !isTransfer

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

  function toggleWord(key: string) {
    setOpenWord((current) => (current === key ? null : key))
  }

  /** Closes whichever word is open and gives the focus back to its button (rule 13). */
  function closeWord() {
    const key = openWord
    setOpenWord(null)
    if (key) requestAnimationFrame(() => wordRefs.current[key]?.focus())
  }

  function wordRef(key: string) {
    return (element: HTMLButtonElement | null) => {
      wordRefs.current[key] = element
    }
  }

  const dateWord = (
    <SentenceWord ref={wordRef('date')} open={openWord === 'date'} onClick={() => toggleWord('date')}>
      {longDate(occurredOn)}
    </SentenceWord>
  )
  const datePanel = openWord === 'date' && (
    <SentencePanel label={t('monthBook.dateLabel')}>
      <Calendar
        mode="single"
        selected={fromIsoDay(occurredOn)}
        defaultMonth={fromIsoDay(occurredOn)}
        onSelect={(date) => {
          if (!date) return
          setOccurredOn(toIsoDay(date))
          closeWord()
        }}
        autoFocus
      />
    </SentencePanel>
  )

  const accountWord = (
    <SentenceWord ref={wordRef('account')} open={openWord === 'account'} onClick={() => toggleWord('account')}>
      {accounts.find((account) => account.id === accountId)?.name ?? ''}
    </SentenceWord>
  )
  const accountPanel = openWord === 'account' && (
    <SentencePanel label={t('monthBook.accountLabel')}>
      <div className="flex flex-wrap gap-2">
        {accounts.map((account) => (
          <SentenceChip
            key={account.id}
            selected={accountId === account.id}
            onClick={() => {
              setAccountId(account.id)
              closeWord()
            }}
          >
            {account.name}
          </SentenceChip>
        ))}
      </div>
    </SentencePanel>
  )

  // A booking made by ticking off cannot be moved off its position (its own
  // dialog is where that happens) — named, not offered as a word to click.
  const positionWord = transaction.autoBooked ? (
    <span className="font-medium">{chosen?.label ?? t('monthBook.noPosition')}</span>
  ) : (
    <SentenceWord
      ref={wordRef('position')}
      open={openWord === 'position'}
      onClick={() => toggleWord('position')}
    >
      {chosen?.label ?? t('monthBook.noPosition')}
    </SentenceWord>
  )
  const positionPanel = !transaction.autoBooked && openWord === 'position' && (
    <SentencePanel label={t('monthBook.positionLabel')}>
      <Select
        value={positionId}
        onValueChange={(value) => {
          setPositionId(value)
          closeWord()
        }}
      >
        <SelectTrigger aria-label={t('monthBook.positionLabel')}>
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
    </SentencePanel>
  )

  // A quiet line under the link when something rare is already set — the section
  // itself stays collapsed regardless (same rule as the other dialogs).
  const extrasSummary = [
    categoryShown &&
      category !== (transaction.category ?? 'household.groceries') &&
      t('common.extrasSummary.category', { value: categoryLabel(category) }),
    note && t('monthBook.note') + ': ' + note,
  ]
    .filter((part): part is string => Boolean(part))
    .join(' · ')

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
      returnFocus={returnFocus}
      start={
        onDelete !== null ? (
          <Button
            type="button"
            variant="ghost"
            className="text-destructive hover:text-destructive"
            disabled={pending}
            onClick={onDelete}
          >
            {t('common.delete')}
          </Button>
        ) : undefined
      }
    >
      <div className="flex flex-col gap-6">
        <div className="flex flex-col gap-2">
          <Label htmlFor="edit-amount" className="text-muted-foreground text-sm">
            {t('common.amount')}
          </Label>
          <AmountField
            id="edit-amount"
            value={amount}
            onChange={setAmount}
            required
            inputClassName="h-auto border-0 bg-transparent px-0 pr-7 text-2xl font-semibold placeholder:text-muted-foreground md:text-2xl"
          />
        </div>

        <div
          className="flex flex-col gap-3"
          onKeyDownCapture={(event) => {
            if (event.key !== 'Escape' || openWord === null) return
            event.stopPropagation()
            event.preventDefault()
            closeWord()
          }}
        >
          <p className="text-lg leading-8">
            {fillSentence(t('monthBook.sentence'), {
              date: dateWord,
              account: accountWord,
              position: positionWord,
            })}
          </p>
          {datePanel}
          {accountPanel}
          {positionPanel}
        </div>

        <MoreDetails
          resetKey={transaction}
          hasValues={false}
          startOpen={detailsOpened.current}
          onToggle={(opened) => {
            detailsOpened.current = opened
          }}
          label={t('monthBook.addDetails')}
          plain
          summary={extrasSummary || undefined}
        >
          {categoryShown && (
            <div className="flex flex-col gap-2">
              <Label>{t('common.category')}</Label>
              <CategoryPicker value={category} onChange={setCategory} />
            </div>
          )}

          <div className="flex flex-col gap-2">
            <Label htmlFor="edit-note">{t('monthBook.note')}<OptionalMark /></Label>
            <Input id="edit-note" value={note} onChange={(event) => setNote(event.target.value)} />
          </div>
        </MoreDetails>
      </div>
    </DialogFrame>
  )
}
