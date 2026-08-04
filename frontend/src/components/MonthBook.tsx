import { useState } from 'react'
import { ArrowRight, Trash2 } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
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
  Empty,
  EmptyDescription,
  EmptyHeader,
  EmptyTitle,
} from '@/components/ui/empty'
import { QueryState } from '@/components/QueryState'
import { errorText } from '@/lib/api'
import { today } from '@/lib/dates'
import {
  OWN_SCOPE,
  BLOCK_SUGGESTION,
  CATEGORY_LABEL,
  euro,
  type Account,
  type Category,
  type PlanPosition,
  type Transaction,
  type BookScope,
} from '@/lib/domain'
import {
  useAccounts,
  useDeleteTransaction,
  useSaveTransaction,
  useTransactions,
} from '@/lib/queries'

/**
 * Das Haushaltsbuch eines Monats — was tatsächlich geflossen ist.
 *
 * Der Plan sagt, wie der Monat gedacht war. Das Buch sagt, wie er lief.
 * Verbunden sind sie nur an einer Stelle: eine Buchung **kann** einem Posten
 * zugeordnet werden, muss aber nicht. Der Kiosk ohne Planung steht trotzdem
 * drin.
 *
 * Die Schnelleingabe ist auf Tempo gebaut, weil man sie täglich benutzt.
 * Wählt man einen Posten, erbt die Buchung dessen Kategorie und Budget — dann
 * bleiben drei Felder. Ohne Posten kommt die Kategorie dazu, weil eine
 * Buchung ohne Zweck sonst im Buch läge, ohne irgendwo mitzuzählen.
 */

const CATEGORIES = Object.keys(CATEGORY_LABEL) as Category[]

type Props = {
  positions: PlanPosition[]
  year: number
  month: number
  /** Wessen Buch: das eigene, das einer Person oder das des Haushalts. */
  scope?: BookScope
  /**
   * Nur ansehen. Bei Stufe „darf ändern" ist das falsch: wer den Posten des
   * anderen abhaken darf, darf auch in dessen Buch buchen — das Abhaken
   * erzeugt ja genau so eine Buchung.
   */
  readOnly?: boolean
}

export function MonthBook({
  positions,
  year,
  month,
  scope = OWN_SCOPE,
  readOnly = false,
}: Props) {
  const transactions = useTransactions(year, month, scope)
  const accounts = useAccounts(scope).data ?? []
  const save = useSaveTransaction(year, month, scope)
  const remove = useDeleteTransaction(year, month, scope)

  const usable = accounts.filter((account) => account.active)
  const fallback = usable.find((account) => account.isDefault) ?? usable[0]

  if (usable.length === 0 && !readOnly) {
    return (
      <Empty className="border-border rounded-xl border border-dashed">
        <EmptyHeader>
          <EmptyTitle>Noch kein Konto</EmptyTitle>
          <EmptyDescription>
            Ohne Konto lässt sich nichts buchen — leg unter „Konten" eins an,
            dann geht es hier weiter.
          </EmptyDescription>
        </EmptyHeader>
      </Empty>
    )
  }

  return (
    <section className="flex flex-col gap-5">
      {/* Die Buchung landet beim **Kontobesitzer**, nicht beim Eintippenden —
          das entscheidet das Backend aus dem gewählten Konto. Zur Auswahl
          stehen hier ohnehin nur dessen Konten. */}
      {!readOnly && (
        <QuickEntry
          accounts={usable}
          fallbackAccountId={fallback?.id ?? ''}
          positions={positions}
          onSave={(draft) => save.mutate(draft)}
          pending={save.isPending}
          error={save.error}
        />
      )}

      <QueryState
        isPending={transactions.isPending}
        error={transactions.error}
        rows={3}
      >
        {transactions.data?.length === 0 ? (
          <p className="text-muted-foreground text-sm">
            Für diesen Monat ist noch nichts erfasst.
          </p>
        ) : (
          <ul className="flex flex-col">
            {transactions.data?.map((transaction) => (
              <Row
                key={transaction.id}
                transaction={transaction}
                accounts={accounts}
                positions={positions}
                onDelete={
                  readOnly ? null : () => remove.mutate(transaction.id)
                }
              />
            ))}
          </ul>
        )}
      </QueryState>
    </section>
  )
}

