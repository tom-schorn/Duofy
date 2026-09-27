import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { DialogFrame } from '@/components/DialogFrame'
import { fillSentence } from '@/lib/sentence'
import { Button } from '@/components/ui/button'
import { AmountField } from '@/components/AmountField'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { DateField } from '@/components/DateField'
import { Calendar } from '@/components/ui/calendar'
import { SentenceWord } from '@/components/SentenceWord'
import { SentencePanel } from '@/components/SentencePanel'
import { SentenceChip } from '@/components/SentenceChip'
import { firstOfNextMonth, fromIsoDay, longDate, toIsoDay } from '@/lib/dates'
import { formatAmount } from '@/lib/amount'
import { MoreDetails } from '@/components/MoreDetails'
import { Switch } from '@/components/ui/switch'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { CategoryPicker } from '@/components/CategoryPicker'
import { cn } from '@/lib/utils'
import {
  BUDGET_DOT,
  budgetLabel,
  BUDGET_ORDER,
  BUDGET_SUGGESTION,
  DUE_DAY_MAY_SHIFT,
  categoryGroup,
  categoryLabel,
  monthLabel,
  paymentLabel,
  intervalLabel,
  dueDayOf,
  isValidInterval,
  nextDueDates,
  dueDateLabel,
  parseIntervalText,
  effectiveDueDay,
  type Category,
  type Commitment,
  type CommitmentType,
  type PaymentMethod,
  PAYMENT_METHODS,
  INTERVAL_MAX,
  INTERVAL_MIN,
  INTERVAL_PRESETS,
} from '@/lib/domain'
import { useAccounts, useHouseholds } from '@/lib/queries'
import { OptionalMark } from '@/components/OptionalMark'

/**
 * One form for every commitment — savings plans and loans are commitments too.
 *
 * Creating takes two steps: first four cards ask „Was ist das?“ in everyday words,
 * then only that kind's fields follow, with a way back. Editing has no cards and no
 * switch: the kind is fixed and named in the title (#190).
 *
 * Name and amount stay a form (real labels, real inputs); the rest of the facts read
 * as one sentence with clickable words, each opening its choice right below the
 * sentence — „Satz statt Formular“ (issue #215, decision 28). Category and every
 * other rare field sit behind a text link instead of a boxed „Weitere Angaben“.
 *
 * The type drives the extra fields:
 *   contract      → none, plus the optional `isLimit` flag
 *   savings_goal  → target amount, target date
 *   debt          → none
 *
 * The CHECK constraints in the database enforce exactly this mapping.
 *
 * For savings_goal and debt, `resolve_budget()` in the backend overrides the budget
 * choice and always sets savings. That is why there is no picker there but the
 * reason instead — a greyed-out field would have suggested it might still work.
 */

/**
 * What the person picks on a card. A limit is a contract with `isLimit` set, so it
 * is a card of its own but no type of its own: the backend knows four types.
 */
type Kind = CommitmentType | 'limit'

const TYPE_OPTIONS: {
  value: Kind
  label: string
  hint: string
  addTitle: string
  editTitle: string
  /** Why the budget is fixed — shown in place of the picker. Catalog keys, like the other texts. */
  budgetHint: string | null
  namePlaceholder: string
  defaultCategory: Category
}[] = [
  {
    value: 'contract',
    label: 'commitmentDialog.types.contract.label',
    hint: 'commitmentDialog.types.contract.hint',
    addTitle: 'commitmentDialog.types.contract.addTitle',
    editTitle: 'commitmentDialog.types.contract.editTitle',
    budgetHint: null,
    namePlaceholder: 'commitmentDialog.types.contract.namePlaceholder',
    defaultCategory: 'housing.rent',
  },
  {
    value: 'limit',
    label: 'commitmentDialog.types.limit.label',
    hint: 'commitmentDialog.types.limit.hint',
    addTitle: 'commitmentDialog.types.limit.addTitle',
    editTitle: 'commitmentDialog.types.limit.editTitle',
    budgetHint: null,
    namePlaceholder: 'commitmentDialog.types.limit.namePlaceholder',
    defaultCategory: 'housing.rent',
  },
  {
    value: 'savings_goal',
    label: 'commitmentDialog.types.savings_goal.label',
    hint: 'commitmentDialog.types.savings_goal.hint',
    addTitle: 'commitmentDialog.types.savings_goal.addTitle',
    editTitle: 'commitmentDialog.types.savings_goal.editTitle',
    budgetHint: 'commitmentDialog.types.savings_goal.budgetHint',
    namePlaceholder: 'commitmentDialog.types.savings_goal.namePlaceholder',
    defaultCategory: 'finance.savings',
  },
  {
    value: 'debt',
    label: 'commitmentDialog.types.debt.label',
    hint: 'commitmentDialog.types.debt.hint',
    addTitle: 'commitmentDialog.types.debt.addTitle',
    editTitle: 'commitmentDialog.types.debt.editTitle',
    // The reason, in one sentence.
    budgetHint: 'commitmentDialog.types.debt.budgetHint',
    namePlaceholder: 'commitmentDialog.types.debt.namePlaceholder',
    defaultCategory: 'finance.debt',
  },
  {
    value: 'income',
    label: 'commitmentDialog.types.income.label',
    hint: 'commitmentDialog.types.income.hint',
    addTitle: 'commitmentDialog.types.income.addTitle',
    editTitle: 'commitmentDialog.types.income.editTitle',
    budgetHint: 'commitmentDialog.types.income.budgetHint',
    namePlaceholder: 'commitmentDialog.types.income.namePlaceholder',
    defaultCategory: 'income.earned',
  },
]

