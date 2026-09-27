import { useEffect, useId, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { DialogFrame } from '@/components/DialogFrame'
import { AmountField } from '@/components/AmountField'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { SentenceWord } from '@/components/SentenceWord'
import { SentencePanel } from '@/components/SentencePanel'
import { SentenceChip } from '@/components/SentenceChip'
import { fillSentence } from '@/lib/sentence'
import { CategoryPicker } from '@/components/CategoryPicker'
import { formatAmount } from '@/lib/amount'
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
  type PlanPosition,
  PAYMENT_METHODS,
} from '@/lib/domain'
import { useAccounts, useHouseholds } from '@/lib/queries'

/**
 * Create and edit one-off positions.
 *
 * Deliberately short: label and amount stay a form; day and budget read as one
 * sentence with clickable words (issue #215, decision 28 — „Satz statt
 * Formular“). Everything rarer — category, account, payment, assignment,
 * passthrough, the actual amount — reads as a second, quieter sentence, same
 * mechanics (review D-215-4: no collapsed link left in this dialog).
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

  // Which sentence word is open — only one at a time (issue #215).
  const [openWord, setOpenWord] = useState<string | null>(null)
  const wordRefs = useRef<Record<string, HTMLButtonElement | null>>({})
  // Read out with every word and every opened field, so a screen reader hears the
  // whole sentence, not just the one word (issue #202, review D-215-3, fix 1).
  const sentenceId = useId()
  // The quiet second sentence for category, account, payment and the rest
  // (issue #215, review D-215-4).
  const extrasSentenceId = useId()

  useEffect(() => {
    if (open) {
      setDraft(position ?? emptyDraft(budget))
      setStep(position ? 'form' : 'choose')
      deleted.current = false
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

  // --- Sentence words -------------------------------------------------------

  const dueDayWord = (
    <SentenceWord
      ref={wordRef('dueDay')}
      open={openWord === 'dueDay'}
      onClick={() => toggleWord('dueDay')}
      describedBy={sentenceId}
    >
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
        aria-describedby={sentenceId}
        required
        className="w-24"
      />
    </SentencePanel>
  )

  const budgetWord = budgetIsFixed ? (
    <span className="font-medium">{budgetLabel(draft.budget)}</span>
  ) : (
    <SentenceWord
      ref={wordRef('budget')}
      open={openWord === 'budget'}
      onClick={() => toggleWord('budget')}
      describedBy={sentenceId}
    >
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
  // The limit kind names its due day in the second sentence instead (below), so
  // its panel must not also render here — the same button, wherever it sits,
  // opens the one panel.
  const panels = [!draft.isLimit && dueDayPanel, budgetPanel]
  const sentenceKey = draft.isLimit ? 'limit' : 'obligation'

  // --- Rare facts, as a second sentence (issue #215, review D-215-4) --------

  const accountName = (id: string | null) =>
    id === null ? t('common.defaultAccount') : (accounts.find((account) => account.id === id)?.name ?? '')

  const categoryWord = (
    <SentenceWord
      ref={wordRef('category')}
      open={openWord === 'category'}
      onClick={() => toggleWord('category')}
      describedBy={extrasSentenceId}
    >
      {categoryLabel(draft.category)}
    </SentenceWord>
  )
  const categoryPanel = openWord === 'category' && (
    <SentencePanel label={t('common.category')}>
      <CategoryPicker value={draft.category} onChange={handleCategory} />
    </SentencePanel>
  )

  const accountWord = (
    <SentenceWord
      ref={wordRef('account')}
      open={openWord === 'account'}
      onClick={() => toggleWord('account')}
      describedBy={extrasSentenceId}
    >
      {accountName(draft.accountId)}
    </SentenceWord>
  )
  const accountPanel = openWord === 'account' && (
    <SentencePanel label={t('common.account')}>
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
        {accounts
          .filter((account) => account.active)
          .map((account) => (
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
      describedBy={extrasSentenceId}
    >
      {draft.counterAccountId === null ? t('common.goesOut') : accountName(draft.counterAccountId)}
    </SentenceWord>
  )
  const counterAccountPanel = openWord === 'counterAccount' && (
    <SentencePanel label={t('common.counterAccount')}>
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

  const paymentWord = (
    <SentenceWord
      ref={wordRef('payment')}
      open={openWord === 'payment'}
      onClick={() => toggleWord('payment')}
      describedBy={extrasSentenceId}
    >
      {draft.paymentMethod === null ? t('common.paymentOpen') : paymentLabel(draft.paymentMethod)}
    </SentenceWord>
  )
  const paymentPanel = openWord === 'payment' && (
    <SentencePanel label={t('common.paymentMethod')}>
      <div className="flex flex-wrap gap-2">
        <SentenceChip
          selected={draft.paymentMethod === null}
          onClick={() => {
            set('paymentMethod', null)
            closeWord()
          }}
        >
          {t('common.paymentOpen')}
        </SentenceChip>
        {PAYMENTS.map((method) => (
          <SentenceChip
            key={method}
            selected={draft.paymentMethod === method}
            onClick={() => {
              set('paymentMethod', method)
              closeWord()
            }}
          >
            {paymentLabel(method)}
          </SentenceChip>
        ))}
      </div>
    </SentencePanel>
  )

  const assignmentWord = (
    <SentenceWord
      ref={wordRef('assignment')}
      open={openWord === 'assignment'}
      onClick={() => toggleWord('assignment')}
      describedBy={extrasSentenceId}
    >
      {draft.householdId === null
        ? t('common.privateOnly')
        : (households.find((household) => household.id === draft.householdId)?.name ?? '')}
    </SentenceWord>
  )
  const assignmentPanel = openWord === 'assignment' && (
    <SentencePanel label={t('common.assignment')}>
      <div className="flex flex-wrap gap-2">
        <SentenceChip
          selected={draft.householdId === null}
          onClick={() => {
            set('householdId', null)
            closeWord()
          }}
        >
          {t('common.privateOnly')}
        </SentenceChip>
        {households.map((household) => (
          <SentenceChip
            key={household.id}
            selected={draft.householdId === household.id}
            onClick={() => {
              set('householdId', household.id)
              closeWord()
            }}
          >
            {household.name}
          </SentenceChip>
        ))}
      </div>
    </SentencePanel>
  )

  const passThroughWord = (
    <SentenceWord
      ref={wordRef('passThrough')}
      open={openWord === 'passThrough'}
      onClick={() => toggleWord('passThrough')}
      describedBy={extrasSentenceId}
    >
      {draft.passThrough ? t('common.passThroughOn') : t('common.passThroughOff')}
    </SentenceWord>
  )
  const passThroughPanel = openWord === 'passThrough' && (
    <SentencePanel label={t('common.passThrough')}>
      <div className="flex flex-wrap gap-2">
        <SentenceChip
          selected={!draft.passThrough}
          onClick={() => {
            set('passThrough', false)
            closeWord()
          }}
        >
          {t('common.passThroughOff')}
        </SentenceChip>
        <SentenceChip
          selected={draft.passThrough}
          onClick={() => {
            set('passThrough', true)
            closeWord()
          }}
        >
          {t('common.passThroughOn')}
        </SentenceChip>
      </div>
      <p className="text-muted-foreground text-xs">{t('common.passThroughHint')}</p>
    </SentencePanel>
  )

  const actualWord = (
    <SentenceWord
      ref={wordRef('actual')}
      open={openWord === 'actual'}
      onClick={() => toggleWord('actual')}
      describedBy={extrasSentenceId}
    >
      {draft.amountActual === null
        ? t('positionDialog.actualOpen')
        : t('positionDialog.actualBooked', { amount: formatAmount(draft.amountActual) })}
    </SentenceWord>
  )
  const actualPanel = openWord === 'actual' && (
    <SentencePanel label={t('positionDialog.actual')}>
      <AmountField
        id="actual"
        value={draft.amountActual ?? ''}
        onChange={(value) => set('amountActual', value || null)}
        allowZero
        placeholder={t('positionDialog.actualPlaceholder')}
        aria-describedby={extrasSentenceId}
        className="w-48"
      />
      <p className="text-muted-foreground text-xs">{t('positionDialog.actualHint')}</p>
    </SentencePanel>
  )

  const extrasWords: Record<string, React.ReactNode> = {
    dueDay: dueDayWord,
    category: categoryWord,
    account: accountWord,
    counterAccount: counterAccountWord,
    payment: paymentWord,
    assignment: assignmentWord,
    passThrough: passThroughWord,
    actual: actualWord,
  }
  const extrasPanels = [
    draft.isLimit && dueDayPanel,
    categoryPanel,
    accountPanel,
    counterAccountPanel,
    paymentPanel,
    assignmentPanel,
    passThroughPanel,
    isEdit && actualPanel,
  ]
  const extrasSentenceKey = `${draft.isLimit ? 'limit' : 'obligation'}${isEdit ? 'Edit' : ''}`

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
              className="font-heading h-auto rounded-none border-0 border-b border-border bg-transparent px-0 pb-2 text-xl placeholder:text-muted-foreground focus-visible:border-primary focus-visible:ring-0 md:text-xl"
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
                inputClassName="h-auto border-0 bg-transparent px-0 pr-7 text-xl font-semibold placeholder:text-muted-foreground md:text-xl"
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
            <p id={sentenceId} className="text-base leading-relaxed">
              {fillSentence(t(`positionDialog.sentence.${sentenceKey}`), words)}
            </p>
            {panels}
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
              {fillSentence(t(`positionDialog.extrasSentence.${extrasSentenceKey}`), extrasWords)}
            </p>
            {extrasPanels}
          </div>
        </div>
      )}
    </DialogFrame>
  )
}
