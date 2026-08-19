import { useRef, useState } from 'react'
import { Upload } from 'lucide-react'

import { CategoryOptions } from '@/components/CategoryOptions'
import { QueryState } from '@/components/QueryState'
import { Button } from '@/components/ui/button'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { useActiveMember } from '@/hooks/use-active-member'
import { errorText } from '@/lib/api'
import { longDate } from '@/lib/dates'
import {
  BLOCK_DOT,
  OWN_SCOPE,
  atLeast,
  euro,
  type Category,
  type ImportedEntry,
  type ImportSummary,
} from '@/lib/domain'
import {
  useAccounts,
  useAssignEntry,
  useBookEntry,
  useDiscardEntry,
  useImportedEntries,
  useUploadStatement,
} from '@/lib/queries'

/**
 * Bank files in, bookings out.
 *
 * Three steps that stay apart on purpose: upload parks, assigning gives meaning,
 * booking is the only one that moves a balance. The middle one may sit half-done
 * for a week — that is what a parking area is for.
 */
export function ImportPage() {
  const active = useActiveMember()
  const mayEdit = atLeast(active.levelFor('accounts'), 'edit')

  const entries = useImportedEntries(active.id)
  // Accounts take a scope, not a plain id — see `BookScope`.
  const accounts = useAccounts(
    active.id === null ? OWN_SCOPE : { kind: 'member', ownerId: active.id }
  )
  const upload = useUploadStatement()

  const fileInput = useRef<HTMLInputElement>(null)
  const [summary, setSummary] = useState<ImportSummary | null>(null)
  const [pendingFile, setPendingFile] = useState<File | null>(null)

  function send(file: File, accountId?: string) {
    upload.mutate(
      { file, ownerId: active.id, accountId },
      {
        onSuccess: (result) => {
          setSummary(result)
          // An unknown IBAN is a question, not a failure: keep the file so the
          // answer does not cost a second pick from the file dialog.
          setPendingFile(result.unknownIban ? file : null)
        },
      }
    )
  }

  const rows = (entries.data ?? []).filter((entry) => entry.discardedAt === null)
  const open = rows.filter((entry) => entry.category === null).length

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-col gap-2">
          <h1 className="font-heading text-3xl font-semibold">Import</h1>
          <p className="text-muted-foreground">
            {active.member === null
              ? 'Lade eine Umsatzdatei deiner Bank hoch. Die Buchungen warten hier, bis du sie zuordnest.'
              : `Die Parkposition von ${active.member.firstName}.`}
          </p>
        </div>
        {mayEdit && (
          <>
            <input
              ref={fileInput}
              type="file"
              accept=".xml,.zip,application/xml,text/xml,application/zip"
              className="hidden"
              onChange={(event) => {
                const file = event.target.files?.[0]
                if (file) send(file)
                event.target.value = ''
              }}
            />
            <Button onClick={() => fileInput.current?.click()} disabled={upload.isPending}>
              <Upload className="size-4" />
              {upload.isPending ? 'Wird gelesen …' : 'Datei hochladen'}
            </Button>
          </>
        )}
      </header>

      {upload.isError && (
        <p className="text-destructive text-sm">{errorText(upload.error)}</p>
      )}

      {summary && (
        <Result
          summary={summary}
          accounts={accounts.data ?? []}
          onPickAccount={(accountId) => {
            if (pendingFile) send(pendingFile, accountId)
          }}
        />
      )}

      <QueryState isPending={entries.isPending} error={entries.error}>
        {rows.length === 0 ? (
          <p className="text-muted-foreground border-border rounded-lg border border-dashed p-10 text-center text-sm">
            Die Parkposition ist leer.
          </p>
        ) : (
          <>
            <p className="text-muted-foreground text-sm">
              {rows.length} geparkt
              {open > 0 && ` · ${open} ohne Kategorie`}
            </p>
            <EntryTable entries={rows} mayEdit={mayEdit} />
          </>
        )}
      </QueryState>
    </div>
  )
}