const PAYMENTS = PAYMENT_METHODS
/** Select value that opens the number field for any other distance. */
const CUSTOM = 'custom'

/**
 * Switching the type clears the extra fields that do not belong to the new one —
 * otherwise the form sends values the database rejects.
 */
function withType(current: Commitment, kind: Kind): Commitment {
  const type: CommitmentType = kind === 'limit' ? 'contract' : kind
  const isLimit = kind === 'limit'
  if (type === current.type) return { ...current, isLimit }
  const previous = TYPE_OPTIONS.find((item) => item.value === kindOf(current))!
  const option = TYPE_OPTIONS.find((item) => item.value === kind)!
  // Only follow along with the category if it still holds the old suggestion —
  // or if it sits on the wrong side of the income line. The income group and
  // the income type belong together in both directions: a salary filed under
  // Miete is as wrong as a contract filed under Gehalt.
  const categoryFits = (categoryGroup(current.category) === 'income') === (type === 'income')
  const category =
    categoryFits && current.category !== previous.defaultCategory
      ? current.category
      : option.defaultCategory

  return {
    ...current,
    type,
    category,
    // Derived from the category that is actually being kept, not from the one
    // being replaced. Otherwise switching away from a savings goal takes the
    // new category but leaves the budget on Sparen.
    budget:
      type === 'savings_goal' || type === 'debt'
        ? 'savings'
        : type === 'income'
          ? 'income'
          : BUDGET_SUGGESTION[category],
    // The limit flag only makes sense on a running contract — Lebensmittel and
    // Sprit are contracts, not savings goals, debts or income.
    isLimit,
    targetAmount: type === 'savings_goal' ? current.targetAmount : null,
    targetDate: type === 'savings_goal' ? current.targetDate : null,
  }
}

function kindOf(commitment: Commitment): Kind {
  return commitment.type === 'contract' && commitment.isLimit ? 'limit' : commitment.type
}

/** A contract and a limit are the same underneath: a free amount, a free category. */
const isPlainKind = (kind: Kind) => kind === 'contract' || kind === 'limit'

/**
 * Which of the optional fields a kind names as a sentence word instead of leaving
 * them behind the „Kategorie oder weitere Angaben“ link. Category itself is never a
 * word — a fact this ordinary, not a household finding, sits with the rare fields.
 */
function upfront(kind: Kind, activeAccounts: number) {
  return {
    // Where the money comes from or goes to: always for a loan and income, and for an
    // ordinary expense once there is a choice.
    account: kind === 'debt' || kind === 'income' || (isPlainKind(kind) && activeAccounts > 1),
    endsOn: kind === 'debt',
    // A savings goal is money moved to another account, and reached on a date.
    counterAccount: kind === 'savings_goal',
    targetDate: kind === 'savings_goal',
  }
}

function emptyDraft(): Commitment {
  return {
    id: '',
    deletable: false,
    type: 'contract',
    name: '',
    amount: '',
    category: 'housing.rent',
    budget: 'needs',
    isLimit: false,
    householdId: null,
    intervalMonths: 1,
    // Every commitment has one; the 1st of next month is a better start than an
    // empty mandatory field, and keeps the pay day at 1 for whoever skips it.
    firstDueDate: firstOfNextMonth(),
    endsOn: null,
    passThrough: false,
    counterAccountId: null,
    targetAmount: null,
    targetDate: null,
    paymentMethod: null,
    accountId: null,
  }
}