function QuickEntry({
  accounts,
  fallbackAccountId,
  positions,
  onSave,
  pending,
  error,
}: {
  accounts: Account[]
  fallbackAccountId: string
  positions: PlanPosition[]
  onSave: (draft: Partial<Transaction>) => void
  pending: boolean
  error: unknown
}) {
  const [amount, setAmount] = useState('')
  const [note, setNote] = useState('')
  const [positionId, setPositionId] = useState('none')
  const [category, setCategory] = useState<Category>('groceries')
  const [accountId, setAccountId] = useState(fallbackAccountId)
  const [counterAccountId, setCounterAccountId] = useState('none')

  const chosen = positions.find((position) => position.id === positionId)
  const isTransfer = counterAccountId !== 'none'

  function submit(event: React.FormEvent) {
    event.preventDefault()
    onSave({
      accountId,
      counterAccountId: isTransfer ? counterAccountId : null,
      occurredOn: today(),
      amount,
      note: note || null,
      // Vom Posten geerbt, sonst aus der Auswahl. Eine reine Umbuchung ohne
      // Posten braucht keinen Zweck — dort ist „wohin" die Antwort, nicht
      // „wofür".
      category: chosen ? chosen.category : isTransfer ? null : category,
      block: chosen ? chosen.block : isTransfer ? null : BLOCK_SUGGESTION[category],
      positionId: chosen ? chosen.id : null,
    })
    setAmount('')
    setNote('')
  }

  return (
    <form
      onSubmit={submit}
      className="bg-card flex flex-col gap-3 rounded-xl p-4 ring-1 ring-foreground/10"
    >
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex w-28 flex-col gap-1.5">
          <Label htmlFor="book-amount" className="text-xs">
            Betrag
          </Label>
          <Input
            id="book-amount"
            type="number"
            step="0.01"
            min="0.01"
            inputMode="decimal"
            value={amount}
            onChange={(event) => setAmount(event.target.value)}
            placeholder="0,00"
            required
          />
        </div>

        <div className="flex min-w-40 flex-1 flex-col gap-1.5">
          <Label htmlFor="book-note" className="text-xs">
            Notiz
          </Label>
          <Input
            id="book-note"
            value={note}
            onChange={(event) => setNote(event.target.value)}
            placeholder="Rewe"
          />
        </div>

        <div className="flex min-w-44 flex-col gap-1.5">
          <Label className="text-xs">Posten</Label>
          <Select value={positionId} onValueChange={setPositionId}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="none">Keinem Posten</SelectItem>
              {positions.map((position) => (
                <SelectItem key={position.id} value={position.id}>
                  {position.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {/* Nur nötig, wenn kein Posten gewählt ist — sonst erbt die Buchung
            Kategorie und Budget von dort. */}
        {!chosen && !isTransfer && (
          <div className="flex min-w-40 flex-col gap-1.5">
            <Label className="text-xs">Kategorie</Label>
            <Select
              value={category}
              onValueChange={(value) => setCategory(value as Category)}
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {CATEGORIES.map((item) => (
                  <SelectItem key={item} value={item}>
                    {CATEGORY_LABEL[item]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        )}

        <div className="flex min-w-40 flex-col gap-1.5">
          <Label className="text-xs">Konto</Label>
          <Select value={accountId} onValueChange={setAccountId}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {accounts.map((account) => (
                <SelectItem key={account.id} value={account.id}>
                  {account.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {/* Gesetzt = Umbuchung. Sie verschiebt den Stand zwischen zwei
            eigenen Konten und ist weder Einnahme noch Ausgabe — es sei denn,
            ein Posten ist gewählt. Geld aufs Tagesgeld legen erfüllt so die
            Sparquote, PayPal aufladen dagegen nicht. */}
        <div className="flex min-w-40 flex-col gap-1.5">
          <Label className="text-xs">Umbuchung nach</Label>
          <Select value={counterAccountId} onValueChange={setCounterAccountId}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="none">Keine Umbuchung</SelectItem>
              {accounts
                .filter((account) => account.id !== accountId)
                .map((account) => (
                  <SelectItem key={account.id} value={account.id}>
                    {account.name}
                  </SelectItem>
                ))}
            </SelectContent>
          </Select>
        </div>

        <Button type="submit" disabled={pending}>
          {pending ? 'Sichert…' : isTransfer ? 'Umbuchen' : 'Buchen'}
        </Button>
      </div>

      <p className="text-muted-foreground text-xs">
        Datum ist heute. Eine Umbuchung mit Posten füllt zugleich das Budget —
        so wird aus „aufs Tagesgeld gelegt" eine erfüllte Sparquote.
      </p>

      {Boolean(error) && (
        <p role="alert" className="text-destructive text-sm">
          {errorText(error)}
        </p>
      )}
    </form>
  )
}

function Row({
  transaction,
  accounts,
  positions,
  onDelete,
}: {
  transaction: Transaction
  accounts: Account[]
  positions: PlanPosition[]
  /** null = fremdes Buch, dann fehlt der Knopf ganz. */
  onDelete: (() => void) | null
}) {
  const account = accounts.find((item) => item.id === transaction.accountId)
  const counter = accounts.find(
    (item) => item.id === transaction.counterAccountId
  )
  const position = positions.find((item) => item.id === transaction.positionId)
  const isTransfer = transaction.counterAccountId !== null

  return (
    <li className="border-border/60 grid grid-cols-[auto_1fr_auto_auto] items-center gap-3 border-b py-2.5 last:border-b-0">
      <span className="text-muted-foreground w-12 text-sm tabular-nums">
        {new Date(transaction.occurredOn).getDate()}.
      </span>

      <span className="flex min-w-0 flex-col gap-0.5">
        <span className="flex flex-wrap items-center gap-2">
          <span className="font-medium">
            {transaction.note ?? position?.label ?? 'Ohne Notiz'}
          </span>
          {transaction.autoBooked && (
            <Badge variant="outline" className="font-normal">
              vom Haken
            </Badge>
          )}
        </span>
        <span className="text-muted-foreground truncate text-xs">
          {isTransfer ? (
            <>
              {account?.name} <ArrowRight className="inline size-3" />{' '}
              {counter?.name} · Umbuchung
              {transaction.ownerName ? ` · ${transaction.ownerName}` : ''}
            </>
          ) : (
            <>
              {account?.name}
              {transaction.category
                ? ` · ${CATEGORY_LABEL[transaction.category]}`
                : ''}
              {position ? ` · ${position.label}` : ''}
              {/* Nur im gemeinsamen Buch gesetzt — dort ist der Name der
                  Unterschied zwischen zwei sonst gleichen Zeilen. */}
              {transaction.ownerName ? ` · ${transaction.ownerName}` : ''}
            </>
          )}
        </span>
      </span>

      <span className="font-mono font-medium tabular-nums">
        {euro.format(Number(transaction.amount))}
      </span>

      {onDelete === null ? (
        // Platzhalter, damit die Spalten aller Zeilen gleich breit bleiben.
        <span className="size-9" />
      ) : (
        <Button
          type="button"
          variant="ghost"
          size="icon"
          aria-label={`Buchung ${transaction.note ?? ''} löschen`}
          onClick={onDelete}
          className="text-muted-foreground hover:text-destructive"
        >
          <Trash2 className="size-4" />
        </Button>
      )}
    </li>
  )
}
