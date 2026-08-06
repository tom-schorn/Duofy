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
  BLOCK_SUGGESTION,
  BUDGET_ORDER,
  CATEGORY_LABEL,
  PAYMENT_LABEL,
  type Block,
  type Category,
  type PaymentMethod,
  type PlanPosition,
} from '@/lib/domain'
import { useAccounts, useHouseholds } from '@/lib/queries'

/**
 * Create and edit one-off positions.
 *
 * Deliberately short: label, amount, block — that is all it takes to put a planned
 * purchase into the month. The rest is prefilled and only touched when needed.
 *
 * Anything recurring does **not** belong here but on the commitments page. A
 * commitment generates its positions itself, every month anew.
 */

const CATEGORIES = Object.keys(CATEGORY_LABEL) as Category[]
const PAYMENTS = Object.keys(PAYMENT_LABEL) as PaymentMethod[]

/** The matching category, so the block does not jump the moment it is picked. */
const DEFAULT_CATEGORY: Record<Block, Category> = {
  income: 'income',
  needs: 'housing',
  wants: 'leisure',
  savings: 'reserves',
}

function emptyDraft(block: Block): PlanPosition {
  return {
    id: '',
    label: '',
    amountPlanned: '',
    amountActual: null,
    category: DEFAULT_CATEGORY[block],
    block,
    dueDay: 1,
    accountId: null,
    isBudget: false,
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
  /** The block the position should land in — prefilled when creating. */
  block: Block
  /** Which plan the new position belongs to. */
  planId: string
  open: boolean
  onOpenChange: (open: boolean) => void
  onSave: (position: PlanPosition) => void
  /**
   * null means do not offer deletion. Needed when acting on somebody else plan:
   * changing is recorded and reversible, deleting is neither.
   */
  onDelete: ((position: PlanPosition) => void) | null
}

export function PositionDialog({
  position,
  block,
  planId: _planId,
  open,
  onOpenChange,
  onSave,
  onDelete,
}: Props) {
  const households = useHouseholds().data ?? []
  const accounts = useAccounts().data ?? []
  const [draft, setDraft] = useState<PlanPosition>(
    position ?? emptyDraft(block)
  )

  useEffect(() => {
    if (open) setDraft(position ?? emptyDraft(block))
  }, [open, position, block])

  const isEdit = position !== null
  // Positions generated from a commitment have a source — label and assignment
  // then belong to the commitment, not to the single month.
  const fromCommitment = draft.commitmentId !== null

  function set<K extends keyof PlanPosition>(
    key: K,
    value: PlanPosition[K]
  ) {
    setDraft((current) => ({ ...current, [key]: value }))
  }

  function handleCategory(category: Category) {
    setDraft((current) => ({
      ...current,
      category,
      block: BLOCK_SUGGESTION[category],
    }))
  }

  function handleSubmit(event: React.FormEvent) {
    event.preventDefault()
    // The backend records changes to other people positions itself.
    onSave(draft)
    onOpenChange(false)
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90svh] overflow-y-auto sm:max-w-md">
        <form onSubmit={handleSubmit} className="flex flex-col gap-5">
          <DialogHeader>
            <DialogTitle className="font-heading text-xl">
              {isEdit ? 'Posten bearbeiten' : 'Posten hinzufügen'}
            </DialogTitle>
            <DialogDescription>
              {fromCommitment
                ? 'Kommt aus einem Vertrag. Änderungen hier gelten nur für diesen Monat.'
                : 'Gilt nur für diesen Monat. Wiederkehrendes gehört zu den Verträgen.'}
            </DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-4">
            <div className="flex flex-col gap-2">
              <Label htmlFor="label">Bezeichnung</Label>
              <Input
                id="label"
                value={draft.label}
                onChange={(event) => set('label', event.target.value)}
                placeholder="Rollo fürs Wohnzimmer"
                required
                autoFocus
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div className="flex flex-col gap-2">
                <Label htmlFor="planned">Betrag</Label>
                <Input
                  id="planned"
                  type="number"
                  step="0.01"
                  min="0"
                  inputMode="decimal"
                  value={draft.amountPlanned}
                  onChange={(event) =>
                    set('amountPlanned', event.target.value)
                  }
                  placeholder="0,00"
                  required
                />
              </div>

              <div className="flex flex-col gap-2">
                <Label htmlFor="pos-due-day">Fällig am</Label>
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
                <Select
                  value={draft.block}
                  onValueChange={(value) => set('block', value as Block)}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {/* BUDGET_ORDER statt BUDGETS: sonst fehlt „Einnahmen"
                        und ein Einnahme-Posten wäre nicht bearbeitbar. */}
                    {BUDGET_ORDER.map((budget) => (
                      <SelectItem key={budget} value={budget}>
                        {BLOCK_LABEL[budget]}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

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
                    {/* „Standardkonto" statt einer Vorauswahl: so bleibt der
                        Posten richtig, wenn du das Standardkonto wechselst. */}
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
                    <SelectItem value="none">offen</SelectItem>
                    {PAYMENTS.map((method) => (
                      <SelectItem key={method} value={method}>
                        {PAYMENT_LABEL[method]}
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
                  <Label htmlFor="position-pass-through">Durchlaufend</Label>
                  <span className="text-muted-foreground text-xs">
                    Geld, das nur weitergereicht wird — BuT, eine Rückzahlung,
                    die sofort weggelegt wird. Zählt in kein Budget und in keine
                    Quote.
                  </span>
                </div>
                <Switch
                  id="position-pass-through"
                  checked={draft.passThrough}
                  onCheckedChange={(checked) => set('passThrough', checked)}
                />
              </div>

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
              </div>
            </div>

            {isEdit && (
              <div className="flex flex-col gap-2">
                <Label htmlFor="actual">Tatsächlich bezahlt</Label>
                <Input
                  id="actual"
                  type="number"
                  step="0.01"
                  min="0"
                  inputMode="decimal"
                  value={draft.amountActual ?? ''}
                  onChange={(event) =>
                    set('amountActual', event.target.value || null)
                  }
                  placeholder="noch offen"
                />
                <p className="text-muted-foreground text-xs">
                  Nur nötig, wenn der Betrag vom geplanten abweicht. Abgehakt
                  wird in der Liste.
                </p>
              </div>
            )}
          </div>

          <DialogFooter className="sm:justify-between">
            {isEdit && onDelete !== null ? (
              // TODO: confirm before deleting, the way the commitments page does.
              <Button
                type="button"
                variant="ghost"
                className="text-destructive hover:text-destructive"
                onClick={() => {
                  onDelete!(draft)
                  onOpenChange(false)
                }}
              >
                Löschen
              </Button>
            ) : (
              <span />
            )}
            <div className="flex gap-2">
              <Button
                type="button"
                variant="outline"
                onClick={() => onOpenChange(false)}
              >
                Abbrechen
              </Button>
              <Button type="submit">
                {isEdit ? 'Speichern' : 'Hinzufügen'}
              </Button>
            </div>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