type Props = {
  /** null means create, otherwise edit. */
  commitment: Commitment | null
  open: boolean
  onOpenChange: (open: boolean) => void
  onSave: (commitment: Commitment) => void
  /** The server has not answered yet; the dialog stays open and locked. */
  pending?: boolean
  /** The server said no; shown above the buttons, the input stays. */
  error?: unknown
  /**
   * Absent or null: no delete button (rule 3) - no right, or the commitment is
   * already in a plan and only ends. The caller asks once before it deletes.
   */
  onDelete?: ((commitment: Commitment) => void) | null
  /** Where the focus goes on closing, when the opener is gone (after a delete). */
  returnFocus?: () => HTMLElement | null
}

export function CommitmentDialog({
  commitment,
  open,
  onOpenChange,
  onSave,
  pending = false,
  error = null,
  onDelete = null,
  returnFocus,
}: Props) {
  const { t } = useTranslation()
  const households = useHouseholds().data ?? []
  const accounts = useAccounts().data ?? []
  const [draft, setDraft] = useState<Commitment>(commitment ?? emptyDraft())
  // Creating starts with the question, editing goes straight to the fields.
  const [step, setStep] = useState<'choose' | 'form'>(commitment ? 'form' : 'choose')
  // Whether the rare fields were open; kept across „Zurück“, where the fields go away.
  const detailsOpened = useRef(false)

  // Reset on open — otherwise the previous state is still in the fields.
  // "Anderer Abstand" is its own state, not derived from the value: typing 6 into
  // the number field must not snap the select back to "alle 6 Monate" under the
  // user's cursor.
  const [customInterval, setCustomInterval] = useState(false)
  const [intervalText, setIntervalText] = useState('1')
  const intervalField = useRef<HTMLInputElement>(null)

  // Which sentence word is open — only one at a time (issue #215).
  const [openWord, setOpenWord] = useState<string | null>(null)
  const wordRefs = useRef<Record<string, HTMLButtonElement | null>>({})

  useEffect(() => {
    if (open) {
      const next = commitment ?? emptyDraft()
      setDraft(next)
      setStep(commitment ? 'form' : 'choose')
      detailsOpened.current = false
      setCustomInterval(!INTERVAL_PRESETS.includes(next.intervalMonths))
      setIntervalText(String(next.intervalMonths))
      setOpenWord(null)
    }
  }, [open, commitment])

  const isEdit = commitment !== null
  // Choosing a card is no change yet: compare against a fresh draft of the same kind.
  const kind = kindOf(draft)
  const baseline = commitment ?? withType(emptyDraft(), kind)
  const dirty = JSON.stringify(draft) !== JSON.stringify(baseline)
  const typeOption = TYPE_OPTIONS.find((option) => option.value === kind)!
  const activeAccounts = accounts.filter((account) => account.active)
  const up = upfront(kind, activeAccounts.length)
  const intervalValid = isValidInterval(draft.intervalMonths)
  // Only savings goals and debts are fixed — resolve_budget() in the backend
  // overrides them anyway. A contract chooses freely: whether fuel is a need or a
  // want depends on the household.
  const typeForcesSavings = draft.type === 'savings_goal' || draft.type === 'debt'
  // Income is settled the same way, only by resolve_budget() sending it to INCOME.
  const typeForcesIncome = draft.type === 'income'

  // An income category settles the budget just as firmly: all four of them lead to
  // Einnahmen. Kept apart from the type, because handleCategory has to know which
  // of the two is talking.
  const budgetIsFixed =
    typeForcesSavings || typeForcesIncome || categoryGroup(draft.category) === 'income'

  function set<K extends keyof Commitment>(key: K, value: Commitment[K]) {
    setDraft((current) => ({ ...current, [key]: value }))
  }

  /** The kind is chosen on the card of step 1, and can be changed again by going back. */
  function handleType(next: Kind) {
    setDraft((current) => withType(current, next))
    setStep('form')
    setOpenWord(null)
  }

  function handleBack() {
    setStep('choose')
    setOpenWord(null)
  }

  /** Changing the category preselects the budget — not for goals and debts. */
  function handleCategory(category: Category) {
    setDraft((current) => ({
      ...current,
      category,
      budget: typeForcesSavings
        ? 'savings'
        : typeForcesIncome
          ? 'income'
          : BUDGET_SUGGESTION[category],
    }))
  }

  function handleInterval(intervalMonths: number) {
    setDraft((current) => ({ ...current, intervalMonths }))
  }

  function handleIntervalSelect(value: string) {
    if (value === CUSTOM) {
      setCustomInterval(true)
      // The field only exists after this render; the panel would otherwise keep
      // the focus and leave the user to find the new field.
      requestAnimationFrame(() => intervalField.current?.focus())
      return
    }
    setCustomInterval(false)
    setIntervalText(value)
    handleInterval(Number(value))
  }

  function handleIntervalText(value: string) {
    setIntervalText(value)
    const parsed = parseIntervalText(value)
    // An empty or broken field keeps a value the range check rejects, so saving
    // stays blocked until it holds a whole number from 1 to 120.
    if (parsed !== 0) handleInterval(parsed)
    else setDraft((current) => ({ ...current, intervalMonths: 0 }))
  }

  /** Day, month and year all fall out of the first due date — it is the one source. */
  function handleFirstDueDate(value: string) {
    // The field can be cleared; keep the last valid date so the draft stays complete.
    if (value) setDraft((current) => ({ ...current, firstDueDate: value }))
  }

  function handleSubmit(event: React.FormEvent) {
    event.preventDefault()
    if (!intervalValid) return
    // Stays open until the server agrees — the caller closes it on success.
    onSave(draft)
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

  // From the 29th on the day can shift — February is the hard case.
  const dueDay = dueDayOf(draft.firstDueDate)
  const dueDayShifts = dueDay >= DUE_DAY_MAY_SHIFT
  const shiftYear = Number(draft.firstDueDate.slice(0, 4))
  const now = new Date()
  const upcoming = intervalValid
    ? nextDueDates(
        draft.intervalMonths,
        draft.firstDueDate,
        { year: now.getFullYear(), month: now.getMonth() + 1 },
        3
      )
    : []
  const februaryDay = effectiveDueDay(dueDay, shiftYear, 2)

  const choosing = step === 'choose'

  // --- Sentence words -------------------------------------------------------

  const rhythmWord = (
    <SentenceWord ref={wordRef('rhythm')} open={openWord === 'rhythm'} onClick={() => toggleWord('rhythm')}>
      {intervalLabel(draft.intervalMonths)}
    </SentenceWord>
  )
  const rhythmPanel = openWord === 'rhythm' && (
    <SentencePanel label={t('commitmentDialog.rhythmLabel')}>
      <div className="flex flex-wrap gap-2">
        {INTERVAL_PRESETS.map((months) => (
          <SentenceChip
            key={months}
            selected={!customInterval && draft.intervalMonths === months}
            onClick={() => {
              handleIntervalSelect(String(months))
              closeWord()
            }}
          >
            {intervalLabel(months)}
          </SentenceChip>
        ))}
        <SentenceChip selected={customInterval} onClick={() => handleIntervalSelect(CUSTOM)}>
          {t('commitmentDialog.customInterval')}
        </SentenceChip>
      </div>
      {customInterval && (
        <div className="flex flex-col gap-2">
          <Label htmlFor="interval-custom">{t('commitmentDialog.customIntervalField')}</Label>
          <Input
            id="interval-custom"
            ref={intervalField}
            type="number"
            min={INTERVAL_MIN}
            max={INTERVAL_MAX}
            step="1"
            inputMode="numeric"
            value={intervalText}
            onChange={(event) => handleIntervalText(event.target.value)}
            aria-invalid={!intervalValid}
            aria-describedby="interval-custom-hint"
            className="w-32"
            required
          />
          {/* Always in the DOM so the field can point at it; only the
              error turns into an announcement. */}
          <span
            id="interval-custom-hint"
            role={intervalValid ? undefined : 'alert'}
            className={intervalValid ? 'text-muted-foreground text-xs' : 'text-destructive text-xs'}
          >
            {t('commitmentDialog.intervalRange', { min: INTERVAL_MIN, max: INTERVAL_MAX })}
          </span>
        </div>
      )}
    </SentencePanel>
  )

  const dateWord = (
    <SentenceWord ref={wordRef('date')} open={openWord === 'date'} onClick={() => toggleWord('date')}>
      {longDate(draft.firstDueDate)}
    </SentenceWord>
  )
  const datePanel = openWord === 'date' && (
    <SentencePanel label={t('commitmentDialog.dateLabel')}>
      <Calendar
        mode="single"
        selected={fromIsoDay(draft.firstDueDate)}
        defaultMonth={fromIsoDay(draft.firstDueDate)}
        onSelect={(date) => {
          if (!date) return
          handleFirstDueDate(toIsoDay(date))
          closeWord()
        }}
        autoFocus
      />
      <p className="text-muted-foreground text-xs">
        {t(
          draft.intervalMonths === 1
            ? 'commitmentDialog.firstDueHintMonthly'
            : 'commitmentDialog.firstDueHint'
        )}
      </p>
      {(upcoming.length > 0 || dueDayShifts) && (
        <p className="text-muted-foreground flex flex-col gap-1 text-xs">
          {upcoming.length > 0 && (
            <span>
              {t('commitmentDialog.dueIn', {
                day: dueDay,
                dates: upcoming.map(dueDateLabel).join(', '),
              })}
              {` — ${t('commitmentDialog.firstTime', {
                month: monthLabel(Number(draft.firstDueDate.slice(5, 7))),
                year: draft.firstDueDate.slice(0, 4),
              })}`}
            </span>
          )}
          {dueDayShifts && (
            <span>
              {t('commitmentDialog.dayShifts', { day: dueDay, year: shiftYear, februaryDay })}
            </span>
          )}
        </p>
      )}
    </SentencePanel>
  )

  const accountName = (id: string | null) =>
    id === null ? t('common.defaultAccount') : (accounts.find((account) => account.id === id)?.name ?? '')

  const accountWord = (
    <SentenceWord ref={wordRef('account')} open={openWord === 'account'} onClick={() => toggleWord('account')}>
      {accountName(draft.accountId)}
    </SentenceWord>
  )
  const accountPanel = openWord === 'account' && (
    <SentencePanel label={t('commitmentDialog.accountLabel')}>
      <div className="flex flex-wrap gap-2">
        <SentenceChip
          selected={draft.accountId === null}
          onClick={() => {
            set('accountId', null)
            closeWord()
          }}
        >
          {t('common.defaultAccount')}
        </SentenceChip>
        {activeAccounts.map((account) => (
          <SentenceChip
            key={account.id}
            selected={draft.accountId === account.id}
            onClick={() => {
              set('accountId', account.id)
              closeWord()
            }}
          >
            {account.name}
          </SentenceChip>
        ))}
      </div>
    </SentencePanel>
  )

  const counterAccountWord = (
    <SentenceWord
      ref={wordRef('counterAccount')}
      open={openWord === 'counterAccount'}
      onClick={() => toggleWord('counterAccount')}
    >
      {draft.counterAccountId === null ? t('common.goesOut') : accountName(draft.counterAccountId)}
    </SentenceWord>
  )
  const counterAccountPanel = openWord === 'counterAccount' && (
    <SentencePanel label={t('commitmentDialog.counterAccountLabel')}>
      <div className="flex flex-wrap gap-2">
        <SentenceChip
          selected={draft.counterAccountId === null}
          onClick={() => {
            set('counterAccountId', null)
            closeWord()
          }}
        >
          {t('common.goesOut')}
        </SentenceChip>
        {accounts
          .filter((account) => account.id !== draft.accountId)
          .map((account) => (
            <SentenceChip
              key={account.id}
              selected={draft.counterAccountId === account.id}
              onClick={() => {
                set('counterAccountId', account.id)
                closeWord()
              }}
            >
              {account.name}
            </SentenceChip>
          ))}
      </div>
      <p className="text-muted-foreground text-xs">{t('common.counterAccountHint')}</p>
    </SentencePanel>
  )

  const budgetWord = budgetIsFixed ? (
    <span className="font-medium">{budgetLabel(draft.budget)}</span>
  ) : (
    <SentenceWord ref={wordRef('budget')} open={openWord === 'budget'} onClick={() => toggleWord('budget')}>
      {budgetLabel(draft.budget)}
    </SentenceWord>
  )
  const budgetPanel = !budgetIsFixed && openWord === 'budget' && (
    <SentencePanel label={t('commitmentDialog.budgetLabel')}>
      <div className="flex flex-wrap gap-2">
        {BUDGET_ORDER.map((budget) => (
          <SentenceChip
            key={budget}
            selected={draft.budget === budget}
            onClick={() => {
              set('budget', budget)
              closeWord()
            }}
          >
            <span className={cn('size-2.5 rounded-sm', BUDGET_DOT[budget])} />
            {budgetLabel(budget)}
          </SentenceChip>
        ))}
      </div>
    </SentencePanel>
  )

  const endsOnWord = (
    <SentenceWord ref={wordRef('endsOn')} open={openWord === 'endsOn'} onClick={() => toggleWord('endsOn')}>
      {draft.endsOn ? longDate(draft.endsOn) : t('commitmentDialog.noEnd')}
    </SentenceWord>
  )
  const endsOnPanel = openWord === 'endsOn' && (
    <SentencePanel label={t('commitmentDialog.endsOnLabel')}>
      <Calendar
        mode="single"
        selected={fromIsoDay(draft.endsOn ?? '')}
        defaultMonth={fromIsoDay(draft.endsOn ?? draft.firstDueDate)}
        onSelect={(date) => {
          if (!date) return
          set('endsOn', toIsoDay(date))
          closeWord()
        }}
        autoFocus
      />
      <SentenceChip
        selected={draft.endsOn === null}
        onClick={() => {
          set('endsOn', null)
          closeWord()
        }}
      >
        {t('commitmentDialog.clearEnd')}
      </SentenceChip>
      <p className="text-muted-foreground text-xs">{t('commitmentDialog.endsOnHint')}</p>
    </SentencePanel>
  )

  const targetDateWord = (
    <SentenceWord
      ref={wordRef('targetDate')}
      open={openWord === 'targetDate'}
      onClick={() => toggleWord('targetDate')}
    >
      {draft.targetDate ? longDate(draft.targetDate) : t('commitmentDialog.noTargetDate')}
    </SentenceWord>
  )
  const targetDatePanel = openWord === 'targetDate' && (
    <SentencePanel label={t('commitmentDialog.targetDateLabel')}>
      <Calendar
        mode="single"
        selected={fromIsoDay(draft.targetDate ?? '')}
        defaultMonth={fromIsoDay(draft.targetDate ?? draft.firstDueDate)}
        onSelect={(date) => {
          if (!date) return
          set('targetDate', toIsoDay(date))
          closeWord()
        }}
        autoFocus
      />
    </SentencePanel>
  )

  const targetAmountWord = (
    <SentenceWord
      ref={wordRef('targetAmount')}
      open={openWord === 'targetAmount'}
      onClick={() => toggleWord('targetAmount')}
    >
      {draft.targetAmount ? `${formatAmount(draft.targetAmount)} €` : t('commitmentDialog.noTargetAmount')}
    </SentenceWord>
  )
  const targetAmountPanel = openWord === 'targetAmount' && (
    <SentencePanel label={t('commitmentDialog.targetAmountLabel')}>
      <AmountField
        id="target-amount"
        value={draft.targetAmount ?? ''}
        onChange={(value) => set('targetAmount', value || null)}
        allowZero
        className="w-48"
      />
    </SentencePanel>
  )

  const words: Record<string, React.ReactNode> = {
    rhythm: rhythmWord,
    date: dateWord,
    account: accountWord,
    counterAccount: counterAccountWord,
    budget: budgetWord,
    endsOn: endsOnWord,
    targetDate: targetDateWord,
    targetAmount: targetAmountWord,
  }
  const panels = [
    rhythmPanel,
    datePanel,
    accountPanel,
    counterAccountPanel,
    budgetPanel,
    endsOnPanel,
    targetDatePanel,
    targetAmountPanel,
  ]

  // Which catalog sentence fits this kind — the account clause drops out entirely
  // once there is only the one, default account to speak of.
  const sentenceKey = isPlainKind(kind)
    ? `${kind}${up.account ? '' : 'DefaultAccount'}`
    : kind

  // --- Rare fields, behind the text link ------------------------------------

  const accountField = (
    <div className="flex flex-col gap-2">
      <Label>{t('common.account')}</Label>
      <Select
        value={draft.accountId ?? 'default'}
        onValueChange={(value) => set('accountId', value === 'default' ? null : value)}
      >
        <SelectTrigger>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="default">{t('common.defaultAccount')}</SelectItem>
          {activeAccounts.map((account) => (
            <SelectItem key={account.id} value={account.id}>
              {account.name}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  )
  const counterAccountField = (
    <div className="flex flex-col gap-2">
      <Label>{t('common.counterAccount')}</Label>
      <Select
        value={draft.counterAccountId ?? 'none'}
        onValueChange={(value) => set('counterAccountId', value === 'none' ? null : value)}
      >
        <SelectTrigger>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="none">{t('common.goesOut')}</SelectItem>
          {accounts
            .filter((account) => account.id !== draft.accountId)
            .map((account) => (
              <SelectItem key={account.id} value={account.id}>
                {account.name}
              </SelectItem>
            ))}
        </SelectContent>
      </Select>
      <span className="text-muted-foreground text-xs">{t('common.counterAccountHint')}</span>
    </div>
  )
  const paymentField = (
    <div className="flex flex-col gap-2">
      <Label>{t('common.paymentMethod')}</Label>
      <Select
        value={draft.paymentMethod ?? 'none'}
        onValueChange={(value) =>
          set('paymentMethod', value === 'none' ? null : (value as PaymentMethod))
        }
      >
        <SelectTrigger>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="none">{t('commitmentDialog.noPaymentMethod')}</SelectItem>
          {PAYMENTS.map((method) => (
            <SelectItem key={method} value={method}>
              {paymentLabel(method)}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  )
  const endsOnField = (
    <div className="flex flex-col gap-2">
      <Label htmlFor="ends-on">{t('commitmentDialog.endsOn')}<OptionalMark /></Label>
      <div className="flex gap-2">
        <DateField
          id="ends-on"
          value={draft.endsOn ?? ''}
          onChange={(iso) => set('endsOn', iso || null)}
          placeholder={t('commitmentDialog.clearEnd')}
          describedBy="ends-on-hint"
        />
        {draft.endsOn !== null && (
          <Button type="button" variant="outline" onClick={() => set('endsOn', null)}>
            {t('commitmentDialog.clearEnd')}
          </Button>
        )}
      </div>
      <p id="ends-on-hint" className="text-muted-foreground text-xs">
        {t('commitmentDialog.endsOnHint')}
      </p>
    </div>
  )

  // A quiet line under the link when something rare is already set — the section
  // itself stays collapsed regardless (review D-215-2, fix 2).
  const extrasSummary = [
    draft.category !== typeOption.defaultCategory &&
      t('commitmentDialog.extrasSummary.category', { value: categoryLabel(draft.category) }),
    !up.account &&
      draft.accountId !== null &&
      t('commitmentDialog.extrasSummary.account', { value: accountName(draft.accountId) }),
    !up.counterAccount &&
      draft.counterAccountId !== null &&
      t('commitmentDialog.extrasSummary.counterAccount', {
        value: accountName(draft.counterAccountId),
      }),
    !up.endsOn &&
      draft.endsOn !== null &&
      t('commitmentDialog.extrasSummary.endsOn', {
        value: dueDateLabel({
          year: Number(draft.endsOn.slice(0, 4)),
          month: Number(draft.endsOn.slice(5, 7)),
        }),
      }),
    draft.paymentMethod !== null &&
      t('commitmentDialog.extrasSummary.paymentMethod', {
        value: paymentLabel(draft.paymentMethod),
      }),
    draft.householdId !== null &&
      t('commitmentDialog.extrasSummary.assignment', {
        value: households.find((household) => household.id === draft.householdId)?.name ?? '',
      }),
    draft.passThrough && t('commitmentDialog.extrasSummary.passThrough'),
  ]
    .filter((part): part is string => Boolean(part))
    .join(' · ')

  return (
    // The frame keeps title and buttons in place and lets only the middle scroll.
    <DialogFrame
      open={open}
      onOpenChange={onOpenChange}
      className="sm:max-w-lg"
      title={
        choosing
          ? t('commitmentDialog.chooseTitle')
          : isEdit
            ? t(typeOption.editTitle)
            : t(typeOption.addTitle)
      }
      description={
        choosing
          ? t('commitmentDialog.chooseDescription')
          : // The hint is about creating; editing already has the fields in front of you.
            isEdit
            ? undefined
            : t('commitmentDialog.description')
      }
      submitLabel={isEdit ? t('common.save') : t('common.create')}
      hideSubmit={choosing}
      focusKey={step}
      onSubmit={choosing ? (event) => event.preventDefault() : handleSubmit}
      submitDisabled={!intervalValid}
      dirty={!choosing && dirty}
      pending={pending}
      error={error}
      returnFocus={returnFocus}
      start={
        isEdit && onDelete !== null ? (
          <Button
            type="button"
            variant="ghost"
            className="text-destructive hover:text-destructive"
            disabled={pending}
            onClick={() => onDelete(commitment)}
          >
            {t('common.delete')}
          </Button>
        ) : !isEdit && !choosing ? (
          <Button type="button" variant="ghost" onClick={handleBack}>
            {t('commitmentDialog.back')}
          </Button>
        ) : undefined
      }
    >
      {choosing ? (
        <div className="grid gap-3 sm:grid-cols-2">
          {TYPE_OPTIONS.map((option) => (
            <button
              key={option.value}
              type="button"
              data-dialog-card
              onClick={() => handleType(option.value)}
              className="border-border hover:bg-muted focus-visible:ring-ring flex flex-col gap-1 rounded-md border p-3 text-left focus-visible:ring-2 focus-visible:outline-none"
            >
              <span className="font-medium">{t(option.label)}</span>
              <span className="text-muted-foreground text-xs">{t(option.hint)}</span>
            </button>
          ))}
        </div>
      ) : (
        <div className="flex flex-col gap-6">
          <div className="flex flex-col gap-2">
            <Label htmlFor="name" className="sr-only">
              {t('positionDialog.label')}
            </Label>
            <Input
              id="name"
              value={draft.name}
              onChange={(event) => set('name', event.target.value)}
              placeholder={t(typeOption.namePlaceholder)}
              required
              // md:text-3xl repeats text-3xl: the base input's own md:text-sm is a
              // Tailwind responsive utility, so it wins over a plain text-3xl on
              // anything ≥768px unless the override names the same breakpoint.
              className="font-heading h-auto rounded-none border-0 border-b border-border bg-transparent px-0 pb-2 text-3xl placeholder:text-muted-foreground focus-visible:border-primary focus-visible:ring-0 md:text-3xl"
            />

            <div className="flex items-baseline gap-3">
              <Label htmlFor="amount" className="text-muted-foreground shrink-0 text-sm">
                {draft.type === 'debt'
                  ? t('commitmentDialog.rate')
                  : draft.type === 'savings_goal'
                    ? t('commitmentDialog.saving')
                    : t('common.amount')}
              </Label>
              <AmountField
                id="amount"
                value={draft.amount}
                onChange={(value) => set('amount', value)}
                required
                allowZero
                className="flex-1"
                inputClassName="h-auto border-0 bg-transparent px-0 pr-7 text-2xl font-semibold placeholder:text-muted-foreground md:text-2xl"
              />
            </div>
          </div>

          <div
            className="flex flex-col gap-3"
            onKeyDownCapture={(event) => {
              // The focus may still sit on the word (nothing chosen yet) or have
              // moved into the panel — either way Esc closes just this, not the
              // whole dialog. Capture, not bubble: a plain <div> has no ARIA role
              // that a keyboard handler is normally attached to.
              if (event.key !== 'Escape' || openWord === null) return
              event.stopPropagation()
              event.preventDefault()
              closeWord()
            }}
          >
            <p className="text-lg leading-8">{fillSentence(t(`commitmentDialog.sentence.${sentenceKey}`), words)}</p>
            {panels}
          </div>

          {typeOption.budgetHint && (
            <p className="text-muted-foreground bg-muted rounded-md px-3 py-2 text-xs">
              {t(typeOption.budgetHint)}
            </p>
          )}

          <MoreDetails
            resetKey={commitment}
            // A set value stays quiet on the summary line instead of forcing the
            // section open (review D-215-2, fix 2).
            hasValues={false}
            startOpen={detailsOpened.current}
            onToggle={(opened) => {
              detailsOpened.current = opened
            }}
            label={t('commitmentDialog.addDetails')}
            plain
            summary={extrasSummary || undefined}
          >
            <div className="flex flex-col gap-2">
              <Label>{t('common.category')}</Label>
              <CategoryPicker value={draft.category} onChange={handleCategory} />
            </div>

            {!up.endsOn && endsOnField}

            {/* Account and payment method belong to the commitment, not to the
                month: they are copied into every position and stay overridable
                there. */}
            {!up.account && accountField}
            {!up.counterAccount && counterAccountField}
            {paymentField}

            <div className="flex flex-col gap-2">
              <Label>{t('common.assignment')}</Label>
              <Select
                value={draft.householdId ?? 'private'}
                onValueChange={(value) => set('householdId', value === 'private' ? null : value)}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="private">{t('common.privateOnly')}</SelectItem>
                  {households.map((household) => (
                    <SelectItem key={household.id} value={household.id}>
                      {household.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-muted-foreground text-xs">
                {t('commitmentDialog.assignmentHint')}
              </p>
            </div>

            {/* Takes the position out of budget and quotas. Needed for money that
                is only passed through — otherwise 1.139 EUR forwarded would look
                like 1.139 EUR saved. */}
            <div className="border-border flex items-center justify-between rounded-md border p-3">
              <div className="flex flex-col pr-4">
                <Label htmlFor="commitment-pass-through">{t('common.passThrough')}</Label>
                <span className="text-muted-foreground text-xs">{t('common.passThroughHint')}</span>
              </div>
              <Switch
                id="commitment-pass-through"
                checked={draft.passThrough}
                onCheckedChange={(checked) => set('passThrough', checked)}
              />
            </div>
          </MoreDetails>
        </div>
      )}
    </DialogFrame>
  )
}
