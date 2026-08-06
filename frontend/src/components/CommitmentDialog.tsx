import { useEffect, useState } from 'react'

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
import {
  BLOCK_LABEL,
  BUDGETS,
  BLOCK_SUGGESTION,
  CATEGORY_LABEL,
  DUE_DAY_MAY_SHIFT,
  MONTH_LABEL,
  PAYMENT_LABEL,
  RHYTHM_LABEL,
  dueMonths,
  effectiveDueDay,
  firstMonthOf,
  type Block,
  type Category,
  type Commitment,
  type CommitmentType,
  type PaymentMethod,
  type Rhythm,
} from '@/lib/domain'
import { useAccounts, useHouseholds } from '@/lib/queries'

/**
 * One form for every commitment — savings plans and loans are commitments too.
 *
 * The type comes first, in everyday words, and drives the extra fields:
 *   contract      → none
 *   savings_goal  → target amount, target date
 *   debt          → remaining debt
 *   budget        → none, but the block is freely selectable
 *
 * The CHECK constraints in the database enforce exactly this mapping.
 *
 * For savings_goal and debt, `resolve_block()` in the backend overrides the block
 * choice and always sets savings. That is why there is no picker there but the
 * reason instead — a greyed-out field would have suggested it might still work. The
 * field is still called `block` in code and Budget in the UI.
 */

const TYPE_OPTIONS: {
  value: CommitmentType
  label: string
  hint: string
  /** Why the block is fixed — shown in place of the picker. */
  budgetHint: string | null
  namePlaceholder: string
  defaultCategory: Category
}[] = [
  {
    value: 'contract',
    label: 'Läuft weiter',
    hint: 'Miete, Handy, Versicherung, Streaming — ohne festes Ende.',
    budgetHint: null,
    namePlaceholder: 'Miete',
    defaultCategory: 'housing',
  },
  {
    value: 'savings_goal',
    label: 'Hat ein Ziel',
    hint: 'Auto, Urlaub, Zähne — es gibt einen Zielbetrag.',
    budgetHint: 'Alles, was du zurücklegst, zählt hierher.',
    namePlaceholder: 'Auto',
    defaultCategory: 'reserves',
  },
  {
    value: 'debt',
    label: 'Wird abbezahlt',
    hint: 'Kredit oder Rückstand — läuft auf null.',
    // The reason, in one sentence.
    budgetHint:
      'Tilgen ist kein Verbrauch — das Geld ist nicht weg, deine Schuld wird kleiner. Unterm Strich derselbe Vorgang wie Sparen.',
    namePlaceholder: 'Rundfunk-Altrückstand',
    defaultCategory: 'debt_repayment',
  },
  {
    value: 'budget',
    label: 'Setze ich selbst',
    hint: 'Sprit, Lebensmittel, Taschengeld — kein Vertrag, du legst den Betrag fest.',
    budgetHint: null,
    namePlaceholder: 'Lebensmittel',
    defaultCategory: 'groceries',
  },
]

const CATEGORIES = Object.keys(CATEGORY_LABEL) as Category[]
const PAYMENTS = Object.keys(PAYMENT_LABEL) as PaymentMethod[]
const BLOCKS: Block[] = ['income', ...BUDGETS]
const RHYTHMS = Object.keys(RHYTHM_LABEL) as Rhythm[]

