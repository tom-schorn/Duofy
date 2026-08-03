import { useEffect, useState } from 'react'
import { Plus, Star, Trash2 } from 'lucide-react'

import { QueryState } from '@/components/QueryState'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Switch } from '@/components/ui/switch'
import { errorText } from '@/lib/api'
import {
  ACCOUNT_TYPE_LABEL,
  euro,
  type Account,
  type AccountType,
} from '@/lib/domain'
import { useAccounts, useDeleteAccount, useSaveAccount } from '@/lib/queries'

/**
 * Zahlungskonten — Giro, Tagesgeld, Kreditkarte, PayPal, Bargeld.
 *
 * Konten sind privat, auch im gemeinsamen Haushalt. Für die gemeinsame
 * Planung zählt, was auf den Posten steht, nicht wo das Geld liegt.
 *
 * **Kein Depot.** Sein Wert kommt von Kursen, nicht von Buchungen — ein Saldo
 * aus Anfangsbestand plus Buchungen wäre dort dauerhaft falsch. Im Buch steht
 * nur das Verrechnungskonto, Wertpapierkäufe sind Umbuchungen dorthin.
 *
 * Der Stand steht hier noch nicht: er ergibt sich aus dem Anfangsbestand plus
 * den Buchungen, und die kommen mit dem Haushaltsbuch.
 */

const TYPES = Object.keys(ACCOUNT_TYPE_LABEL) as AccountType[]

function emptyAccount(isFirst: boolean): Account {
  return {
    id: '',
    name: '',
    type: 'checking',
    openingBalance: '',
    openingDate: new Date().toISOString().slice(0, 10),
    // Das erste Konto ist automatisch der Standard — sonst müsste man beim
    // ersten Buchen erst zurück in die Einstellungen.
    isDefault: isFirst,
    active: true,
    externalRef: null,
    countsAsAvailable: true,
  }
}

