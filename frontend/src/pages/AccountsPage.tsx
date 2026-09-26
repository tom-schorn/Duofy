import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Plus, Star, Trash2 } from 'lucide-react'

import {
  Empty,
  EmptyDescription,
  EmptyHeader,
} from '@/components/ui/empty'
import { DateField } from '@/components/DateField'
import { ListRow } from '@/components/ListRow'
import { today, shortDate } from '@/lib/dates'
import { QueryState } from '@/components/QueryState'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { DialogFrame } from '@/components/DialogFrame'
import { AmountField } from '@/components/AmountField'
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
import {
  atLeast,
  accountTypeLabel,
  euro,
  type Account,
  type AccountType,
  ACCOUNT_TYPES,
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

const TYPES = ACCOUNT_TYPES

function emptyAccount(isFirst: boolean): Account {
  return {
    id: '',
    deletable: false,
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
  const { t } = useTranslation()
  // `?member=` shows somebody else's accounts — see `MemberSwitcher`. Their level
  // decides whether the page offers buttons; the endpoint checks it again anyway.
  const active = useActiveMember()
  const accounts = useAccounts(
    active.id === null ? OWN_SCOPE : { kind: 'member', ownerId: active.id }
  )
  const mayEdit = atLeast(active.levelFor('accounts'), 'edit')
  // Your own you may always delete — as long as it is unused; another's needs `delete`.
  const mayDelete = active.member === null || atLeast(active.levelFor('accounts'), 'delete')
  const [editing, setEditing] = useState<Account | null>(null)
  const [open, setOpen] = useState(false)
  // After a delete the row is gone; the focus goes to the page heading (rule 13).
  const heading = useRef<HTMLHeadingElement>(null)
  const deleted = useRef(false)

  const list = accounts.data ?? []

  function add() {
    deleted.current = false
    setEditing(emptyAccount(list.length === 0))
    setOpen(true)
  }

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-col gap-2">
          <h1
            ref={heading}
            tabIndex={-1}
            className="font-heading text-3xl font-semibold outline-none"
          >
            {t('accounts.title')}
          </h1>
          <p className="text-muted-foreground max-w-2xl">
            {active.member === null
              ? t('accounts.lead')
              : mayEdit
                ? t('accounts.leadMemberEdit', { name: active.member.firstName })
                : t('accounts.leadMemberView', { name: active.member.firstName })}
          </p>
        </div>
        {mayEdit && (
          <Button onClick={add}>
            <Plus className="size-4" />
            {t('accounts.create')}
          </Button>
        )}
      </header>

      <QueryState isPending={accounts.isPending} error={accounts.error}>
        {list.length === 0 ? (
          <Empty className="border-border rounded-xl border border-dashed">
          <EmptyHeader>
            <EmptyDescription>{t('accounts.empty')}</EmptyDescription>
          </EmptyHeader>
        </Empty>
        ) : (
          <ul className="flex flex-col">
            {list.map((account) => (
              <AccountRow
                key={account.id}
                account={account}
                mayEdit={mayEdit}
                onOpen={() => {
                  deleted.current = false
                  setEditing(account)
                  setOpen(true)
                }}
              />
            ))}
          </ul>
        )}
      </QueryState>

      <AccountDialog
        account={editing}
        mayDelete={mayDelete}
        open={open}
        onOpenChange={setOpen}
        onDeleted={() => {
          deleted.current = true
        }}
        returnFocus={() => (deleted.current ? heading.current : null)}
      />
    </div>
  )
}

/**
 * One account: the whole row opens it (UI guideline rules 1-5). Deleting an unused
 * account and deactivating a used one both sit in the edit dialog: the „Aktiv“
 * switch is the deactivation. Without the right to edit the row is read-only.
 */
export function AccountRow({
  account,
  mayEdit,
  onOpen,
}: {
  account: Account
  mayEdit: boolean
  onOpen: () => void
}) {
  const { t } = useTranslation()
  return (
    <ListRow
      onOpen={mayEdit ? onOpen : undefined}
      className={account.active ? undefined : 'opacity-60'}
      trailing={
        <span className="font-mono font-medium">
          {euro.format(Number(account.openingBalance))}
        </span>
      }
    >
      <span className="flex flex-wrap items-center gap-2">
        <span className="font-medium">{account.name}</span>
        {account.isDefault && (
          <Badge variant="secondary" className="gap-1 font-normal">
            <Star className="size-3" />
            {t('accounts.default')}
          </Badge>
        )}
        {!account.active && (
          <Badge variant="outline" className="font-normal">
            {t('accounts.closed')}
          </Badge>
        )}
      </span>
      <span className="text-muted-foreground text-xs">
        {accountTypeLabel(account.type)} ·{' '}
        {t('accounts.openingFrom', { date: shortDate(account.openingDate) })}
      </span>
    </ListRow>
  )
}

export function AccountDialog({
  account,
  mayDelete,
  open,
  onOpenChange,
  onDeleted,
  returnFocus,
}: {
  account: Account | null
  /** Only decides whether the button is offered. The endpoint checks it again. */
  mayDelete: boolean
  open: boolean
  onOpenChange: (open: boolean) => void
  /** The account was deleted; the row is gone, so the focus needs another place. */
  onDeleted?: () => void
  /** Where the focus goes on closing instead of back to the row. */
  returnFocus?: () => HTMLElement | null
}) {
  const save = useSaveAccount()
  const { t } = useTranslation()
  const remove = useDeleteAccount()
  const [draft, setDraft] = useState<Account>(account ?? emptyAccount(false))
  const [confirming, setConfirming] = useState(false)

  // An old error must not greet the next attempt.
  const resetSave = save.reset
  const resetRemove = remove.reset
  useEffect(() => {
    if (open) {
      resetSave()
      resetRemove()
    }
  }, [open, resetSave, resetRemove])

  useEffect(() => {
    if (open && account) setDraft(account)
    if (!open) setConfirming(false)
  }, [open, account])

  const isEdit = Boolean(draft.id)

  function set<K extends keyof Account>(key: K, value: Account[K]) {
    setDraft((current) => ({ ...current, [key]: value }))
  }

  return (
    <DialogFrame
      open={open}
      onOpenChange={onOpenChange}
      title={isEdit ? t('accounts.edit') : t('accounts.create')}
      description={t('accounts.dialogDescription')}
      submitLabel={isEdit ? t('common.save') : t('common.create')}
      onSubmit={(event) => {
        event.preventDefault()
        // Only the latest action may show its error.
        remove.reset()
        save.mutate(
          { ...draft, id: draft.id || undefined },
          { onSuccess: () => onOpenChange(false) }
        )
      }}
      dirty={JSON.stringify(draft) !== JSON.stringify(account ?? emptyAccount(false))}
      pending={save.isPending}
      returnFocus={returnFocus}
      error={save.isError || remove.isError ? (save.error ?? remove.error) : null}
      start={
        isEdit && mayDelete && draft.deletable ? (
          <>
            <Button
              type="button"
              variant="ghost"
              className="text-destructive"
              onClick={() => setConfirming(true)}
            >
              <Trash2 className="size-4" />
              {t('common.delete')}
            </Button>
          <AlertDialog open={confirming} onOpenChange={setConfirming}>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle className="font-heading">
                  {t('accounts.deleteTitle', { name: draft.name })}
                </AlertDialogTitle>
                <AlertDialogDescription>{t('accounts.deleteText')}</AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>{t('common.cancel')}</AlertDialogCancel>
                <AlertDialogAction
                  onClick={() => {
                    save.reset()
                    remove.mutate(draft.id, {
                      onSuccess: () => {
                        onDeleted?.()
                        onOpenChange(false)
                      },
                    })
                  }}
                >
                  {t('common.delete')}
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
          </>
        ) : undefined
      }
    >
      <div className="flex flex-col gap-4">
        <div className="flex flex-col gap-2">
          <Label htmlFor="account-name">{t('positionDialog.label')}</Label>
          <Input
            id="account-name"
            value={draft.name}
            onChange={(event) => set('name', event.target.value)}
            placeholder={t('accounts.namePlaceholder')}
            required
          />
        </div>

        <div className="flex flex-col gap-2">
          <Label>{t('accounts.type')}</Label>
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
                  {accountTypeLabel(type)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div className="flex flex-col gap-2">
            <Label htmlFor="account-balance">{t('accounts.openingBalance')}</Label>
            <AmountField
              id="account-balance"
              value={draft.openingBalance}
              onChange={(value) => set('openingBalance', value)}
              placeholder="0,00"
              required
              allowNegative
            />
          </div>
          <div className="flex flex-col gap-2">
            {/* Ohne Stichtag wäre der Stand zu einem Zeitpunkt nicht
                berechenbar — man wüsste nicht, welche Buchungen schon
                im Anfangsbestand stecken. */}
            <Label htmlFor="account-date">{t('accounts.openingDate')}</Label>
            <DateField
              id="account-date"
              value={draft.openingDate}
              onChange={(iso) => set('openingDate', iso)}
            />
          </div>
        </div>

        {/* Die IBAN ist der Schlüssel zur Umbuchungserkennung: steht sie als
            Gegenpartei auf einer importierten Zeile, ist das keine Ausgabe,
            sondern eine Bewegung zwischen zwei eigenen Konten. Ein Import
            trägt sie von allein ein — von Hand ist sie für das Konto da,
            das nie eine Datei liefert. Meist das Sparkonto, und das ist
            genau das, wohin am häufigsten umgebucht wird. */}
        <div className="flex flex-col gap-2">
          <Label htmlFor="account-iban">{t('accounts.iban')}</Label>
          <Input
            id="account-iban"
            value={draft.externalRef ?? ''}
            onChange={(event) =>
              set('externalRef', event.target.value || null)
            }
            placeholder={t('accounts.ibanPlaceholder')}
            autoComplete="off"
            spellCheck={false}
          />
          <p className="text-muted-foreground text-xs">
            {t('accounts.ibanHint')}
          </p>
        </div>

        <div className="border-border flex items-center justify-between gap-4 rounded-lg border p-3">
          <span className="flex flex-col">
            <span className="text-sm font-medium">{t('common.defaultAccount')}</span>
            <span className="text-muted-foreground text-xs">
              {t('accounts.defaultHint')}
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
            <span className="text-sm font-medium">{t('accounts.countsAsAvailable')}</span>
            <span className="text-muted-foreground text-xs">
              {t('accounts.countsAsAvailableHint')}
            </span>
          </span>
          <Switch
            checked={draft.countsAsAvailable}
            onCheckedChange={(value) => set('countsAsAvailable', value)}
          />
        </div>

        <div className="border-border flex items-center justify-between gap-4 rounded-lg border p-3">
          <span className="flex flex-col">
            <span className="text-sm font-medium">{t('accounts.active')}</span>
            <span className="text-muted-foreground text-xs">
              {t('accounts.activeHint')}
            </span>
          </span>
          <Switch
            aria-label={t('accounts.active')}
            checked={draft.active}
            onCheckedChange={(value) => set('active', value)}
          />
        </div>
        </div>
    </DialogFrame>
  )
}