function emptyDraft(): Commitment {
  return {
    id: '',
    type: 'contract',
    name: '',
    amount: '',
    category: 'housing',
    block: 'needs',
    householdId: null,
    rhythm: 'monthly',
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
  const households = useHouseholds().data ?? []
  const accounts = useAccounts().data ?? []
  const [draft, setDraft] = useState<Commitment>(commitment ?? emptyDraft())

  // Reset on open — otherwise the previous state is still in the fields.
  useEffect(() => {
    if (open) setDraft(commitment ?? emptyDraft())
  }, [open, commitment])

  const isEdit = commitment !== null
  const typeOption = TYPE_OPTIONS.find((option) => option.value === draft.type)!
  const isRecurringIrregular = draft.rhythm !== 'monthly'
  // Only savings goals and debts are fixed — resolve_block() in the backend
  // overrides them anyway. A budget chooses freely: whether fuel is a need or a
  // want depends on the household.
  const blockIsFixed = draft.type === 'savings_goal' || draft.type === 'debt'

  function set<K extends keyof Commitment>(key: K, value: Commitment[K]) {
    setDraft((current) => ({ ...current, [key]: value }))
  }

  /**
   * Switching the type clears the extra fields that do not belong to the new one —
   * otherwise the form sends values the database rejects.
   */
  function handleType(type: CommitmentType) {
    const option = TYPE_OPTIONS.find((item) => item.value === type)!
    setDraft((current) => ({
      ...current,
      type,
      // Only follow along with the category if it still holds the old suggestion.
      category:
        current.category === typeOption.defaultCategory
          ? option.defaultCategory
          : current.category,
      block:
        type === 'savings_goal' || type === 'debt'
          ? 'savings'
          : BLOCK_SUGGESTION[current.category],
      targetAmount: type === 'savings_goal' ? current.targetAmount : null,
      targetDate: type === 'savings_goal' ? current.targetDate : null,
      remainingDebt: type === 'debt' ? current.remainingDebt : null,
    }))
  }

  /** Changing the category preselects the block — not for goals and debts. */
  function handleCategory(category: Category) {
    setDraft((current) => ({
      ...current,
      category,
      block: blockIsFixed ? 'savings' : BLOCK_SUGGESTION[category],
    }))
  }

  function handleRhythm(rhythm: Rhythm) {
    setDraft((current) => {
      if (rhythm === 'monthly') {
        // A first due date exists for a non-monthly rhythm only.
        return { ...current, rhythm, firstDueDate: null }
      }
      // When switching to quarterly and friends, suggest today — better than an
      // empty mandatory field.
      const seed = current.firstDueDate ?? today()
      return {
        ...current,
        rhythm,
        firstDueDate: seed,
        dueDay: Number(seed.slice(8, 10)),
      }
    })
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
    onSave(draft)
    onOpenChange(false)
  }

  const months = dueMonths(draft.rhythm, firstMonthOf(draft))

  // From the 29th on the day can shift — February is the hard case.
  const dueDayShifts = draft.dueDay >= DUE_DAY_MAY_SHIFT
  const shiftYear = draft.firstDueDate
    ? Number(draft.firstDueDate.slice(0, 4))
    : new Date().getFullYear()
  const februaryDay = effectiveDueDay(draft.dueDay, shiftYear, 2)

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      {/* Bei „Wird abbezahlt" und auf niedrigen Bildschirmen wird das Formular
          höher als das Fenster — ohne max-h wären Titel und Knöpfe abgeschnitten. */}
      <DialogContent className="max-h-[90svh] overflow-y-auto sm:max-w-lg">
        <form onSubmit={handleSubmit} className="flex flex-col gap-5">
          <DialogHeader>
            <DialogTitle className="font-heading text-xl">
              {isEdit ? 'Vertrag bearbeiten' : 'Vertrag anlegen'}
            </DialogTitle>
            <DialogDescription>
              Einmal anlegen — die Posten für jeden Monat entstehen daraus von
              selbst.
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
                  {option.label}
                </Button>
              ))}
            </div>
            <p className="text-muted-foreground text-xs">{typeOption.hint}</p>
          </div>

          <div className="flex flex-col gap-4">
            <div className="flex flex-col gap-2">
              <Label htmlFor="name">Bezeichnung</Label>
              <Input
                id="name"
                value={draft.name}
                onChange={(event) => set('name', event.target.value)}
                placeholder={typeOption.namePlaceholder}
                required
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div className="flex flex-col gap-2">
                <Label htmlFor="amount">
                  {draft.type === 'debt' ? 'Rate' : 'Betrag'}
                </Label>
                <Input
                  id="amount"
                  type="number"
                  step="0.01"
                  min="0"
                  inputMode="decimal"
                  value={draft.amount}
                  onChange={(event) => set('amount', event.target.value)}
                  placeholder="0,00"
                  required
                />
              </div>

              <div className="flex flex-col gap-2">
                <Label>Rhythmus</Label>
                <Select value={draft.rhythm} onValueChange={handleRhythm}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {RHYTHMS.map((rhythm) => (
                      <SelectItem key={rhythm} value={rhythm}>
                        {RHYTHM_LABEL[rhythm]}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            {/* Beide gehören an den Vertrag, nicht an den Monat — sie werden
                beim Erzeugen in jeden Posten kopiert und bleiben dort
                überschreibbar. */}
            <div className="grid grid-cols-2 gap-3">
              <div className="flex flex-col gap-2">
                <Label>Konto</Label>
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
                    <SelectItem value="default">Standardkonto</SelectItem>
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
                <Label>Zielkonto</Label>
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
                    <SelectItem value="none">Geht raus</SelectItem>
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
                  Nur beim Sparen auf ein eigenes Konto. Dann bucht der Haken
                  eine Umbuchung, und der Gesamtstand bleibt richtig.
                </span>
              </div>


            <div className="flex flex-col gap-2">
              <Label>Zahlungsart</Label>
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
                  <SelectItem value="none">Keine Angabe</SelectItem>
                  {PAYMENTS.map((method) => (
                    <SelectItem key={method} value={method}>
                      {PAYMENT_LABEL[method]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              </div>
            </div>

            {isRecurringIrregular ? (
              <div className="flex flex-col gap-2">
                <Label htmlFor="first-due">Erste Fälligkeit</Label>
                <DateField
                  id="first-due"
                  value={draft.firstDueDate ?? ''}
                  onChange={handleFirstDueDate}
                />
                <p className="text-muted-foreground text-xs">
                  Tag und Monat kommen von hier — das Jahr entscheidet, ab wann
                  Posten entstehen.
                </p>
              </div>
            ) : (
              <div className="flex flex-col gap-2">
                <Label htmlFor="due-day">Fällig am</Label>
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
                    Fällt an am {draft.dueDay}. in{' '}
                    {months.map((month) => MONTH_LABEL[month - 1]).join(', ')}
                    {draft.firstDueDate
                      ? ` — erstmals ${MONTH_LABEL[Number(draft.firstDueDate.slice(5, 7)) - 1]} ${draft.firstDueDate.slice(0, 4)}.`
                      : '.'}
                  </span>
                )}
                {dueDayShifts && (
                  <span>
                    Den {draft.dueDay}. gibt es nicht in jedem Monat — dann
                    rutscht es auf den letzten Tag. Im Februar {shiftYear} also
                    auf den {februaryDay}.
                  </span>
                )}
              </p>
            )}

            <div className="grid grid-cols-2 gap-3">
              <div className="flex flex-col gap-2">
                <Label>Kategorie</Label>
                <Select value={draft.category} onValueChange={handleCategory}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {CATEGORIES.map((category) => (
                      <SelectItem key={category} value={category}>
                        {CATEGORY_LABEL[category]}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="flex flex-col gap-2">
                <Label>Budget</Label>
                {blockIsFixed ? (
                  <div className="flex flex-col gap-1">
                    <span className="flex h-9 items-center gap-2 text-sm font-medium">
                      <span className="bg-chart-4 size-2.5 rounded-sm" />
                      Sparen
                    </span>
                  </div>
                ) : (
                  <Select
                    value={draft.block}
                    onValueChange={(value) => set('block', value as Block)}
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {BLOCKS.map((block) => (
                        <SelectItem key={block} value={block}>
                          {BLOCK_LABEL[block]}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              </div>
            </div>

            {typeOption.budgetHint && (
              <p className="text-muted-foreground bg-muted rounded-md px-3 py-2 text-xs">
                {typeOption.budgetHint}
              </p>
            )}

            <div className="flex flex-col gap-2">
              <Label>Zuordnung</Label>
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
                  <SelectItem value="private">Nur mein Plan</SelectItem>
                  {households.map((household) => (
                    <SelectItem key={household.id} value={household.id}>
                      {household.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-muted-foreground text-xs">
                In welchem Plan die Posten landen. Einmal entschieden, gilt für
                alle künftigen Monate.
              </p>
            </div>

            {draft.type === 'savings_goal' && (
              <div className="grid grid-cols-2 gap-3">
                <div className="flex flex-col gap-2">
                  <Label htmlFor="target-amount">Zielbetrag</Label>
                  <Input
                    id="target-amount"
                    type="number"
                    step="0.01"
                    min="0"
                    value={draft.targetAmount ?? ''}
                    onChange={(event) =>
                      set('targetAmount', event.target.value || null)
                    }
                    placeholder="0,00"
                  />
                </div>
                <div className="flex flex-col gap-2">
                  <Label htmlFor="target-date">Zieldatum</Label>
                  <DateField
                    id="target-date"
                    value={draft.targetDate ?? ''}
                    onChange={(iso) => set('targetDate', iso || null)}
                    placeholder="Kein Zieldatum"
                  />
                </div>
              </div>
            )}

            {draft.type === 'debt' && (
              <div className="flex flex-col gap-2">
                <Label htmlFor="remaining-debt">Restschuld</Label>
                <Input
                  id="remaining-debt"
                  type="number"
                  step="0.01"
                  min="0"
                  value={draft.remainingDebt ?? ''}
                  onChange={(event) =>
                    set('remainingDebt', event.target.value || null)
                  }
                  placeholder="0,00"
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
                <Label htmlFor="commitment-pass-through">Durchlaufend</Label>
                <span className="text-muted-foreground text-xs">
                  Geld, das nur weitergereicht wird — BuT, eine Rückzahlung,
                  die sofort weggelegt wird. Zählt in kein Budget und in keine
                  Quote.
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
                <Label htmlFor="active">Aktiv</Label>
                <span className="text-muted-foreground text-xs">
                  Inaktive erzeugen keine neuen Posten.
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
              Abbrechen
            </Button>
            <Button type="submit">{isEdit ? 'Speichern' : 'Anlegen'}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
