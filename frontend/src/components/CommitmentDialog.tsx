import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { DateField } from '@/components/DateField'
import { today } from '@/lib/dates'
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
  isValidInterval,
  dueMonths,
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
 *   debt          → remaining debt
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
    type: 'contract',
    name: '',
    amount: '',
    category: 'housing.rent',
    budget: 'needs',
    isLimit: false,
    householdId: null,
    intervalMonths: 1,
    firstDueDate: null,
    dueDay: 1,
    active: true,
    passThrough: false,
    counterAccountId: null,
    targetAmount: null,
    targetDate: null,
    remainingDebt: null,
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
}

export function CommitmentDialog({
  commitment,
  open,
  onOpenChange,
  onSave,
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

  useEffect(() => {
    if (open) {
      const next = commitment ?? emptyDraft()
      setDraft(next)
      setCustomInterval(!INTERVAL_PRESETS.includes(next.intervalMonths))
      setIntervalText(String(next.intervalMonths))
    }
  }, [open, commitment])

  const isEdit = commitment !== null
  const typeOption = TYPE_OPTIONS.find((option) => option.value === draft.type)!
  const isRecurringIrregular = draft.intervalMonths !== 1
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
        remainingDebt: type === 'debt' ? current.remainingDebt : null,
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
    setDraft((current) => {
      if (intervalMonths === 1) {
        // A first due date exists for an interval other than 1 only.
        return { ...current, intervalMonths, firstDueDate: null }
      }
      // When switching to quarterly and friends, suggest today — better than an
      // empty mandatory field.
      const seed = current.firstDueDate ?? today()
      return {
        ...current,
        intervalMonths,
        firstDueDate: seed,
        dueDay: Number(seed.slice(8, 10)),
      }
    })
  }

  function handleIntervalSelect(value: string) {
    if (value === CUSTOM) {
      setCustomInterval(true)
      return
    }
    setCustomInterval(false)
    setIntervalText(value)
    handleInterval(Number(value))
  }

  function handleIntervalText(value: string) {
    setIntervalText(value)
    const parsed = value.trim() === '' ? 0 : Number(value)
    // An empty or broken field keeps a value the range check rejects, so saving
    // stays blocked until it holds a whole number from 1 to 120.
    if (isValidInterval(parsed)) handleInterval(parsed)
    else setDraft((current) => ({ ...current, intervalMonths: 0 }))
  }

  /** Month and day fall out of the first due date. */
  function handleFirstDueDate(value: string) {
    setDraft((current) => ({
      ...current,
      firstDueDate: value || null,
      // Day and month come from here — two fields about the same thing would
      // otherwise contradict each other, and the backend rejects that.
      dueDay: value ? Number(value.slice(8, 10)) : current.dueDay,
    }))
  }

  function handleSubmit(event: React.FormEvent) {
    event.preventDefault()
    if (!intervalValid) return
    onSave(draft)
    onOpenChange(false)
  }

  // From the 29th on the day can shift — February is the hard case.
  const dueDayShifts = draft.dueDay >= DUE_DAY_MAY_SHIFT
  const shiftYear = draft.firstDueDate
    ? Number(draft.firstDueDate.slice(0, 4))
    : new Date().getFullYear()
  const months = intervalValid
    ? dueMonths(draft.intervalMonths, draft.firstDueDate, shiftYear)
    : []
  const februaryDay = effectiveDueDay(draft.dueDay, shiftYear, 2)

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      {/* Bei „Wird abbezahlt" und auf niedrigen Bildschirmen wird das Formular
          höher als das Fenster — ohne max-h wären Titel und Knöpfe abgeschnitten. */}
      <DialogContent className="max-h-[90svh] overflow-y-auto sm:max-w-lg">
        <form onSubmit={handleSubmit} className="flex flex-col gap-5">
          <DialogHeader>
            <DialogTitle className="font-heading text-xl">
              {isEdit ? t('commitmentDialog.editTitle') : t('commitmentDialog.addTitle')}
            </DialogTitle>
            <DialogDescription>
              {t('commitmentDialog.description')}
            </DialogDescription>
          </DialogHeader>

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
                <Input
                  id="amount"
                  type="number"
                  step="0.01"
                  min="0"
                  inputMode="decimal"
                  value={draft.amount}
                  onChange={(event) => set('amount', event.target.value)}
                  placeholder={t('common.amountPlaceholder')}
                  required
                />
              </div>

              <div className="flex flex-col gap-2">
                <Label>{t('commitmentDialog.interval')}</Label>
                <Select
                  value={customInterval ? CUSTOM : String(draft.intervalMonths)}
                  onValueChange={handleIntervalSelect}
                >
                  <SelectTrigger>
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
                    <Input
                      type="number"
                      min={INTERVAL_MIN}
                      max={INTERVAL_MAX}
                      step="1"
                      inputMode="numeric"
                      value={intervalText}
                      onChange={(event) => handleIntervalText(event.target.value)}
                      aria-label={t('commitmentDialog.customIntervalField')}
                      aria-invalid={!intervalValid}
                      required
                    />
                    {!intervalValid && (
                      <span className="text-destructive text-xs">
                        {t('commitmentDialog.intervalRange', {
                          min: INTERVAL_MIN,
                          max: INTERVAL_MAX,
                        })}
                      </span>
                    )}
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

            {isRecurringIrregular ? (
              <div className="flex flex-col gap-2">
                <Label htmlFor="first-due">{t('commitmentDialog.firstDue')}</Label>
                <DateField
                  id="first-due"
                  value={draft.firstDueDate ?? ''}
                  onChange={handleFirstDueDate}
                />
                <p className="text-muted-foreground text-xs">
                  {t('commitmentDialog.firstDueHint')}
                </p>
              </div>
            ) : (
              <div className="flex flex-col gap-2">
                <Label htmlFor="due-day">{t('common.dueOn')}</Label>
                <Input
                  id="due-day"
                  type="number"
                  min="1"
                  max="31"
                  value={draft.dueDay}
                  onChange={(event) =>
                    set('dueDay', Number(event.target.value))
                  }
                  required
                />
              </div>
            )}

            {(months.length > 0 || dueDayShifts) && (
              <p className="text-muted-foreground bg-muted flex flex-col gap-1 rounded-md px-3 py-2 text-xs">
                {months.length > 0 && (
                  <span>
                    {t('commitmentDialog.dueIn', {
                      day: draft.dueDay,
                      months: months.map((month) => monthLabel(month)).join(', '),
                    })}
                    {draft.firstDueDate
                      ? ` — ${t('commitmentDialog.firstTime', {
                          month: monthLabel(Number(draft.firstDueDate.slice(5, 7))),
                          year: draft.firstDueDate.slice(0, 4),
                        })}`
                      : '.'}
                  </span>
                )}
                {dueDayShifts && (
                  <span>
                    {t('commitmentDialog.dayShifts', {
                      day: draft.dueDay,
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
                  <Input
                    id="target-amount"
                    type="number"
                    step="0.01"
                    min="0"
                    value={draft.targetAmount ?? ''}
                    onChange={(event) =>
                      set('targetAmount', event.target.value || null)
                    }
                    placeholder={t('common.amountPlaceholder')}
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

            {draft.type === 'debt' && (
              <div className="flex flex-col gap-2">
                <Label htmlFor="remaining-debt">{t('commitmentDialog.remainingDebt')}</Label>
                <Input
                  id="remaining-debt"
                  type="number"
                  step="0.01"
                  min="0"
                  value={draft.remainingDebt ?? ''}
                  onChange={(event) =>
                    set('remainingDebt', event.target.value || null)
                  }
                  placeholder={t('common.amountPlaceholder')}
                />
                {/* TODO: Restschuld nach jeder Tilgung fortschreiben — daraus
                    ergibt sich das Datum, an dem die Schuld durch ist. */}
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

            <div className="border-border flex items-center justify-between rounded-md border p-3">
              <div className="flex flex-col">
                <Label htmlFor="active">{t('commitmentDialog.active')}</Label>
                <span className="text-muted-foreground text-xs">
                  {t('commitmentDialog.activeHint')}
                </span>
              </div>
              <Switch
                id="active"
                checked={draft.active}
                onCheckedChange={(checked) => set('active', checked)}
              />
            </div>
          </div>

          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => onOpenChange(false)}
            >
              {t('common.cancel')}
            </Button>
            <Button type="submit" disabled={!intervalValid}>{isEdit ? t('common.save') : t('common.create')}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
