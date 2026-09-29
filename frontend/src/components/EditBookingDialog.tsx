import { useId, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { AmountField } from '@/components/AmountField'
import { CategoryPicker } from '@/components/CategoryPicker'
import { Calendar } from '@/components/ui/calendar'
import { DialogFrame } from '@/components/DialogFrame'
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
import {
  fromIsoDay,
  longDate,
  monthOffset,
  shiftMonth,
  toIsoDay,
  type YearMonth,
} from '@/lib/dates'
import {
  BUDGET_SUGGESTION,
  categoryLabel,
  monthText,
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
 *
 * Amount stays a form; everything else reads as two sentences with clickable
 * words — when and where, then category and note (issue #215, decisions 28 and
 * D-215-4: rare facts read as a sentence too, not a collapsed link).
 */
export function EditBookingDialog({
  transaction,
  accounts,
  positions,
  viewedMonth,
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
  /** The plan month the positions belong to — where a booking with a position counts. */
  viewedMonth: YearMonth
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
  // Null until the person picks a month: then the server keeps the choice the
  // booking had relative to its date, also when the date moves (#239).
  const [planChoice, setPlanChoice] = useState<YearMonth | null>(null)

  // Which sentence word is open — only one at a time (issue #215).
  const [openWord, setOpenWord] = useState<string | null>(null)
  const wordRefs = useRef<Record<string, HTMLButtonElement | null>>({})
  // Read out with every word and every opened field, so a screen reader hears the
  // whole sentence, not just the one word (issue #202, review D-215-3, fix 1).
  const sentenceId = useId()
  // The quiet second sentence for category and note (issue #215, review D-215-4).
  const extrasSentenceId = useId()

  const chosen = positions.find((position) => position.id === positionId)
  const isTransfer = transaction.counterAccountId !== null
  const categoryShown = !chosen && !isTransfer

  // Where the booking counts: with a position, in that position's plan; a pure
  // transfer in the month of its date; otherwise the choice — previous, own or next
  // month — which keeps its distance to the date when the date moves.
  const planFixed = Boolean(chosen) || isTransfer
  const storedOffset = monthOffset(transaction.occurredOn, {
    year: transaction.planYear,
    month: transaction.planMonth,
  })
  const planMonth: YearMonth = chosen
    ? viewedMonth
    : isTransfer
      ? shiftMonth(occurredOn, 0)
      : (planChoice ?? shiftMonth(occurredOn, Math.abs(storedOffset) <= 1 ? storedOffset : 0))

  /** Everything that differs from the stored booking. */
  function changes(): Partial<Transaction> {
    const diff: Partial<Transaction> = {}
    if (amount !== transaction.amount) diff.amount = amount
    if (occurredOn !== transaction.occurredOn) diff.occurredOn = occurredOn
    if (accountId !== transaction.accountId) diff.accountId = accountId
    if ((note || null) !== transaction.note) diff.note = note || null
    if ((chosen?.id ?? null) !== transaction.positionId) diff.positionId = chosen?.id ?? null
    if (planChoice && !planFixed) {
      diff.planYear = planChoice.year
      diff.planMonth = planChoice.month
    }

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
    <SentenceWord
      ref={wordRef('date')}
      open={openWord === 'date'}
      onClick={() => toggleWord('date')}
      describedBy={sentenceId}
    >
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
        aria-describedby={sentenceId}
        autoFocus
      />
    </SentencePanel>
  )

  const accountWord = (
    <SentenceWord
      ref={wordRef('account')}
      open={openWord === 'account'}
      onClick={() => toggleWord('account')}
      describedBy={sentenceId}
    >
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
  const planMonthText = monthText(planMonth, occurredOn)
  const planMonthWord = planFixed ? (
    <span className="font-medium">{planMonthText}</span>
  ) : (
    <SentenceWord
      ref={wordRef('planMonth')}
      open={openWord === 'planMonth'}
      onClick={() => toggleWord('planMonth')}
      describedBy={sentenceId}
    >
      {planMonthText}
    </SentenceWord>
  )
  const planMonthPanel = !planFixed && openWord === 'planMonth' && (
    <SentencePanel label={t('monthBook.planMonthLabel')}>
      <div className="flex flex-wrap gap-2">
        {([-1, 0, 1] as const).map((offset) => {
          const option = shiftMonth(occurredOn, offset)
          const kind = offset < 0 ? 'previous' : offset > 0 ? 'next' : 'same'
          return (
            <SentenceChip
              key={offset}
              selected={option.year === planMonth.year && option.month === planMonth.month}
              onClick={() => {
                setPlanChoice(option)
                closeWord()
              }}
            >
              {t(`monthBook.planMonthOption.${kind}`, {
                month: monthText(option, occurredOn),
              })}
            </SentenceChip>
          )
        })}
      </div>
    </SentencePanel>
  )

  const positionWord = transaction.autoBooked ? (
    <span className="font-medium">{chosen?.label ?? t('monthBook.noPosition')}</span>
  ) : (
    <SentenceWord
      ref={wordRef('position')}
      open={openWord === 'position'}
      onClick={() => toggleWord('position')}
      describedBy={sentenceId}
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

  const categoryWord = (
    <SentenceWord
      ref={wordRef('category')}
      open={openWord === 'category'}
      onClick={() => toggleWord('category')}
      describedBy={extrasSentenceId}
    >
      {categoryLabel(category)}
    </SentenceWord>
  )
  const categoryPanel = openWord === 'category' && (
    <SentencePanel label={t('common.category')}>
      <CategoryPicker value={category} onChange={setCategory} />
    </SentencePanel>
  )

  const noteWord = (
    <SentenceWord
      ref={wordRef('note')}
      open={openWord === 'note'}
      onClick={() => toggleWord('note')}
      describedBy={extrasSentenceId}
    >
      {note ? t('monthBook.notedWith', { note }) : t('monthBook.noNote')}
    </SentenceWord>
  )
  const notePanel = openWord === 'note' && (
    <SentencePanel label={t('monthBook.note')}>
      <Label htmlFor="edit-note" className="sr-only">
        {t('monthBook.note')}
      </Label>
      <Input
        id="edit-note"
        value={note}
        onChange={(event) => setNote(event.target.value)}
        placeholder={t('monthBook.notePlaceholder')}
        aria-describedby={extrasSentenceId}
      />
    </SentencePanel>
  )

  const extrasWords: Record<string, React.ReactNode> = { category: categoryWord, note: noteWord }
  const extrasPanels = [categoryPanel, notePanel]
  const extrasSentenceKey = categoryShown ? 'withCategory' : 'plain'

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
            inputClassName="h-auto border-0 bg-transparent px-0 pr-7 text-xl font-semibold placeholder:text-muted-foreground md:text-xl"
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
          <p id={sentenceId} className="text-base leading-relaxed">
            {fillSentence(t('monthBook.sentence'), {
              date: dateWord,
              account: accountWord,
              position: positionWord,
              planMonth: planMonthWord,
            })}
          </p>
          {datePanel}
          {accountPanel}
          {positionPanel}
          {planMonthPanel}
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
          <p id={extrasSentenceId} className="text-muted-foreground text-base leading-relaxed">
            {fillSentence(t(`monthBook.extrasSentence.${extrasSentenceKey}`), extrasWords)}
          </p>
          {extrasPanels}
        </div>
      </div>
    </DialogFrame>
  )
}
