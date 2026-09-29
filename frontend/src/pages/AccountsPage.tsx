import { useEffect, useId, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Plus, Star, Trash2 } from 'lucide-react'

import { ListRow } from '@/components/ListRow'
import { today, shortDate, fromIsoDay, toIsoDay, longDate } from '@/lib/dates'
import { EmptyState } from '@/components/EmptyState'
import { QueryState } from '@/components/QueryState'
import { SentenceWord } from '@/components/SentenceWord'
import { SentencePanel } from '@/components/SentencePanel'
import { SentenceChip } from '@/components/SentenceChip'
import { fillSentence } from '@/lib/sentence'
import { Calendar } from '@/components/ui/calendar'
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
  atLeast,
  accountTypeLabel,
  euro,
  isViewOnly,
  type Account,
  ACCOUNT_TYPES,
} from '@/lib/domain'
import { AccountCards } from '@/components/AccountCards'
import { useActiveMember } from '@/hooks/use-active-member'
import { OWN_SCOPE, type BookScope } from '@/lib/domain'
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

function emptyAccount(isFirst: boolean, ownerId?: string): Account {
  return {
    id: '',
    ownerId,
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
  const scope: BookScope =
    active.id === null ? OWN_SCOPE : { kind: 'member', ownerId: active.id }
  const accounts = useAccounts(scope)
  const accountsLevel = active.levelFor('accounts')
  const mayEdit = atLeast(accountsLevel, 'edit')
  const isAccountsViewOnly = isViewOnly(accountsLevel)
  // Your own you may always delete — as long as it is unused; another's needs `delete`.
  const mayDelete = active.member === null || atLeast(accountsLevel, 'delete')
  const [editing, setEditing] = useState<Account | null>(null)
  const [open, setOpen] = useState(false)
  // After a delete the row is gone; the focus goes to the page heading (rule 13).
  const heading = useRef<HTMLHeadingElement>(null)
  const deleted = useRef(false)

  const list = accounts.data ?? []

  function add() {
    deleted.current = false
    setEditing(emptyAccount(list.length === 0, active.id ?? undefined))
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
                : isAccountsViewOnly
                  ? t('accounts.leadMemberView', { name: active.member.firstName })
                  : null}
          </p>
        </div>
        {mayEdit && (
          <Button onClick={add}>
            <Plus className="size-4" />
            {t('accounts.create')}
          </Button>
        )}
      </header>

      {/* The balances used to sit in the book; the book is a tab of the plan now
          (#241), so they live with the accounts. */}
      {list.length > 0 && <AccountCards scope={scope} />}

      <QueryState
        isPending={accounts.isPending}
        error={accounts.error}
        onRetry={() => void accounts.refetch()}
        notShared={
          active.member
            ? t('accounts.notShared', { name: active.member.firstName })
            : undefined
        }
      >
        {list.length === 0 ? (
          <EmptyState
            action={
              mayEdit && (
                <Button onClick={add}>
                  <Plus className="size-4" />
                  {t('accounts.create')}
                </Button>
              )
            }
          >
            {t('accounts.empty')}
          </EmptyState>
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

  // Which sentence word is open — only one at a time (issue #215).
  const [openWord, setOpenWord] = useState<string | null>(null)
  const wordRefs = useRef<Record<string, HTMLButtonElement | null>>({})
  // Read out with every word and every opened field, so a screen reader hears the
  // whole sentence, not just the one word (issue #202, review D-215-3, fix 1).
  const sentenceId = useId()
  // The quiet second sentence for the IBAN and the switches (issue #215, review
  // D-215-4).
  const extrasSentenceId = useId()

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
    if (!open) {
      setConfirming(false)
      setOpenWord(null)
    }
  }, [open, account])

  const isEdit = Boolean(draft.id)

  function set<K extends keyof Account>(key: K, value: Account[K]) {
    setDraft((current) => ({ ...current, [key]: value }))
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

  const typeWord = (
    <SentenceWord
      ref={wordRef('type')}
      open={openWord === 'type'}
      onClick={() => toggleWord('type')}
      describedBy={sentenceId}
    >
      {accountTypeLabel(draft.type)}
    </SentenceWord>
  )
  const typePanel = openWord === 'type' && (
    <SentencePanel label={t('accounts.typeLabel')}>
      <div className="flex flex-wrap gap-2">
        {TYPES.map((type) => (
          <SentenceChip
            key={type}
            selected={draft.type === type}
            onClick={() => {
              set('type', type)
              closeWord()
            }}
          >
            {accountTypeLabel(type)}
          </SentenceChip>
        ))}
      </div>
    </SentencePanel>
  )

  const dateWord = (
    <SentenceWord
      ref={wordRef('date')}
      open={openWord === 'date'}
      onClick={() => toggleWord('date')}
      describedBy={sentenceId}
    >
      {longDate(draft.openingDate)}
    </SentenceWord>
  )
  const datePanel = openWord === 'date' && (
    <SentencePanel label={t('accounts.dateLabel')}>
      <Calendar
        mode="single"
        selected={fromIsoDay(draft.openingDate)}
        defaultMonth={fromIsoDay(draft.openingDate)}
        onSelect={(date) => {
          if (!date) return
          set('openingDate', toIsoDay(date))
          closeWord()
        }}
        aria-describedby={sentenceId}
        autoFocus
      />
    </SentencePanel>
  )

  // --- Rare facts, as a second sentence (issue #215, review D-215-4) --------

  const ibanWord = (
    <SentenceWord
      ref={wordRef('iban')}
      open={openWord === 'iban'}
      onClick={() => toggleWord('iban')}
      describedBy={extrasSentenceId}
    >
      {draft.externalRef ? t('accounts.ibanWith', { iban: draft.externalRef }) : t('accounts.noIban')}
    </SentenceWord>
  )
  const ibanPanel = openWord === 'iban' && (
    <SentencePanel label={t('accounts.iban')}>
      <Label htmlFor="account-iban" className="sr-only">
        {t('accounts.iban')}
      </Label>
      <Input
        id="account-iban"
        value={draft.externalRef ?? ''}
        onChange={(event) => set('externalRef', event.target.value || null)}
        placeholder={t('accounts.ibanPlaceholder')}
        autoComplete="off"
        spellCheck={false}
        aria-describedby={extrasSentenceId}
      />
      <p className="text-muted-foreground text-xs">{t('accounts.ibanHint')}</p>
    </SentencePanel>
  )

  const defaultWord = (
    <SentenceWord
      ref={wordRef('default')}
      open={openWord === 'default'}
      onClick={() => toggleWord('default')}
      describedBy={extrasSentenceId}
    >
      {draft.isDefault ? t('accounts.isDefault') : t('accounts.isNotDefault')}
    </SentenceWord>
  )
  const defaultPanel = openWord === 'default' && (
    <SentencePanel label={t('common.defaultAccount')}>
      <div className="flex flex-wrap gap-2">
        <SentenceChip
          selected={!draft.isDefault}
          onClick={() => {
            set('isDefault', false)
            closeWord()
          }}
        >
          {t('accounts.isNotDefault')}
        </SentenceChip>
        <SentenceChip
          selected={draft.isDefault}
          onClick={() => {
            set('isDefault', true)
            closeWord()
          }}
        >
          {t('accounts.isDefault')}
        </SentenceChip>
      </div>
      <p className="text-muted-foreground text-xs">{t('accounts.defaultHint')}</p>
    </SentencePanel>
  )

  const availableWord = (
    <SentenceWord
      ref={wordRef('available')}
      open={openWord === 'available'}
      onClick={() => toggleWord('available')}
      describedBy={extrasSentenceId}
    >
      {draft.countsAsAvailable ? t('accounts.isAvailable') : t('accounts.isNotAvailable')}
    </SentenceWord>
  )
  const availablePanel = openWord === 'available' && (
    <SentencePanel label={t('accounts.countsAsAvailable')}>
      <div className="flex flex-wrap gap-2">
        <SentenceChip
          selected={!draft.countsAsAvailable}
          onClick={() => {
            set('countsAsAvailable', false)
            closeWord()
          }}
        >
          {t('accounts.isNotAvailable')}
        </SentenceChip>
        <SentenceChip
          selected={draft.countsAsAvailable}
          onClick={() => {
            set('countsAsAvailable', true)
            closeWord()
          }}
        >
          {t('accounts.isAvailable')}
        </SentenceChip>
      </div>
      <p className="text-muted-foreground text-xs">{t('accounts.countsAsAvailableHint')}</p>
    </SentencePanel>
  )

  const activeWord = (
    <SentenceWord
      ref={wordRef('active')}
      open={openWord === 'active'}
      onClick={() => toggleWord('active')}
      describedBy={extrasSentenceId}
    >
      {draft.active ? t('accounts.isActive') : t('accounts.isClosed')}
    </SentenceWord>
  )
  const activePanel = openWord === 'active' && (
    <SentencePanel label={t('accounts.active')}>
      <div className="flex flex-wrap gap-2">
        <SentenceChip
          selected={draft.active}
          onClick={() => {
            set('active', true)
            closeWord()
          }}
        >
          {t('accounts.isActive')}
        </SentenceChip>
        <SentenceChip
          selected={!draft.active}
          onClick={() => {
            set('active', false)
            closeWord()
          }}
        >
          {t('accounts.isClosed')}
        </SentenceChip>
      </div>
      <p className="text-muted-foreground text-xs">{t('accounts.activeHint')}</p>
    </SentencePanel>
  )

  const extrasWords: Record<string, React.ReactNode> = {
    iban: ibanWord,
    default: defaultWord,
    available: availableWord,
    active: activeWord,
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
                    remove.mutate({ id: draft.id, name: draft.name }, {
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
      <div className="flex flex-col gap-6">
        <div className="flex flex-col gap-2">
          <Label htmlFor="account-name" className="sr-only">
            {t('positionDialog.label')}
          </Label>
          <Input
            id="account-name"
            value={draft.name}
            onChange={(event) => set('name', event.target.value)}
            placeholder={t('accounts.namePlaceholder')}
            required
            className="font-heading h-auto rounded-none border-0 border-b border-border bg-transparent px-0 pb-2 text-xl placeholder:text-muted-foreground focus-visible:border-primary focus-visible:ring-0 md:text-xl"
          />

          <div className="flex items-baseline gap-3">
            <Label htmlFor="account-balance" className="text-muted-foreground shrink-0 text-sm">
              {t('accounts.openingBalance')}
            </Label>
            <AmountField
              id="account-balance"
              value={draft.openingBalance}
              onChange={(value) => set('openingBalance', value)}
              placeholder="0,00"
              required
              allowNegative
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
          {/* Ohne Stichtag wäre der Stand zu einem Zeitpunkt nicht
              berechenbar — man wüsste nicht, welche Buchungen schon
              im Anfangsbestand stecken. */}
          <p id={sentenceId} className="text-base leading-relaxed">
            {fillSentence(t('accounts.sentence'), { type: typeWord, date: dateWord })}
          </p>
          {typePanel}
          {datePanel}
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
          {/* Die IBAN ist der Schlüssel zur Umbuchungserkennung: steht sie als
              Gegenpartei auf einer importierten Zeile, ist das keine Ausgabe,
              sondern eine Bewegung zwischen zwei eigenen Konten. Ein Import
              trägt sie von allein ein — von Hand ist sie für das Konto da,
              das nie eine Datei liefert. Meist das Sparkonto, und das ist
              genau das, wohin am häufigsten umgebucht wird. */}
          <p id={extrasSentenceId} className="text-muted-foreground text-base leading-relaxed">
            {fillSentence(t('accounts.extrasSentence'), extrasWords)}
          </p>
          {ibanPanel}
          {defaultPanel}
          {availablePanel}
          {activePanel}
        </div>
      </div>
    </DialogFrame>
  )
}
