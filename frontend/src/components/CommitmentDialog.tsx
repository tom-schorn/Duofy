import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { DialogFrame } from '@/components/DialogFrame'
import { Button } from '@/components/ui/button'
import { AmountField } from '@/components/AmountField'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { DateField } from '@/components/DateField'
import { firstOfNextMonth } from '@/lib/dates'
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

/**
 * One form for every commitment — savings plans and loans are commitments too.
 *
 * The type comes first, in everyday words, and drives the extra fields:
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

const TYPE_OPTIONS: {
  value: CommitmentType
  label: string
  hint: string
  /** Why the budget is fixed — shown in place of the picker. Catalog keys, like the other texts. */
  budgetHint: string | null
  namePlaceholder: string
  defaultCategory: Category
}[] = [
  {
    value: 'contract',
    label: 'commitmentDialog.types.contract.label',
    hint: 'commitmentDialog.types.contract.hint',
    budgetHint: null,
    namePlaceholder: 'commitmentDialog.types.contract.namePlaceholder',
    defaultCategory: 'housing.rent',
  },
  {
    value: 'savings_goal',
    label: 'commitmentDialog.types.savings_goal.label',
    hint: 'commitmentDialog.types.savings_goal.hint',
    budgetHint: 'commitmentDialog.types.savings_goal.budgetHint',
    namePlaceholder: 'commitmentDialog.types.savings_goal.namePlaceholder',
    defaultCategory: 'finance.savings',
  },
  {
    value: 'debt',
    label: 'commitmentDialog.types.debt.label',
    hint: 'commitmentDialog.types.debt.hint',
    // The reason, in one sentence.
    budgetHint: 'commitmentDialog.types.debt.budgetHint',
    namePlaceholder: 'commitmentDialog.types.debt.namePlaceholder',
    defaultCategory: 'finance.debt',
  },
  {
    value: 'income',
    label: 'commitmentDialog.types.income.label',
    hint: 'commitmentDialog.types.income.hint',
    budgetHint: 'commitmentDialog.types.income.budgetHint',
    namePlaceholder: 'commitmentDialog.types.income.namePlaceholder',
    defaultCategory: 'income.earned',
  },
]

