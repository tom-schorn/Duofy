import { useId, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { AmountField } from '@/components/AmountField'
import { CategoryPicker } from '@/components/CategoryPicker'
import { DialogFrame } from '@/components/DialogFrame'
import type { PositionMonth } from '@/components/PositionPicker'
import { SentenceChip } from '@/components/SentenceChip'
import { SentencePanel } from '@/components/SentencePanel'
import { SentenceWord } from '@/components/SentenceWord'
import { Button } from '@/components/ui/button'
import { Calendar } from '@/components/ui/calendar'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  fromIsoDay,
  longDate,
  monthOffset,
  shiftMonth,
  toIsoDay,
  today,
  type YearMonth,
} from '@/lib/dates'
import {
  BUDGET_SUGGESTION,
  budgetLabel,
  categoryLabel,
  monthLabel,
  monthText,
  type Account,
  type BookScope,
  type Category,
  type PlanPosition,
  type Transaction,
} from '@/lib/domain'
import { useSaveTransaction } from '@/lib/queries'
import { fillSentence } from '@/lib/sentence'

/**
 * How the booking dialog starts (#254). The fields are the same in every case —
 * amount, date, account, plan month, position — only what is prefilled, what is
 * fixed and where the result goes differ.
 */
export type BookingStart =
  /** A new booking; the dialog saves it itself and closes. */
  | { kind: 'new'; scope: BookScope }
  /** A stored booking; only what changed is handed to `onSave`. */
  | {
      kind: 'edit'
      transaction: Transaction
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
      /** The sentence word that starts open, e.g. `position` when assigning (#241). */
      startWord?: string | null
    }
  /**
   * Ticking a position off: the tick creates the booking, the dialog only asks for
   * date and amount. Position and plan month are the position's and stay fixed —
   * ticking off an August position with a July date gives a July booking on an
   * August position, which is exactly what is meant.
   */
  | {
      kind: 'tick'
      position: PlanPosition
      onConfirm: (values: { occurredOn: string; amount: string }) => void
      pending: boolean
      /** The sentence for a rejected tick; the dialog stays open and shows it. */
      error: string | null
      /**
       * The position already has bookings. The tick then books nothing more, so date
       * and amount would be ignored — the fields are disabled and say so.
       */
      hasBookings: boolean
      /** The bookings could not be loaded, so nobody knows whether date and amount count. */
      bookingsUnknown: boolean
    }

type Props = {
  accounts: Account[]
  /** The positions a booking may hang on, month by month. */
  positions: PositionMonth[]
  /** The plan month the dialog was opened from. */
  viewedMonth: YearMonth
  onClose: () => void
  start: BookingStart
}

/**
 * One booking, as a sentence like every other dialog (decision 28): amount on top,
 * then "Bezahlt am [Datum] vom [Konto], zählt im Plan [Monat] auf [Posten]." with
 * each bracket a word that opens its choice, and a quiet second sentence for the
 * rarer facts.
 *
 * Replaces the three dialogs for adding, changing and ticking off (#254): they
 * asked the same questions in three slightly different ways.
 */
export function BookingDialog(props: Props) {
  if (props.start.kind === 'new') return <NewBooking {...props} scope={props.start.scope} />
  return <BookingForm {...props} submitNew={null} />
}

/** The new start saves for itself, so only it needs the query client. */
function NewBooking({ scope, ...props }: Props & { scope: BookScope }) {
  const save = useSaveTransaction(props.viewedMonth.year, props.viewedMonth.month, scope)
  return (
    <BookingForm
      {...props}
      submitNew={{
        save: (draft) => save.mutate(draft, { onSuccess: props.onClose }),
        pending: save.isPending,
        error: save.error,
      }}
    />
  )
}

type NewSubmit = {
  save: (draft: Partial<Transaction>) => void
  pending: boolean
  error: unknown
}

