import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { DialogFrame } from '@/components/DialogFrame'
import { AmountField } from '@/components/AmountField'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { MoreDetails } from '@/components/MoreDetails'
import { Switch } from '@/components/ui/switch'
import { SentenceWord } from '@/components/SentenceWord'
import { SentencePanel } from '@/components/SentencePanel'
import { SentenceChip } from '@/components/SentenceChip'
import { fillSentence } from '@/lib/sentence'
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
  budgetHeadingId,
  budgetLabel,
  BUDGET_SUGGESTION,
  BUDGET_ORDER,
  categoryLabel,
  paymentLabel,
  categoryGroup,
  type Budget,
  type Category,
  type PaymentMethod,
  type PlanPosition,
  PAYMENT_METHODS,
} from '@/lib/domain'
import { useAccounts, useHouseholds } from '@/lib/queries'
import { OptionalMark } from '@/components/OptionalMark'

/**
 * Create and edit one-off positions.
 *
 * Deliberately short: label and amount stay a form; day and budget read as one
 * sentence with clickable words (issue #215, decision 28 — „Satz statt
 * Formular“). Everything rarer sits behind a text link.
 *
 * Same structure as the commitment dialog (#190): creating first asks what it is —
 * Verpflichtung (with a tick) or Limit — then the fields follow, with a way back.
 * Editing has no cards; the kind is named in the title.
 *
 * Anything recurring does **not** belong here but on the commitments page. A
 * commitment generates its positions itself, every month anew.
 */

const PAYMENTS = PAYMENT_METHODS