/** What the last upload did — and the one question it may have. */
function Result({
  summary,
  accounts,
  onPickAccount,
}: {
  summary: ImportSummary
  accounts: { id: string; name: string }[]
  onPickAccount: (accountId: string) => void
}) {
  if (summary.unknownIban) {
    return (
      <div className="border-border bg-card flex flex-col gap-3 rounded-lg border p-4">
        <p className="text-sm">
          Die Datei gehört zu <span className="font-medium">{summary.unknownIban}</span>.
          Zu welchem Konto?
        </p>
        <p className="text-muted-foreground text-sm">
          Duofy merkt sich die Antwort am Konto — beim nächsten Mal geht es von
          allein.
        </p>
        <Select onValueChange={onPickAccount}>
          <SelectTrigger className="max-w-xs">
            <SelectValue placeholder="Konto wählen …" />
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
    )
  }

  return (
    <div className="border-border bg-card flex flex-wrap gap-x-8 gap-y-2 rounded-lg border p-4 text-sm">
      <span>
        <span className="font-medium">{summary.read}</span> gelesen
      </span>
      <span>
        <span className="font-medium">{summary.parked}</span> geparkt
      </span>
      <span className="text-muted-foreground">
        {summary.known} schon bekannt
      </span>
      {!summary.balancesMatch && (
        <span className="text-destructive">
          Die Salden gehen nicht auf — fehlt eine Seite der Datei?
        </span>
      )}
    </div>
  )
}

/** The parked entries, grouped by day. */
function EntryTable({
  entries,
  mayEdit,
}: {
  entries: ImportedEntry[]
  mayEdit: boolean
}) {
  const assign = useAssignEntry()
  const book = useBookEntry()
  const discard = useDiscardEntry()

  let previousDay: string | null = null

  return (
    <div className="border-border bg-card overflow-x-auto rounded-lg border">
      <table className="w-full min-w-[52rem] border-collapse">
        <thead>
          <tr className="border-border border-b">
            {['Datum', 'Empfänger / Zahler', 'Verwendungszweck', 'Betrag', 'Kategorie', ''].map(
              (head, index) => (
                <th
                  key={head || index}
                  className={`text-muted-foreground px-3 py-2 text-xs font-medium tracking-wide uppercase ${
                    head === 'Betrag' ? 'text-right' : 'text-left'
                  }`}
                >
                  {head}
                </th>
              )
            )}
          </tr>
        </thead>
        <tbody>
          {entries.map((entry) => {
            const sameDay = entry.occurredOn === previousDay
            previousDay = entry.occurredOn

            return (
              <tr
                key={entry.id}
                className={`hover:bg-accent/40 ${sameDay ? '' : 'border-border border-t'}`}
              >
                <td
                  className={`px-3 py-2 text-sm whitespace-nowrap tabular-nums ${
                    sameDay ? 'text-transparent' : 'text-muted-foreground'
                  }`}
                >
                  {longDate(entry.occurredOn)}
                </td>
                <td className="px-3 py-2 text-sm font-medium">
                  {entry.counterpartyName ?? '—'}
                </td>
                <td className="text-muted-foreground max-w-[16rem] truncate px-3 py-2 text-sm">
                  {entry.purpose ?? ''}
                </td>
                <td
                  className={`px-3 py-2 text-right text-sm font-medium tabular-nums whitespace-nowrap ${
                    entry.incoming ? 'text-chart-4' : ''
                  }`}
                >
                  {entry.incoming ? '' : '−'}
                  {euro.format(Number(entry.amount))}
                </td>
                <td className="px-3 py-2">
                  <div className="flex items-center gap-2">
                    {entry.block && (
                      <span
                        className={`size-2 shrink-0 rounded-full ${BLOCK_DOT[entry.block]}`}
                      />
                    )}
                    <Select
                      value={entry.category ?? ''}
                      disabled={!mayEdit}
                      onValueChange={(value) =>
                        assign.mutate({ id: entry.id, category: value as Category })
                      }
                    >
                      <SelectTrigger className="h-8 max-w-[13rem]">
                        <SelectValue placeholder="wählen …" />
                      </SelectTrigger>
                      <SelectContent>
                        <CategoryOptions />
                      </SelectContent>
                    </Select>
                  </div>
                </td>
                <td className="px-3 py-2 text-right whitespace-nowrap">
                  {mayEdit && (
                    <>
                      <Button
                        size="sm"
                        variant="ghost"
                        disabled={entry.category === null || book.isPending}
                        onClick={() => book.mutate(entry.id)}
                      >
                        Buchen
                      </Button>
                      <Button
                        size="sm"
                        variant="ghost"
                        className="text-muted-foreground"
                        onClick={() => discard.mutate(entry.id)}
                      >
                        Verwerfen
                      </Button>
                    </>
                  )}
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