function BookingForm({
  accounts,
  positions,
  viewedMonth,
  onClose,
  start,
  submitNew,
}: Props & { submitNew: NewSubmit | null }) {
  const { t } = useTranslation()
  const edit = start.kind === 'edit' ? start : null
  const tick = start.kind === 'tick' ? start : null
  const stored = edit?.transaction ?? null
  const fallback = accounts.find((account) => account.isDefault) ?? accounts[0]

  const [amount, setAmount] = useState(
    stored?.amount ?? tick?.position.amountPlanned ?? ''
  )
  const [note, setNote] = useState(stored?.note ?? '')
  const [occurredOn, setOccurredOn] = useState(stored?.occurredOn ?? today())
  const [accountId, setAccountId] = useState<string | null>(
    stored ? stored.accountId : tick ? tick.position.accountId : (fallback?.id ?? null)
  )
  // The value the picker shows, so what is shown is what is saved.
  const [category, setCategory] = useState<Category>(
    stored?.category ?? 'household.groceries'
  )
  const [positionId, setPositionId] = useState(
    stored?.positionId ?? tick?.position.id ?? 'none'
  )
  const [counterAccountId, setCounterAccountId] = useState('none')
  // Null until the person picks a month: then it is the month of the date, or for
  // a stored booking the distance it had to its date (#239).
  const [planChoice, setPlanChoice] = useState<YearMonth | null>(null)

  // Which sentence word is open — only one at a time (issue #215).
  const [openWord, setOpenWord] = useState<string | null>(edit?.startWord ?? null)
  const wordRefs = useRef<Record<string, HTMLButtonElement | null>>({})
  // Read out with every word and every opened field, so a screen reader hears the
  // whole sentence, not just the one word (issue #202, review D-215-3, fix 1).
  const sentenceId = useId()
  const extrasSentenceId = useId()

  const allPositions = positions.flatMap((entry) => entry.positions)
  const chosen = tick
    ? tick.position
    : allPositions.find((position) => position.id === positionId)
  /** The plan a position belongs to — the viewed one unless it came from another month. */
  const monthOf = (position: PlanPosition): YearMonth => {
    const entry = positions.find((month) => month.positions.some((p) => p.id === position.id))
    return entry ? { year: entry.year, month: entry.month } : viewedMonth
  }

  // A stored transfer stays one; only a new booking can become one.
  const isTransfer = stored ? stored.counterAccountId !== null : counterAccountId !== 'none'
  // Where the booking counts: with a position, in that position's plan; a pure
  // transfer in the month of its date; otherwise the choice — previous, own or next
  // month.
  const planFixed = Boolean(chosen) || isTransfer
  const storedOffset = stored
    ? monthOffset(stored.occurredOn, { year: stored.planYear, month: stored.planMonth })
    : 0
  const planMonth: YearMonth = chosen
    ? monthOf(chosen)
    : isTransfer
      ? shiftMonth(occurredOn, 0)
      : (planChoice ?? shiftMonth(occurredOn, Math.abs(storedOffset) <= 1 ? storedOffset : 0))

  /** A stored booking: everything that differs from it. */
  function changes(transaction: Transaction): Partial<Transaction> {
    const diff: Partial<Transaction> = {}
    if (amount !== transaction.amount) diff.amount = amount
    if (occurredOn !== transaction.occurredOn) diff.occurredOn = occurredOn
    if (accountId !== null && accountId !== transaction.accountId) diff.accountId = accountId
    if ((note || null) !== transaction.note) diff.note = note || null
    if ((chosen?.id ?? null) !== transaction.positionId) diff.positionId = chosen?.id ?? null
    if (planChoice && !planFixed) {
      diff.planYear = planChoice.year
      diff.planMonth = planChoice.month
    }

    // A position gives the booking its category and budget, otherwise the picked
    // category decides. A pure transfer has neither, and one with no purpose to
    // change is left as it is.
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

  const diff = stored ? changes(stored) : {}

  function newDraft(): Partial<Transaction> {
    return {
      accountId: accountId ?? undefined,
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
  }

  function submit(event: React.SyntheticEvent) {
    event.preventDefault()
    if (tick) return tick.onConfirm({ occurredOn, amount })
    if (edit && stored) {
      // Nothing changed: nothing to ask the server.
      if (Object.keys(diff).length === 0) return onClose()
      return edit.onSave({ id: stored.id, ...diff })
    }
    submitNew?.save(newDraft())
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

  /** A word to click, or — when `fixed` — the same text, named but not offered. */
  const word = (key: string, describedBy: string, text: React.ReactNode, fixed = false) =>
    fixed ? (
      <span className="font-medium">{text}</span>
    ) : (
      <SentenceWord
        ref={(element: HTMLButtonElement | null) => {
          wordRefs.current[key] = element
        }}
        open={openWord === key}
        onClick={() => toggleWord(key)}
        describedBy={describedBy}
      >
        {text}
      </SentenceWord>
    )

  // Ticking a position that already has bookings uses neither date nor amount.
  const locked = tick?.hasBookings ?? false

  const datePanel = !locked && openWord === 'date' && (
    <SentencePanel label={t(tick ? 'paidDialog.dateLabel' : 'monthBook.dateLabel')}>
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

  // The tick books on the position's account; without one the backend picks the
  // default, so there is nothing to name.
  const account = accounts.find((entry) => entry.id === accountId)
  const accountPanel = !tick && openWord === 'account' && (
    <SentencePanel label={t('monthBook.accountLabel')}>
      <div className="flex flex-wrap gap-2">
        {accounts.map((entry) => (
          <SentenceChip
            key={entry.id}
            selected={accountId === entry.id}
            onClick={() => {
              setAccountId(entry.id)
              // A transfer to the account it comes from makes no sense.
              if (counterAccountId === entry.id) setCounterAccountId('none')
              closeWord()
            }}
          >
            {entry.name}
          </SentenceChip>
        ))}
      </div>
    </SentencePanel>
  )

  const planMonthText = monthText(planMonth, occurredOn)
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

  // A booking made by ticking off cannot be moved off its position (the tick is
  // where that happens), and the tick itself is about one position.
  const positionFixed = Boolean(tick) || Boolean(stored?.autoBooked)
  const unplannedText = t('monthBook.unplannedIn', {
    budget: budgetLabel(BUDGET_SUGGESTION[category]),
  })
  const positionText =
    chosen?.label ??
    (stored || isTransfer ? t('monthBook.noPosition') : unplannedText)
  const positionItem = (position: PlanPosition) => (
    <SelectItem key={position.id} value={position.id}>
      {position.label}
    </SelectItem>
  )
  const positionPanel = !positionFixed && openWord === 'position' && (
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
          <SelectItem value="none">
            {t(stored ? 'monthBook.noPosition' : 'monthBook.unplanned')}
          </SelectItem>
          {positions.length === 1
            ? positions[0].positions.map(positionItem)
            : positions.map((entry) => (
                <SelectGroup key={`${entry.year}-${entry.month}`}>
                  <SelectLabel>{`${monthLabel(entry.month)} ${entry.year}`}</SelectLabel>
                  {entry.positions.map(positionItem)}
                </SelectGroup>
              ))}
        </SelectContent>
      </Select>
    </SentencePanel>
  )

  const sentenceKey = tick
    ? account
      ? 'paidDialog.sentenceWithAccount'
      : 'paidDialog.sentence'
    : stored
      ? 'monthBook.sentence'
      : 'monthBook.addSentence'
  const sentence = fillSentence(t(sentenceKey), {
    date: word('date', sentenceId, longDate(occurredOn), locked),
    account: word('account', sentenceId, account?.name ?? '', Boolean(tick)),
    planMonth: word('planMonth', sentenceId, planMonthText, planFixed),
    position: word('position', sentenceId, positionText, positionFixed),
  })

  // The quiet second sentence: the category while nothing else decides it, then a
  // transfer for a new booking or the note for a stored one. The tick has none.
  const categoryShown = !chosen && !isTransfer
  const categoryPanel = categoryShown && openWord === 'category' && (
    <SentencePanel label={t('common.category')}>
      <CategoryPicker value={category} onChange={setCategory} />
    </SentencePanel>
  )
  const counter = accounts.find((entry) => entry.id === counterAccountId)
  // A transfer moves money between two of one's own accounts: neither income nor
  // spending, unless a position is chosen — putting money on the savings account
  // then fulfils the savings quota.
  const transferPanel = !stored && openWord === 'transfer' && (
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
          .filter((entry) => entry.id !== accountId)
          .map((entry) => (
            <SentenceChip
              key={entry.id}
              selected={counterAccountId === entry.id}
              onClick={() => {
                setCounterAccountId(entry.id)
                closeWord()
              }}
            >
              {entry.name}
            </SentenceChip>
          ))}
      </div>
    </SentencePanel>
  )
  const notePanel = stored && openWord === 'note' && (
    <SentencePanel label={t('monthBook.note')}>
      <Label htmlFor="booking-note" className="sr-only">
        {t('monthBook.note')}
      </Label>
      <Input
        id="booking-note"
        value={note}
        onChange={(event) => setNote(event.target.value)}
        placeholder={t('monthBook.notePlaceholder')}
        aria-describedby={extrasSentenceId}
      />
    </SentencePanel>
  )
  const extras = tick
    ? null
    : fillSentence(
        t(
          `${stored ? 'monthBook.extrasSentence' : 'monthBook.addExtras'}.${categoryShown ? 'withCategory' : 'plain'}`
        ),
        {
          category: word('category', extrasSentenceId, categoryLabel(category)),
          note: word(
            'note',
            extrasSentenceId,
            note ? t('monthBook.notedWith', { note }) : t('monthBook.noNote')
          ),
          transfer: word(
            'transfer',
            extrasSentenceId,
            counter
              ? t('monthBook.transferWord', { account: counter.name })
              : t('monthBook.noTransfer')
          ),
        }
      )

  // A payment on 25.06. may belong to the July plan; only say so, never block it.
  const outsideMonth =
    tick !== null &&
    !locked &&
    occurredOn.slice(0, 7) !==
      `${viewedMonth.year}-${String(viewedMonth.month).padStart(2, '0')}`

  // Escape closes the open word first, the dialog only on a second press.
  const escapeWord = (event: React.KeyboardEvent) => {
    if (event.key !== 'Escape' || openWord === null) return
    event.stopPropagation()
    event.preventDefault()
    closeWord()
  }

  const frame = tick
    ? {
        title: t('paidDialog.title', { label: tick.position.label }),
        submitLabel: t('paidDialog.submit'),
        pendingLabel: t('paidDialog.pending'),
        dirty: occurredOn !== today() || amount !== tick.position.amountPlanned,
        pending: tick.pending,
        error: tick.error,
      }
    : edit
      ? {
          title: t('monthBook.editTitle'),
          description: t('monthBook.editHint'),
          submitLabel: t('common.save'),
          dirty: Object.keys(diff).length > 0,
          pending: edit.pending,
          error: edit.error,
          returnFocus: edit.returnFocus,
        }
      : {
          title: t('monthBook.add'),
          description: t('monthBook.addHint'),
          submitLabel: isTransfer ? t('monthBook.transfer') : t('monthBook.book'),
          dirty: amount !== '' || note !== '',
          pending: submitNew?.pending ?? false,
          error: submitNew?.error ?? null,
        }

  const amountClass =
    'h-auto border-0 bg-transparent px-0 pr-7 text-xl font-semibold placeholder:text-muted-foreground md:text-xl'

  return (
    <DialogFrame
      open
      onOpenChange={(open) => !open && onClose()}
      onSubmit={submit}
      {...frame}
      start={
        edit?.onDelete ? (
          <Button
            type="button"
            variant="ghost"
            className="text-destructive hover:text-destructive"
            disabled={edit.pending}
            onClick={edit.onDelete}
          >
            {t('common.delete')}
          </Button>
        ) : undefined
      }
    >
      <div className="flex flex-col gap-6">
        <div className="flex flex-col gap-3">
          {!stored && !tick && (
            <>
              <Label htmlFor="booking-note" className="sr-only">
                {t('monthBook.what')}
              </Label>
              <Input
                id="booking-note"
                value={note}
                onChange={(event) => setNote(event.target.value)}
                placeholder={t('monthBook.what')}
                className="h-auto border-0 border-b bg-transparent px-0 text-xl shadow-none focus-visible:ring-0 md:text-xl"
              />
            </>
          )}
          <div className="flex flex-col gap-2">
            <Label htmlFor="booking-amount" className="text-muted-foreground text-sm">
              {t('common.amount')}
            </Label>
            <AmountField
              id="booking-amount"
              value={amount}
              onChange={setAmount}
              required
              disabled={locked}
              aria-describedby={locked ? 'booking-locked-note' : undefined}
              inputClassName={amountClass}
            />
          </div>
        </div>

        <div className="flex flex-col gap-3" onKeyDownCapture={escapeWord}>
          <p id={sentenceId} className="text-base leading-relaxed">
            {sentence}
          </p>
          {datePanel}
          {accountPanel}
          {planMonthPanel}
          {positionPanel}
        </div>

        {extras && (
          <div className="flex flex-col gap-3" onKeyDownCapture={escapeWord}>
            <p id={extrasSentenceId} className="text-muted-foreground text-base leading-relaxed">
              {extras}
            </p>
            {categoryPanel}
            {transferPanel}
            {notePanel}
          </div>
        )}

        {locked && (
          <p id="booking-locked-note" className="text-muted-foreground text-sm" role="status">
            {t('paidDialog.hasBookings')}
          </p>
        )}
        {tick?.bookingsUnknown && (
          <p className="text-muted-foreground text-sm" role="status">
            {t('paidDialog.bookingsUnknown')}
          </p>
        )}
        {outsideMonth && (
          <p className="text-muted-foreground text-sm" role="status">
            {t('paidDialog.outsideMonth', {
              month: `${monthLabel(viewedMonth.month)} ${viewedMonth.year}`,
            })}
          </p>
        )}
      </div>
    </DialogFrame>
  )
}