export function AccountsPage() {
  const accounts = useAccounts()
  const [editing, setEditing] = useState<Account | null>(null)
  const [open, setOpen] = useState(false)

  const list = accounts.data ?? []

  function add() {
    setEditing(emptyAccount(list.length === 0))
    setOpen(true)
  }

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-col gap-2">
          <h1 className="font-heading text-3xl font-semibold">Konten</h1>
          <p className="text-muted-foreground max-w-2xl">
            Wo dein Geld liegt. Konten sind privat — für die gemeinsame Planung
            zählt, was auf den Posten steht.
          </p>
        </div>
        <Button onClick={add}>
          <Plus className="size-4" />
          Konto anlegen
        </Button>
      </header>

      <QueryState isPending={accounts.isPending} error={accounts.error}>
        {list.length === 0 ? (
          <p className="text-muted-foreground bg-card border-border rounded-lg border p-6 text-sm">
            Noch kein Konto. Leg eins an — ohne Konto lässt sich später nichts
            ins Haushaltsbuch buchen.
          </p>
        ) : (
          <ul className="flex flex-col gap-3">
            {list.map((account) => (
              <li key={account.id}>
                <button
                  type="button"
                  onClick={() => {
                    setEditing(account)
                    setOpen(true)
                  }}
                  className={`bg-card border-border hover:border-ring flex w-full flex-wrap items-center justify-between gap-4 rounded-lg border p-4 text-left transition-colors ${
                    account.active ? '' : 'opacity-60'
                  }`}
                >
                  <span className="flex min-w-0 flex-col gap-1">
                    <span className="flex flex-wrap items-center gap-2">
                      <span className="font-medium">{account.name}</span>
                      {account.isDefault && (
                        <Badge variant="secondary" className="gap-1 font-normal">
                          <Star className="size-3" />
                          Standard
                        </Badge>
                      )}
                      {!account.active && (
                        <Badge variant="outline" className="font-normal">
                          aufgelöst
                        </Badge>
                      )}
                    </span>
                    <span className="text-muted-foreground text-xs">
                      {ACCOUNT_TYPE_LABEL[account.type]} · Anfangsbestand vom{' '}
                      {new Date(account.openingDate).toLocaleDateString('de-DE')}
                    </span>
                  </span>
                  <span className="font-mono font-medium tabular-nums">
                    {euro.format(Number(account.openingBalance))}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </QueryState>

      <AccountDialog account={editing} open={open} onOpenChange={setOpen} />
    </div>
  )
}

function AccountDialog({
  account,
  open,
  onOpenChange,
}: {
  account: Account | null
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const save = useSaveAccount()
  const remove = useDeleteAccount()
  const [draft, setDraft] = useState<Account>(account ?? emptyAccount(false))

  useEffect(() => {
    if (open && account) setDraft(account)
  }, [open, account])

  const isEdit = Boolean(draft.id)

  function set<K extends keyof Account>(key: K, value: Account[K]) {
    setDraft((current) => ({ ...current, [key]: value }))
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <form
          onSubmit={(event) => {
            event.preventDefault()
            save.mutate(
              { ...draft, id: draft.id || undefined },
              { onSuccess: () => onOpenChange(false) }
            )
          }}
        >
          <DialogHeader>
            <DialogTitle>
              {isEdit ? 'Konto bearbeiten' : 'Konto anlegen'}
            </DialogTitle>
            <DialogDescription>
              Der Stand ergibt sich später aus Anfangsbestand plus Buchungen.
              Ein Depot gehört nicht hierher — nur sein Verrechnungskonto.
            </DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-4 py-4">
            <div className="flex flex-col gap-2">
              <Label htmlFor="account-name">Bezeichnung</Label>
              <Input
                id="account-name"
                value={draft.name}
                onChange={(event) => set('name', event.target.value)}
                placeholder="Girokonto"
                required
              />
            </div>

            <div className="flex flex-col gap-2">
              <Label>Art</Label>
              <Select
                value={draft.type}
                onValueChange={(value) => set('type', value as AccountType)}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {TYPES.map((type) => (
                    <SelectItem key={type} value={type}>
                      {ACCOUNT_TYPE_LABEL[type]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div className="flex flex-col gap-2">
                <Label htmlFor="account-balance">Anfangsbestand</Label>
                <Input
                  id="account-balance"
                  type="number"
                  step="0.01"
                  inputMode="decimal"
                  value={draft.openingBalance}
                  onChange={(event) =>
                    set('openingBalance', event.target.value)
                  }
                  placeholder="0,00"
                  required
                />
              </div>
              <div className="flex flex-col gap-2">
                {/* Ohne Stichtag wäre der Stand zu einem Zeitpunkt nicht
                    berechenbar — man wüsste nicht, welche Buchungen schon
                    im Anfangsbestand stecken. */}
                <Label htmlFor="account-date">Stand vom</Label>
                <Input
                  id="account-date"
                  type="date"
                  value={draft.openingDate}
                  onChange={(event) => set('openingDate', event.target.value)}
                  required
                />
              </div>
            </div>

            <div className="border-border flex items-center justify-between gap-4 rounded-lg border p-3">
              <span className="flex flex-col">
                <span className="text-sm font-medium">Standardkonto</span>
                <span className="text-muted-foreground text-xs">
                  Wird beim schnellen Buchen vorausgewählt. Ein zweites löst
                  das bisherige ab.
                </span>
              </span>
              <Switch
                checked={draft.isDefault}
                onCheckedChange={(value) => set('isDefault', value)}
              />
            </div>

            {/* Der Schalter, der das Buch beeinflusst: liegt Zweckgebundenes
                auf dem Konto, ist eine Umbuchung dorthin eine Ausgabe. */}
            <div className="border-border flex items-center justify-between gap-4 rounded-lg border p-3">
              <span className="flex flex-col">
                <span className="text-sm font-medium">Zählt als verfügbar</span>
                <span className="text-muted-foreground text-xs">
                  Aus beim Tagesgeld oder Depot: was dorthin wandert, gilt im
                  Buch als ausgegeben. An bei Giro und PayPal — dort bleibt das
                  Geld greifbar.
                </span>
              </span>
              <Switch
                checked={draft.countsAsAvailable}
                onCheckedChange={(value) => set('countsAsAvailable', value)}
              />
            </div>

            <div className="border-border flex items-center justify-between gap-4 rounded-lg border p-3">
              <span className="flex flex-col">
                <span className="text-sm font-medium">Aktiv</span>
                <span className="text-muted-foreground text-xs">
                  Aufgelöste Konten bleiben stehen, damit alte Buchungen ihren
                  Bezug behalten.
                </span>
              </span>
              <Switch
                checked={draft.active}
                onCheckedChange={(value) => set('active', value)}
              />
            </div>

            {(save.isError || remove.isError) && (
              <p role="alert" className="text-destructive text-sm">
                {errorText(save.error ?? remove.error)}
              </p>
            )}
          </div>

          <DialogFooter className="gap-2 sm:justify-between">
            {isEdit ? (
              <Button
                type="button"
                variant="ghost"
                className="text-destructive"
                onClick={() =>
                  remove.mutate(draft.id, {
                    onSuccess: () => onOpenChange(false),
                  })
                }
              >
                <Trash2 className="size-4" />
                Löschen
              </Button>
            ) : (
              <span />
            )}
            <Button type="submit" disabled={save.isPending}>
              {save.isPending ? 'Wird gesichert…' : 'Sichern'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
