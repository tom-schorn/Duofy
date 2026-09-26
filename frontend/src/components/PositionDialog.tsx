import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { DialogFrame } from '@/components/DialogFrame'
import { AmountField } from '@/components/AmountField'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
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
  BUDGET_SUGGESTION,
  BUDGET_ORDER,
  paymentLabel,
  categoryGroup,
  type Budget,
  type Category,
  type PaymentMethod,
  type PlanPosition,
  PAYMENT_METHODS,
} from '@/lib/domain'
import { useAccounts, useHouseholds } from '@/lib/queries'

/**
 * Create and edit one-off positions.
 *
 * Deliberately short: label, amount, budget — that is all it takes to put a planned
 * purchase into the month. The rest is prefilled and only touched when needed.
 *
 * Anything recurring does **not** belong here but on the commitments page. A
 * commitment generates its positions itself, every month anew.
 */

const PAYMENTS = PAYMENT_METHODS

/** The matching category, so the budget does not jump the moment it is picked. */
const DEFAULT_CATEGORY: Record<Budget, Category> = {
  income: 'income.earned',
  needs: 'housing.rent',
  wants: 'leisure.hobbies',
  savings: 'finance.savings',
}

function emptyDraft(budget: Budget): PlanPosition {
  return {
    id: '',
    label: '',
    amountPlanned: '',
    amountActual: null,
    category: DEFAULT_CATEGORY[budget],
    budget,
    dueDay: 1,
    accountId: null,
    isLimit: false,
    counterAccountId: null,
    passThrough: false,
    paymentMethod: null,
    householdId: null,
    commitmentId: null,
    paidAt: null,
  }
}

type Props = {
  /** null means create, otherwise edit. */
  position: PlanPosition | null
  /** The budget the position should land in — prefilled when creating. */
  budget: Budget
  /** Which plan the new position belongs to. */
  planId: string
  open: boolean
  onOpenChange: (open: boolean) => void
  onSave: (position: PlanPosition) => void
  /** The server has not answered yet; the dialog stays open and locked. */
  pending?: boolean
  /** The server said no; shown above the buttons, the input stays. */
  error?: unknown
}

export function PositionDialog({
  position,
  budget,
  planId: _planId,
  open,
  onOpenChange,
  onSave,
  pending = false,
  error = null,
}: Props) {
  const { t } = useTranslation()
  const households = useHouseholds().data ?? []
  const accounts = useAccounts().data ?? []
  const [draft, setDraft] = useState<PlanPosition>(
    position ?? emptyDraft(budget)
  )

  useEffect(() => {
    if (open) setDraft(position ?? emptyDraft(budget))
  }, [open, position, budget])

  const isEdit = position !== null
  const dirty =
    JSON.stringify(draft) !== JSON.stringify(position ?? emptyDraft(budget))
  // Positions generated from a commitment have a source — label and assignment
  // then belong to the commitment, not to the single month.
  const fromCommitment = draft.commitmentId !== null

  function set<K extends keyof PlanPosition>(
    key: K,
    value: PlanPosition[K]
  ) {
    setDraft((current) => ({ ...current, [key]: value }))
  }

  // Every category under Einnahmen leads to the same budget, so there is nothing
  // left to pick. Showing the field anyway asks the same question twice — under a
  // heading that reads "Einnahmen" on both sides.
  const budgetIsFixed = categoryGroup(draft.category) === 'income'

  function handleCategory(category: Category) {
    setDraft((current) => ({
      ...current,
      category,
      budget: BUDGET_SUGGESTION[category],
    }))
  }

  function handleSubmit(event: React.FormEvent) {
    event.preventDefault()
    // The backend records changes to other people positions itself.
    // Stays open until the server agrees — the caller closes it on success.
    onSave(draft)
  }

  return (
    <DialogFrame
      open={open}
      onOpenChange={onOpenChange}
      title={isEdit ? t('positionDialog.editTitle') : t('positionDialog.addTitle')}
      description={
        fromCommitment
          ? t('positionDialog.fromCommitment')
          : t('positionDialog.oneOff')
      }
      submitLabel={isEdit ? t('common.save') : t('common.create')}
      onSubmit={handleSubmit}
      dirty={dirty}
      pending={pending}
      error={error}
    >
      <div className="flex flex-col gap-4">
        <div className="flex flex-col gap-2">
          <Label htmlFor="label">{t('positionDialog.label')}</Label>
          <Input
            id="label"
            value={draft.label}
            onChange={(event) => set('label', event.target.value)}
            placeholder={t('positionDialog.labelPlaceholder')}
            required
          />
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div className="flex flex-col gap-2">
            <Label htmlFor="planned">{t('common.amount')}</Label>
            <AmountField
              id="planned"
              value={draft.amountPlanned}
              onChange={(value) => set('amountPlanned', value)}
              required
              allowZero
            />
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="pos-due-day">{t('common.dueOn')}</Label>
            <Input
              id="pos-due-day"
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
        </div>

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
                  {/* BUDGET_ORDER statt BUDGETS: sonst fehlt „Einnahmen"
                      und ein Einnahme-Posten wäre nicht bearbeitbar. */}
                  {BUDGET_ORDER.map((option) => (
                    <SelectItem key={option} value={option}>
                      {budgetLabel(option)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          </div>
        </div>

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
                {/* „Standardkonto" statt einer Vorauswahl: so bleibt der
                    Posten richtig, wenn du das Standardkonto wechselst. */}
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
                <SelectItem value="none">{t('common.paymentOpen')}</SelectItem>
                {PAYMENTS.map((method) => (
                  <SelectItem key={method} value={method}>
                    {paymentLabel(method)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

        {/* Nimmt den Posten aus Budget und Quoten. Nötig für Geld, das
              nur durchgereicht wird — sonst sähen 1.139 € weitergeleitet
              aus wie 1.139 € gespart. */}
          <div className="border-border flex items-center justify-between rounded-md border p-3">
            <div className="flex flex-col pr-4">
              <Label htmlFor="position-pass-through">{t('common.passThrough')}</Label>
              <span className="text-muted-foreground text-xs">
                {t('common.passThroughHint')}
              </span>
            </div>
            <Switch
              id="position-pass-through"
              checked={draft.passThrough}
              onCheckedChange={(checked) => set('passThrough', checked)}
            />
          </div>

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
          </div>
        </div>

        {isEdit && (
          <div className="flex flex-col gap-2">
            <Label htmlFor="actual">{t('positionDialog.actual')}</Label>
            <AmountField
              id="actual"
              value={draft.amountActual ?? ''}
              onChange={(value) => set('amountActual', value || null)}
              allowZero
              placeholder={t('positionDialog.actualPlaceholder')}
            />
            <p className="text-muted-foreground text-xs">
              {t('positionDialog.actualHint')}
            </p>
          </div>
        )}
      </div>
    </DialogFrame>
  )
}
