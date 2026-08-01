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
import { useHouseholds } from '@/lib/queries'

/**
 * Einmal-Posten anlegen und bearbeiten.
 *
 * Bewusst kurz gehalten: Bezeichnung, Betrag, Budget — mehr braucht es nicht,
 * um „ich will ein Rollo kaufen" in den Monat zu schreiben. Der Rest ist
 * vorbelegt und nur bei Bedarf anzufassen.
 *
 * Wiederkehrendes gehört **nicht** hierher, sondern auf die Verträge-Seite.
 * Ein Vertrag erzeugt seine Posten selbst, jeden Monat neu.
 */

const CATEGORIES = Object.keys(CATEGORY_LABEL) as Category[]
const PAYMENTS = Object.keys(PAYMENT_LABEL) as PaymentMethod[]

/** Zur passenden Kategorie, damit das Budget nicht sofort umspringt. */
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
    paymentMethod: null,
    householdId: null,
    commitmentId: null,
    paidAt: null,
  }
}

type Props = {
  /** null = anlegen, sonst bearbeiten. */
  position: PlanPosition | null
  /** Budget, in dem der Posten landen soll — vorbelegt beim Anlegen. */
  block: Block
  /** Zu welchem Plan der neue Posten gehört. */
  planId: string
  open: boolean
  onOpenChange: (open: boolean) => void
  onSave: (position: PlanPosition) => void
  onDelete: (position: PlanPosition) => void
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
  const [draft, setDraft] = useState<PlanPosition>(
    position ?? emptyDraft(block)
  )

  useEffect(() => {
    if (open) setDraft(position ?? emptyDraft(block))
  }, [open, position, block])

  const isEdit = position !== null
  // Aus einem Vertrag erzeugte Posten haben eine Quelle — Bezeichnung und
  // Zuordnung gehören dann zum Vertrag, nicht zum einzelnen Monat.
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
    // Änderungen an fremden Posten protokolliert das Backend selbst.
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
            {isEdit ? (
              // TODO: Bestätigung vor dem Löschen, wie bei den Verträgen.
              <Button
                type="button"
                variant="ghost"
                className="text-destructive hover:text-destructive"
                onClick={() => {
                  onDelete(draft)
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
