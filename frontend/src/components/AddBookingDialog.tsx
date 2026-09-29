import { useId, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { AmountField } from '@/components/AmountField'
import { CategoryPicker } from '@/components/CategoryPicker'
import { DialogFrame } from '@/components/DialogFrame'
import { SentenceChip } from '@/components/SentenceChip'
import { SentencePanel } from '@/components/SentencePanel'
import { SentenceWord } from '@/components/SentenceWord'
import { Calendar } from '@/components/ui/calendar'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { fromIsoDay, longDate, shiftMonth, toIsoDay, today, type YearMonth } from '@/lib/dates'
import {
  BUDGET_SUGGESTION,
  budgetLabel,
  categoryLabel,
  monthText,
  type Account,
  type BookScope,
  type Category,
  type PlanPosition,
  type Transaction,
} from '@/lib/domain'
import { fillSentence } from '@/lib/sentence'
import { useSaveTransaction } from '@/lib/queries'

/**
 * Add one booking, as a sentence like every other dialog (decision 28):
 * what and how much on top, then "Bezahlt am [Datum] vom [Konto], zählt im Plan
 * [Monat] auf [Posten]." with each bracket a word that opens its choice, and a
 * quiet second sentence for the category and a transfer.
 *
 * Picking a position makes the booking inherit its category and budget and count in
 * that position's plan. Without one the category is asked for, because a booking
 * with no purpose would sit in the book without counting anywhere — and the plan
 * month is a choice: previous, own or next month of the date (#239).
 */
export function AddBookingDialog({
  accounts,
  positions,
  viewedMonth,
  scope,
  onClose,
}: {
  accounts: Account[]
  positions: PlanPosition[]
  /** The plan month the positions belong to — where a booking with a position counts. */
  viewedMonth: YearMonth
  scope: BookScope
  onClose: () => void
}) {
  const { t } = useTranslation()
  const save = useSaveTransaction(viewedMonth.year, viewedMonth.month, scope)
  const fallback = accounts.find((account) => account.isDefault) ?? accounts[0]

  const [amount, setAmount] = useState('')
  const [note, setNote] = useState('')
  const [occurredOn, setOccurredOn] = useState(today())
  const [accountId, setAccountId] = useState(fallback.id)
  const [positionId, setPositionId] = useState('none')
  const [category, setCategory] = useState<Category>('household.groceries')
  const [counterAccountId, setCounterAccountId] = useState('none')
  // Null until the person picks a month: then it is the month of the date.
  const [planChoice, setPlanChoice] = useState<YearMonth | null>(null)

  // Which sentence word is open — only one at a time (issue #215).
  const [openWord, setOpenWord] = useState<string | null>(null)
  const wordRefs = useRef<Record<string, HTMLButtonElement | null>>({})
  const sentenceId = useId()
  const extrasSentenceId = useId()

  const chosen = positions.find((position) => position.id === positionId)
  const isTransfer = counterAccountId !== 'none'
  const planFixed = Boolean(chosen) || isTransfer
  const planMonth: YearMonth = chosen
    ? viewedMonth
    : isTransfer
      ? shiftMonth(occurredOn, 0)
      : (planChoice ?? shiftMonth(occurredOn, 0))

  function submit(event: React.SyntheticEvent) {
    event.preventDefault()
    const draft: Partial<Transaction> = {
      accountId,
      counterAccountId: isTransfer ? counterAccountId : null,
      occurredOn,
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
      ...(planFixed ? {} : { planYear: planMonth.year, planMonth: planMonth.month }),
    }
    save.mutate(draft, { onSuccess: onClose })
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

  const word = (key: string, describedBy: string, text: React.ReactNode) => (
    <SentenceWord
      ref={wordRef(key)}
      open={openWord === key}
      onClick={() => toggleWord(key)}
      describedBy={describedBy}
    >
      {text}
    </SentenceWord>
  )

  const unplannedText = t('monthBook.unplannedIn', {
    budget: budgetLabel(BUDGET_SUGGESTION[category]),
  })
  const positionText = chosen
    ? chosen.label
    : isTransfer
      ? t('monthBook.noPosition')
      : unplannedText
  const counter = accounts.find((account) => account.id === counterAccountId)
  const planMonthText = monthText(planMonth, occurredOn)

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

  const accountPanel = openWord === 'account' && (
    <SentencePanel label={t('monthBook.accountLabel')}>
      <div className="flex flex-wrap gap-2">
        {accounts.map((account) => (
          <SentenceChip
            key={account.id}
            selected={accountId === account.id}
            onClick={() => {
              setAccountId(account.id)
              // A transfer to the account it comes from makes no sense.
              if (counterAccountId === account.id) setCounterAccountId('none')
              closeWord()
            }}
          >
            {account.name}
          </SentenceChip>
        ))}
      </div>
    </SentencePanel>
  )

  // With a position or a transfer the month is decided elsewhere — named, not
  // offered as a word to click.
  const planMonthWord = planFixed ? (
    <span className="font-medium">{planMonthText}</span>
  ) : (
    word('planMonth', sentenceId, planMonthText)
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

  const positionPanel = openWord === 'position' && (
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
          <SelectItem value="none">{t('monthBook.unplanned')}</SelectItem>
          {positions.map((position) => (
            <SelectItem key={position.id} value={position.id}>
              {position.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </SentencePanel>
  )

  const categoryShown = !chosen && !isTransfer
  const categoryPanel = openWord === 'category' && (
    <SentencePanel label={t('common.category')}>
      <CategoryPicker value={category} onChange={setCategory} />
    </SentencePanel>
  )

  // A transfer moves money between two of one's own accounts: neither income nor
  // spending, unless a position is chosen — putting money on the savings account
  // then fulfils the savings quota.
  const transferPanel = openWord === 'transfer' && (
    <SentencePanel label={t('monthBook.transferTo')}>
      <div className="flex flex-wrap gap-2">
        <SentenceChip
          selected={counterAccountId === 'none'}
          onClick={() => {
            setCounterAccountId('none')
            closeWord()
          }}
        >
          {t('monthBook.noTransfer')}
        </SentenceChip>
        {accounts
          .filter((account) => account.id !== accountId)
          .map((account) => (
            <SentenceChip
              key={account.id}
              selected={counterAccountId === account.id}
              onClick={() => {
                setCounterAccountId(account.id)
                closeWord()
              }}
            >
              {account.name}
            </SentenceChip>
          ))}
      </div>
    </SentencePanel>
  )

  const extrasWords: Record<string, React.ReactNode> = {
    category: word('category', extrasSentenceId, categoryLabel(category)),
    transfer: word(
      'transfer',
      extrasSentenceId,
      counter ? t('monthBook.transferWord', { account: counter.name }) : t('monthBook.noTransfer')
    ),
  }

  // Escape closes the open word first, the dialog only on a second press.
  const escapeWord = (event: React.KeyboardEvent) => {
    if (event.key !== 'Escape' || openWord === null) return
    event.stopPropagation()
    event.preventDefault()
    closeWord()
  }

  return (
    <DialogFrame
      open
      onOpenChange={(open) => !open && onClose()}
      title={t('monthBook.add')}
      description={t('monthBook.addHint')}
      submitLabel={isTransfer ? t('monthBook.transfer') : t('monthBook.book')}
      onSubmit={submit}
      dirty={amount !== '' || note !== ''}
      pending={save.isPending}
      error={save.error}
    >
      <div className="flex flex-col gap-6">
        <div className="flex flex-col gap-3">
          <Label htmlFor="add-note" className="sr-only">
            {t('monthBook.what')}
          </Label>
          <Input
            id="add-note"
            value={note}
            onChange={(event) => setNote(event.target.value)}
            placeholder={t('monthBook.what')}
            className="h-auto border-0 border-b bg-transparent px-0 text-xl shadow-none focus-visible:ring-0 md:text-xl"
          />
          <div className="flex flex-col gap-2">
            <Label htmlFor="add-amount" className="text-muted-foreground text-sm">
              {t('common.amount')}
            </Label>
            <AmountField
              id="add-amount"
              value={amount}
              onChange={setAmount}
              required
              inputClassName="h-auto border-0 bg-transparent px-0 pr-7 text-xl font-semibold placeholder:text-muted-foreground md:text-xl"
            />
          </div>
        </div>

        <div className="flex flex-col gap-3" onKeyDownCapture={escapeWord}>
          <p id={sentenceId} className="text-base leading-relaxed">
            {fillSentence(t('monthBook.addSentence'), {
              date: word('date', sentenceId, longDate(occurredOn)),
              account: word(
                'account',
                sentenceId,
                accounts.find((account) => account.id === accountId)?.name ?? ''
              ),
              planMonth: planMonthWord,
              position: word('position', sentenceId, positionText),
            })}
          </p>
          {datePanel}
          {accountPanel}
          {planMonthPanel}
          {positionPanel}
        </div>

        <div className="flex flex-col gap-3" onKeyDownCapture={escapeWord}>
          <p id={extrasSentenceId} className="text-muted-foreground text-base leading-relaxed">
            {fillSentence(
              t(categoryShown ? 'monthBook.addExtras.withCategory' : 'monthBook.addExtras.plain'),
              extrasWords
            )}
          </p>
          {categoryShown && categoryPanel}
          {transferPanel}
        </div>
      </div>
    </DialogFrame>
  )
}
