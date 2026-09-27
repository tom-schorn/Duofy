import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { DialogFrame } from '@/components/DialogFrame'
import { Button } from '@/components/ui/button'
import { AmountField } from '@/components/AmountField'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { DateField } from '@/components/DateField'
import { firstOfNextMonth } from '@/lib/dates'
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
  monthLabel,
  paymentLabel,
  intervalLabel,
  dueDayOf,
  isValidInterval,
  nextDueDates,
  dueDateLabel,
  parseIntervalText,
  effectiveDueDay,
  type Budget,
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
 * switch: the kind is fixed and named in the title (#190). Required fields are
 * visible, everything rare sits under „Weitere Angaben“.
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

/** Which of the optional fields a kind asks for up front instead of under „Weitere Angaben“. */
function upfront(kind: Kind, activeAccounts: number) {
  return {
    // Where the money comes from or goes to: always for a loan and income, and for an
    // ordinary expense once there is a choice.
    account: kind === 'debt' || kind === 'income' || (isPlainKind(kind) && activeAccounts > 1),
    endsOn: kind === 'debt',
    // A savings goal is money moved to another account, and reached on a date.
    counterAccount: kind === 'savings_goal',
    targetDate: kind === 'savings_goal',
    category: isPlainKind(kind),
  }
}

/** Something under „Weitere Angaben“ is set, so the section must start open. */
function hasExtras(commitment: Commitment, activeAccounts: number): boolean {
  const kind = kindOf(commitment)
  const option = TYPE_OPTIONS.find((item) => item.value === kind)!
  const up = upfront(kind, activeAccounts)
  return (
    (!up.account && commitment.accountId !== null) ||
    (!up.counterAccount && commitment.counterAccountId !== null) ||
    commitment.paymentMethod !== null ||
    commitment.householdId !== null ||
    commitment.passThrough ||
    (!up.endsOn && commitment.endsOn !== null) ||
    (!up.targetDate && commitment.targetDate !== null) ||
    // A contract shows its category up front; the other kinds keep it in here.
    (!up.category && commitment.category !== option.defaultCategory)
  )
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
  // Whether „Weitere Angaben“ was open; kept across „Zurück“, where the fields go away.
  const detailsOpened = useRef(false)

  // Reset on open — otherwise the previous state is still in the fields.
  // "Anderer Abstand" is its own state, not derived from the value: typing 6 into
  // the number field must not snap the select back to "alle 6 Monate" under the
  // user's cursor.
  const [customInterval, setCustomInterval] = useState(false)
  const [intervalText, setIntervalText] = useState('1')
  const intervalField = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (open) {
      const next = commitment ?? emptyDraft()
      setDraft(next)
      setStep(commitment ? 'form' : 'choose')
      detailsOpened.current = false
      setCustomInterval(!INTERVAL_PRESETS.includes(next.intervalMonths))
      setIntervalText(String(next.intervalMonths))
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
      // The field only exists after this render; the select would otherwise keep
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

  const categoryBudget = (
    <>
      <div className="grid grid-cols-2 gap-3">
        <div className="flex flex-col gap-2">
          <Label>{t('common.category')}</Label>
          <CategoryPicker value={draft.category} onChange={handleCategory} />
        </div>

        <div className="flex flex-col gap-2">
          <Label>{t('common.budget')}</Label>
          {budgetIsFixed ? (
            <span className="flex h-9 items-center gap-2 text-sm font-medium">
              <span className={cn('size-2.5 rounded-sm', BUDGET_DOT[draft.budget])} />
              {budgetLabel(draft.budget)}
            </span>
          ) : (
            <Select value={draft.budget} onValueChange={(value) => set('budget', value as Budget)}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {BUDGET_ORDER.map((budget) => (
                  <SelectItem key={budget} value={budget}>
                    {budgetLabel(budget)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}
        </div>
      </div>

      {typeOption.budgetHint && (
        <p className="text-muted-foreground bg-muted rounded-md px-3 py-2 text-xs">
          {t(typeOption.budgetHint)}
        </p>
      )}
    </>
  )

  // Shared between „up front“ and „Weitere Angaben“: a kind asks for some of these
  // right away, the rest keeps them folded away.
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
          {/* "Standardkonto" instead of a fixed preselection: the
                commitment stays right when the default account changes.
                Only set where it differs. */}
          <SelectItem value="default">{t('common.defaultAccount')}</SelectItem>
          {accounts
            .filter((account) => account.active)
            .map((account) => (
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
          placeholder={t('commitmentDialog.noEnd')}
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
  const targetDateField = (
    <div className="flex flex-col gap-2">
      <Label htmlFor="target-date">{t('commitmentDialog.targetDate')}<OptionalMark /></Label>
      <DateField
        id="target-date"
        value={draft.targetDate ?? ''}
        onChange={(iso) => set('targetDate', iso || null)}
        placeholder={t('commitmentDialog.noTargetDate')}
      />
    </div>
  )

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
        choosing ? t('commitmentDialog.chooseDescription') : t('commitmentDialog.description')
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
          <Button type="button" variant="ghost" onClick={() => setStep('choose')}>
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
        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-2">
            <Label htmlFor="name">{t('positionDialog.label')}</Label>
            <Input
              id="name"
              value={draft.name}
              onChange={(event) => set('name', event.target.value)}
              placeholder={t(typeOption.namePlaceholder)}
              required
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-2">
              <Label htmlFor="amount">
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
              />
            </div>

            <div className="flex flex-col gap-2">
              <Label htmlFor="interval">{t('commitmentDialog.interval')}</Label>
              <Select
                value={customInterval ? CUSTOM : String(draft.intervalMonths)}
                onValueChange={handleIntervalSelect}
              >
                <SelectTrigger id="interval">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {INTERVAL_PRESETS.map((months) => (
                    <SelectItem key={months} value={String(months)}>
                      {intervalLabel(months)}
                    </SelectItem>
                  ))}
                  <SelectItem value={CUSTOM}>{t('commitmentDialog.customInterval')}</SelectItem>
                </SelectContent>
              </Select>
              {customInterval && (
                <>
                  <Label htmlFor="interval-custom">
                    {t('commitmentDialog.customIntervalField')}
                  </Label>
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
                    data-own-error
                    required
                  />
                  {/* Always in the DOM so the field can point at it; only the
                      error turns into an announcement. */}
                  <span
                    id="interval-custom-hint"
                    role={intervalValid ? undefined : 'alert'}
                    className={
                      intervalValid ? 'text-muted-foreground text-xs' : 'text-destructive text-xs'
                    }
                  >
                    {t('commitmentDialog.intervalRange', {
                      min: INTERVAL_MIN,
                      max: INTERVAL_MAX,
                    })}
                  </span>
                </>
              )}
            </div>
          </div>

          {draft.type === 'savings_goal' && (
            <div className="grid grid-cols-2 gap-3">
              <div className="flex flex-col gap-2">
                <Label htmlFor="target-amount">{t('commitmentDialog.targetAmount')}<OptionalMark /></Label>
                <AmountField
                  id="target-amount"
                  value={draft.targetAmount ?? ''}
                  onChange={(value) => set('targetAmount', value || null)}
                  allowZero
                />
              </div>
              {targetDateField}
            </div>
          )}

          <div className="flex flex-col gap-2">
            <Label htmlFor="first-due">{t('commitmentDialog.firstDue')}</Label>
            <DateField
              id="first-due"
              value={draft.firstDueDate}
              onChange={handleFirstDueDate}
              describedBy="first-due-hint"
            />
            <p id="first-due-hint" className="text-muted-foreground text-xs">
              {t(
                draft.intervalMonths === 1
                  ? 'commitmentDialog.firstDueHintMonthly'
                  : 'commitmentDialog.firstDueHint'
              )}
            </p>
          </div>

          {(upcoming.length > 0 || dueDayShifts) && (
            <p className="text-muted-foreground bg-muted flex flex-col gap-1 rounded-md px-3 py-2 text-xs">
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
                  {t('commitmentDialog.dayShifts', {
                    day: dueDay,
                    year: shiftYear,
                    februaryDay,
                  })}
                </span>
              )}
            </p>
          )}

          {/* A contract chooses its budget freely, so the question stays up front;
              for the other kinds the budget is fixed and the category is a detail. */}
          {up.category && categoryBudget}

          {up.endsOn && endsOnField}
          {up.account && accountField}
          {up.counterAccount && counterAccountField}

          <MoreDetails
            resetKey={commitment}
            hasValues={commitment !== null && hasExtras(commitment, activeAccounts.length)}
            startOpen={detailsOpened.current}
            onToggle={(opened) => {
              detailsOpened.current = opened
            }}
          >
            {!up.category && categoryBudget}

            {!up.endsOn && endsOnField}

            {/* Account and payment method belong to the commitment, not to the
                month: they are copied into every position and stay overridable
                there. */}
            <div className="grid grid-cols-2 gap-3">
              {!up.account && accountField}
              {!up.counterAccount && counterAccountField}
              {paymentField}
            </div>

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