const KINDS = [
  {
    limit: false,
    label: 'positionDialog.kinds.obligation.label',
    hint: 'positionDialog.kinds.obligation.hint',
    addTitle: 'positionDialog.kinds.obligation.addTitle',
    editTitle: 'positionDialog.kinds.obligation.editTitle',
  },
  {
    limit: true,
    label: 'positionDialog.kinds.limit.label',
    hint: 'positionDialog.kinds.limit.hint',
    addTitle: 'positionDialog.kinds.limit.addTitle',
    editTitle: 'positionDialog.kinds.limit.editTitle',
  },
] as const

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
  /**
   * Absent or null: this person may not delete, so there is no button (rule 3).
   * Needed when acting on somebody else's plan: changing is recorded and
   * reversible, deleting is neither.
   */
  onDelete?: ((position: PlanPosition) => void) | null
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
  onDelete = null,
}: Props) {
  const { t } = useTranslation()
  const households = useHouseholds().data ?? []
  const accounts = useAccounts().data ?? []
  const [draft, setDraft] = useState<PlanPosition>(position ?? emptyDraft(budget))

  // Creating starts with the question, editing goes straight to the fields.
  const [step, setStep] = useState<'choose' | 'form'>(position ? 'form' : 'choose')

  // A deleted position leaves no row to return the focus to.
  const deleted = useRef(false)

  // Whether the rare fields were open; kept across „Zurück“, where they go away.
  const detailsOpened = useRef(false)

  // Which sentence word is open — only one at a time (issue #215).
  const [openWord, setOpenWord] = useState<string | null>(null)
  const wordRefs = useRef<Record<string, HTMLButtonElement | null>>({})

  useEffect(() => {
    if (open) {
      setDraft(position ?? emptyDraft(budget))
      setStep(position ? 'form' : 'choose')
      deleted.current = false
      detailsOpened.current = false
      setOpenWord(null)
    }
  }, [open, position, budget])

  const isEdit = position !== null
  const choosing = step === 'choose'
  const kind = KINDS.find((item) => item.limit === draft.isLimit)!
  // Choosing a card is no change yet: compare against a fresh draft of the same kind.
  const baseline = position ?? {
    ...emptyDraft(budget),
    isLimit: draft.isLimit,
  }
  const dirty = JSON.stringify(draft) !== JSON.stringify(baseline)

  function handleKind(isLimit: boolean) {
    setDraft((current) => ({ ...current, isLimit }))
    setStep('form')
    setOpenWord(null)
  }

  function handleBack() {
    setStep('choose')
    setOpenWord(null)
  }
  // Positions generated from a commitment have a source — label and assignment
  // then belong to the commitment, not to the single month.
  const fromCommitment = draft.commitmentId !== null

  function set<K extends keyof PlanPosition>(key: K, value: PlanPosition[K]) {
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

  const dueDayField = (
    <div className="flex flex-col gap-2">
      <Label htmlFor="pos-due-day">{t('common.dueOn')}</Label>
      <div className="flex items-center gap-2 text-sm">
        {t('common.dueOnBefore')}
        <Input
          id="pos-due-day"
          type="number"
          min="1"
          max="31"
          className="w-20"
          value={draft.dueDay}
          onChange={(event) => set('dueDay', Number(event.target.value))}
          aria-describedby="pos-due-day-sentence"
          required
        />
        {t('common.dueOnAfter')}
        <span id="pos-due-day-sentence" className="sr-only">
          {t('common.dueOnSentence', { day: draft.dueDay })}
        </span>
      </div>
    </div>
  )

  // --- Sentence words -------------------------------------------------------

  const dueDayWord = (
    <SentenceWord ref={wordRef('dueDay')} open={openWord === 'dueDay'} onClick={() => toggleWord('dueDay')}>
      {t('common.dueDay', { day: draft.dueDay })}
    </SentenceWord>
  )
  const dueDayPanel = openWord === 'dueDay' && (
    <SentencePanel label={t('positionDialog.dueDayLabel')}>
      <Label htmlFor="pos-due-day-word" className="sr-only">
        {t('positionDialog.dueDayLabel')}
      </Label>
      <Input
        id="pos-due-day-word"
        type="number"
        min="1"
        max="31"
        value={draft.dueDay}
        onChange={(event) => set('dueDay', Number(event.target.value))}
        required
        className="w-24"
      />
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
    <SentencePanel label={t('positionDialog.budgetLabel')}>
      <div className="flex flex-wrap gap-2">
        {BUDGET_ORDER.map((option) => (
          <SentenceChip
            key={option}
            selected={draft.budget === option}
            onClick={() => {
              set('budget', option)
              closeWord()
            }}
          >
            <span className={cn('size-2.5 rounded-sm', BUDGET_DOT[option])} />
            {budgetLabel(option)}
          </SentenceChip>
        ))}
      </div>
    </SentencePanel>
  )

  const words: Record<string, React.ReactNode> = { dueDay: dueDayWord, budget: budgetWord }
  const panels = [dueDayPanel, budgetPanel]
  const sentenceKey = draft.isLimit ? 'limit' : 'obligation'

  const accountName = (id: string | null) =>
    id === null ? t('common.defaultAccount') : (accounts.find((account) => account.id === id)?.name ?? '')

  // A quiet line under the link when something rare is already set — the section
  // itself stays collapsed regardless (same rule as the commitment dialog).
  const extrasSummary = [
    draft.category !== DEFAULT_CATEGORY[draft.budget] &&
      t('common.extrasSummary.category', { value: categoryLabel(draft.category) }),
    draft.accountId !== null &&
      t('common.extrasSummary.account', { value: accountName(draft.accountId) }),
    draft.counterAccountId !== null &&
      t('common.extrasSummary.counterAccount', { value: accountName(draft.counterAccountId) }),
    draft.paymentMethod !== null &&
      t('common.extrasSummary.paymentMethod', { value: paymentLabel(draft.paymentMethod) }),
    draft.householdId !== null &&
      t('common.extrasSummary.assignment', {
        value: households.find((household) => household.id === draft.householdId)?.name ?? '',
      }),
    draft.passThrough && t('common.extrasSummary.passThrough'),
    draft.amountActual !== null &&
      t('common.extrasSummary.actual', { value: `${draft.amountActual} €` }),
  ]
    .filter((part): part is string => Boolean(part))
    .join(' · ')

  return (
    <DialogFrame
      open={open}
      focusKey={step}
      onOpenChange={onOpenChange}
      title={
        choosing ? t('positionDialog.chooseTitle') : isEdit ? t(kind.editTitle) : t(kind.addTitle)
      }
      description={
        choosing
          ? t('positionDialog.chooseDescription')
          : // Unlike the commitment dialog's create hint, this names where the
            // fields belong (this month only, or a commitment) — true in both
            // create and edit.
            fromCommitment
            ? t('positionDialog.fromCommitment')
            : t('positionDialog.oneOff')
      }
      submitLabel={isEdit ? t('common.save') : t('common.create')}
      hideSubmit={choosing}
      onSubmit={choosing ? (event) => event.preventDefault() : handleSubmit}
      dirty={!choosing && dirty}
      pending={pending}
      error={error}
      returnFocus={() =>
        // The section the row was in, not the one the draft was moved to.
        deleted.current && position
          ? document.getElementById(budgetHeadingId(position.budget))
          : null
      }
      start={
        isEdit && onDelete !== null ? (
          <Button
            type="button"
            variant="ghost"
            className="text-destructive hover:text-destructive"
            disabled={pending}
            onClick={() => {
              deleted.current = true
              onDelete(draft)
              onOpenChange(false)
            }}
          >
            {t('common.delete')}
          </Button>
        ) : !isEdit && !choosing ? (
          <Button type="button" variant="ghost" onClick={handleBack}>
            {t('positionDialog.back')}
          </Button>
        ) : undefined
      }
    >
      {choosing ? (
        <div className="grid gap-3 sm:grid-cols-2">
          {KINDS.map((item) => (
            <button
              key={item.label}
              type="button"
              data-dialog-card
              onClick={() => handleKind(item.limit)}
              className="border-border hover:bg-muted focus-visible:ring-ring flex flex-col gap-1 rounded-md border p-3 text-left focus-visible:ring-2 focus-visible:outline-none"
            >
              <span className="font-medium">{t(item.label)}</span>
              <span className="text-muted-foreground text-xs">{t(item.hint)}</span>
            </button>
          ))}
        </div>
      ) : (
        <div className="flex flex-col gap-6">
          <div className="flex flex-col gap-2">
            <Label htmlFor="label" className="sr-only">
              {t('positionDialog.label')}
            </Label>
            <Input
              id="label"
              value={draft.label}
              onChange={(event) => set('label', event.target.value)}
              placeholder={t('positionDialog.labelPlaceholder')}
              required
              className="font-heading h-auto rounded-none border-0 border-b border-border bg-transparent px-0 pb-2 text-3xl placeholder:text-muted-foreground focus-visible:border-primary focus-visible:ring-0 md:text-3xl"
            />

            <div className="flex items-baseline gap-3">
              <Label htmlFor="planned" className="text-muted-foreground shrink-0 text-sm">
                {t('common.amount')}
              </Label>
              <AmountField
                id="planned"
                value={draft.amountPlanned}
                onChange={(value) => set('amountPlanned', value)}
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
              if (event.key !== 'Escape' || openWord === null) return
              event.stopPropagation()
              event.preventDefault()
              closeWord()
            }}
          >
            <p className="text-lg leading-8">
              {fillSentence(t(`positionDialog.sentence.${sentenceKey}`), words)}
            </p>
            {panels}
          </div>

          <MoreDetails
            resetKey={position}
            hasValues={false}
            startOpen={detailsOpened.current}
            onToggle={(opened) => {
              detailsOpened.current = opened
            }}
            label={t('positionDialog.addDetails')}
            plain
            summary={extrasSummary || undefined}
          >
            {draft.isLimit && dueDayField}

            <div className="flex flex-col gap-2">
              <Label>{t('common.category')}</Label>
              <CategoryPicker value={draft.category} onChange={handleCategory} />
            </div>

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
              <span className="text-muted-foreground text-xs">
                {t('common.counterAccountHint')}
              </span>
            </div>

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
            </div>

            {isEdit && (
              <div className="flex flex-col gap-2">
                <Label htmlFor="actual">{t('positionDialog.actual')}<OptionalMark /></Label>
                <AmountField
                  id="actual"
                  value={draft.amountActual ?? ''}
                  onChange={(value) => set('amountActual', value || null)}
                  allowZero
                  placeholder={t('positionDialog.actualPlaceholder')}
                />
                <p className="text-muted-foreground text-xs">{t('positionDialog.actualHint')}</p>
              </div>
            )}
          </MoreDetails>
        </div>
      )}
    </DialogFrame>
  )
}
