import { useEffect, useState } from 'react'
import { Plus, Star, Trash2 } from 'lucide-react'

import {
  Empty,
  EmptyDescription,
  EmptyHeader,
} from '@/components/ui/empty'
import { DateField } from '@/components/DateField'
import { today } from '@/lib/dates'
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
  atLeast,
  ACCOUNT_TYPE_LABEL,
  euro,
  type Account,
  type AccountType,
} from '@/lib/domain'
import { useActiveMember } from '@/hooks/use-active-member'
import { OWN_SCOPE } from '@/lib/domain'
import { useAccounts, useDeleteAccount, useSaveAccount } from '@/lib/queries'

/**
 * Payment accounts — current, savings, card, wallet, cash.
 *
 * Accounts are private, even inside a shared household. For joint planning what
 * matters is what the positions say, not where the money sits.
 *
 * **No securities accounts.** Their value comes from market prices, not from
 * bookings — a balance from opening amount plus bookings would be permanently
 * wrong. Only the settlement account appears in the book; buying securities is a
 * transfer to it.
 */

const TYPES = Object.keys(ACCOUNT_TYPE_LABEL) as AccountType[]

function emptyAccount(isFirst: boolean): Account {
  return {
    id: '',
    name: '',
    type: 'checking',
    openingBalance: '',
    openingDate: today(),
    // The first account becomes the default automatically — otherwise the first
    // booking would send you back into the settings.
    isDefault: isFirst,
    active: true,
    externalRef: null,
    countsAsAvailable: true,
  }
}

export function AccountsPage() {
  // `?member=` shows somebody else's accounts — see `MemberSwitcher`. Their level
  // decides whether the page offers buttons; the endpoint checks it again anyway.
  const active = useActiveMember()
  const accounts = useAccounts(
    active.id === null ? OWN_SCOPE : { kind: 'member', ownerId: active.id }
  )
  const mayEdit = atLeast(active.levelFor('accounts'), 'edit')
  const mayDelete = atLeast(active.levelFor('accounts'), 'delete')
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
            {active.member === null
              ? 'Wo dein Geld liegt. Konten sind privat — für die gemeinsame Planung zählt, was auf den Posten steht.'
              : `Die Konten von ${active.member.firstName}. ${mayEdit ? 'Du darfst sie ändern.' : 'Nur zum Ansehen.'}`}
          </p>
        </div>
        {mayEdit && (
          <Button onClick={add}>
            <Plus className="size-4" />
            Konto anlegen
          </Button>
        )}
      </header>

      <QueryState isPending={accounts.isPending} error={accounts.error}>
        {list.length === 0 ? (
          <Empty className="border-border rounded-xl border border-dashed">
          <EmptyHeader>
            <EmptyDescription>Noch kein Konto. Leg eins an — ohne Konto lässt sich später nichts
            ins Haushaltsbuch buchen.</EmptyDescription>
          </EmptyHeader>
        </Empty>
        ) : (
          <ul className="flex flex-col gap-3">
            {list.map((account) => (
              <li key={account.id}>
                <button
                  type="button"
                  disabled={!mayEdit}
                  onClick={() => {
                    setEditing(account)
                    setOpen(true)
                  }}
                  className={`bg-card ring-foreground/10 hover:ring-ring flex w-full flex-wrap items-center justify-between gap-4 rounded-xl p-4 text-left ring-1 transition-[box-shadow] ${
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

      <AccountDialog
        account={editing}
        mayDelete={mayDelete}
        open={open}
        onOpenChange={setOpen}
      />
    </div>
  )
}

function AccountDialog({
  account,
  mayDelete,
  open,
  onOpenChange,
}: {
  account: Account | null
  /** Only decides whether the button is offered. The endpoint checks it again. */
  mayDelete: boolean
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
                <DateField
                  id="account-date"
                  value={draft.openingDate}
                  onChange={(iso) => set('openingDate', iso)}
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
            {isEdit && mayDelete ? (
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