const PAYMENTS = PAYMENT_METHODS
/** Select value that opens the number field for any other distance. */
const CUSTOM = 'custom'

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
      setCustomInterval(!INTERVAL_PRESETS.includes(next.intervalMonths))
      setIntervalText(String(next.intervalMonths))
    }
  }, [open, commitment])

  const isEdit = commitment !== null
  const dirty = JSON.stringify(draft) !== JSON.stringify(commitment ?? emptyDraft())
  const typeOption = TYPE_OPTIONS.find((option) => option.value === draft.type)!
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

  /**
   * Switching the type clears the extra fields that do not belong to the new one —
   * otherwise the form sends values the database rejects.
   */
  function handleType(type: CommitmentType) {
    // Clicking the type that is already chosen changes nothing — and must not make
    // the dialog dirty.
    if (type === draft.type) return
    const option = TYPE_OPTIONS.find((item) => item.value === type)!
    setDraft((current) => {
      // Only follow along with the category if it still holds the old suggestion —
      // or if it sits on the wrong side of the income line. The income group and
      // the income type belong together in both directions: a salary filed under
      // Miete is as wrong as a contract filed under Gehalt.
      const categoryFits = (categoryGroup(current.category) === 'income') === (type === 'income')
      const category =
        categoryFits && current.category !== typeOption.defaultCategory
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
        isLimit: type === 'contract' ? current.isLimit : false,
        targetAmount: type === 'savings_goal' ? current.targetAmount : null,
        targetDate: type === 'savings_goal' ? current.targetDate : null,
      }
    })
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

  return (
    // The frame caps the height: with "debt" and on low screens the form is
    // taller than the window, and title and buttons would be cut off.
    <DialogFrame
      open={open}
      onOpenChange={onOpenChange}
      className="sm:max-w-lg"
      title={isEdit ? t('commitmentDialog.editTitle') : t('commitmentDialog.addTitle')}
      description={t('commitmentDialog.description')}
      submitLabel={isEdit ? t('common.save') : t('common.create')}
      onSubmit={handleSubmit}
      submitDisabled={!intervalValid}
      dirty={dirty}
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
        ) : undefined
      }
    >
      <div className="flex flex-col gap-2">
        <div className="border-border grid grid-cols-3 gap-1 rounded-md border p-1">
          {TYPE_OPTIONS.map((option) => (
            <Button
              key={option.value}
              type="button"
              variant={draft.type === option.value ? 'default' : 'ghost'}
              size="sm"
              onClick={() => handleType(option.value)}
              aria-pressed={draft.type === option.value}
            >
              {t(option.label)}
            </Button>
          ))}
        </div>
        <p className="text-muted-foreground text-xs">{t(typeOption.hint)}</p>
      </div>

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
              {draft.type === 'debt' ? t('commitmentDialog.rate') : t('common.amount')}
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
                  required
                />
                {/* Always in the DOM so the field can point at it; only the
                    error turns into an announcement. */}
                <span
                  id="interval-custom-hint"
                  role={intervalValid ? undefined : 'alert'}
                  className={
                    intervalValid
                      ? 'text-muted-foreground text-xs'
                      : 'text-destructive text-xs'
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

        {/* Beide gehören an den Vertrag, nicht an den Monat — sie werden
            beim Erzeugen in jeden Posten kopiert und bleiben dort
            überschreibbar. */}
        <div className="grid grid-cols-2 gap-3">
          <div className="flex flex-col gap-2">
            <Label>{t('common.account')}</Label>
            <Select
              value={draft.accountId ?? 'default'}
              onValueChange={(value) =>
                set('accountId', value === 'default' ? null : value)
              }
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {/* „Standardkonto" statt fester Vorauswahl: der Vertrag
                    bleibt richtig, wenn du das Standardkonto wechselst.
                    Gesetzt wird es nur, wo es abweicht — das Claude-Abo
                    läuft über die Kreditkarte, nicht übers Giro. */}
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
          <div className="flex flex-col gap-2">
            <Label>{t('common.counterAccount')}</Label>
            <Select
              value={draft.counterAccountId ?? 'none'}
              onValueChange={(value) =>
                set('counterAccountId', value === 'none' ? null : value)
              }
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
            <span className="text-muted-foreground text-xs">
              {t('common.counterAccountHint')}
            </span>
          </div>


        <div className="flex flex-col gap-2">
          <Label>{t('common.paymentMethod')}</Label>
          <Select
            value={draft.paymentMethod ?? 'none'}
            onValueChange={(value) =>
              set(
                'paymentMethod',
                value === 'none' ? null : (value as PaymentMethod)
              )
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
        </div>

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

        <div className="grid grid-cols-2 gap-3">
          <div className="flex flex-col gap-2">
            <Label>{t('common.category')}</Label>
            <CategoryPicker
              value={draft.category}
              onChange={handleCategory}
            />
          </div>

          <div className="flex flex-col gap-2">
            <Label>{t('common.budget')}</Label>
            {budgetIsFixed ? (
              <span className="flex h-9 items-center gap-2 text-sm font-medium">
                <span className={cn('size-2.5 rounded-sm', BUDGET_DOT[draft.budget])} />
                {budgetLabel(draft.budget)}
              </span>
            ) : (
              <Select
                value={draft.budget}
                onValueChange={(value) => set('budget', value as Budget)}
              >
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

        {/* Only for a contract ("Läuft weiter"): a savings goal, a debt and
            income have a fixed amount or none at all, so the limit checkbox
            only decides something where the amount is chosen freely and
            recurs. Replaces the former type of its own ("Setze ich selbst"):
            existing contracts of that type were migrated here with the flag
            set. The backend rejects a limit on any other type. */}
        {draft.type === 'contract' && (
          <div className="border-border flex items-center justify-between rounded-md border p-3">
            <div className="flex flex-col pr-4">
              <Label htmlFor="commitment-limit">{t('commitmentDialog.limit')}</Label>
              <span className="text-muted-foreground text-xs">
                {t('commitmentDialog.limitHint')}
              </span>
            </div>
            <Switch
              id="commitment-limit"
              checked={draft.isLimit}
              onCheckedChange={(checked) => set('isLimit', checked)}
            />
          </div>
        )}

        <div className="flex flex-col gap-2">
          <Label>{t('common.assignment')}</Label>
          <Select
            value={draft.householdId ?? 'private'}
            onValueChange={(value) =>
              set('householdId', value === 'private' ? null : value)
            }
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

        {draft.type === 'savings_goal' && (
          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-2">
              <Label htmlFor="target-amount">{t('commitmentDialog.targetAmount')}</Label>
              <AmountField
                id="target-amount"
                value={draft.targetAmount ?? ''}
                onChange={(value) => set('targetAmount', value || null)}
                allowZero
              />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="target-date">{t('commitmentDialog.targetDate')}</Label>
              <DateField
                id="target-date"
                value={draft.targetDate ?? ''}
                onChange={(iso) => set('targetDate', iso || null)}
                placeholder={t('commitmentDialog.noTargetDate')}
              />
            </div>
          </div>
        )}

        {/* Nimmt den Posten aus Budget und Quoten. Nötig für Geld, das
            nur durchgereicht wird — sonst sähen 1.139 € weitergeleitet
            aus wie 1.139 € gespart. */}
        <div className="border-border flex items-center justify-between rounded-md border p-3">
          <div className="flex flex-col pr-4">
            <Label htmlFor="commitment-pass-through">{t('common.passThrough')}</Label>
            <span className="text-muted-foreground text-xs">
              {t('common.passThroughHint')}
            </span>
          </div>
          <Switch
            id="commitment-pass-through"
            checked={draft.passThrough}
            onCheckedChange={(checked) => set('passThrough', checked)}
          />
        </div>

        <div className="flex flex-col gap-2">
          <Label htmlFor="ends-on">{t('commitmentDialog.endsOn')}</Label>
          <div className="flex gap-2">
            <DateField
              id="ends-on"
              value={draft.endsOn ?? ''}
              onChange={(iso) => set('endsOn', iso || null)}
              placeholder={t('commitmentDialog.noEnd')}
              describedBy="ends-on-hint"
            />
            {draft.endsOn !== null && (
              <Button
                type="button"
                variant="outline"
                onClick={() => set('endsOn', null)}
              >
                {t('commitmentDialog.clearEnd')}
              </Button>
            )}
          </div>
          <p id="ends-on-hint" className="text-muted-foreground text-xs">
            {t('commitmentDialog.endsOnHint')}
          </p>
        </div>
      </div>
    </DialogFrame>
  )
}
