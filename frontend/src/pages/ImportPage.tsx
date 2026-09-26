import { Fragment, useRef, useState } from 'react'
import { Trans, useTranslation } from 'react-i18next'
import { AlertTriangle, ArrowLeftRight, Upload } from 'lucide-react'

import { CategoryPicker } from '@/components/CategoryPicker'
import { PositionPicker, type PositionMonth } from '@/components/PositionPicker'
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
  BUDGET_DOT,
  categoryLabel,
  OWN_SCOPE,
  atLeast,
  euro,
  type Account,
  type ImportedEntry,
  type ImportSummary,
  type PlanPosition,
} from '@/lib/domain'
import {
  useAcceptSuggestion,
  useAccounts,
  useAssignEntry,
  useBookEntry,
  useDiscardEntry,
  useImportedEntries,
  usePlansForMonths,
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
  const { t } = useTranslation()
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

  // Every parked entry needs the positions of **its own** month, and a pile
  // usually spans two or three.
  const months = uniqueMonths(rows)
  const plans = usePlansForMonths(months, active.id)
  const positionsByMonth = new Map<string, PlanPosition[]>()
  months.forEach(({ year, month }, index) => {
    const plan = plans[index]?.data
    if (plan) positionsByMonth.set(`${year}-${month}`, plan.positions)
  })

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-col gap-2">
          <h1 className="font-heading text-3xl font-semibold">{t('nav.import')}</h1>
          <p className="text-muted-foreground">
            {active.member === null
              ? t('import.lead')
              : t('import.leadMember', { name: active.member.firstName })}
          </p>
        </div>
        {mayEdit && (
          <>
            <input
              ref={fileInput}
              type="file"
              accept=".xml,.zip,.csv,application/xml,text/xml,application/zip,text/csv"
              className="hidden"
              onChange={(event) => {
                const file = event.target.files?.[0]
                if (file) send(file)
                event.target.value = ''
              }}
            />
            <Button onClick={() => fileInput.current?.click()} disabled={upload.isPending}>
              <Upload className="size-4" />
              {upload.isPending ? t('import.reading') : t('import.upload')}
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
            {t('import.empty')}
          </p>
        ) : (
          <>
            <p className="text-muted-foreground text-sm">
              {t('import.parkedCount', { number: rows.length })}
              {open > 0 && ` · ${t('import.withoutCategory', { number: open })}`}
            </p>
            <EntryTable
              entries={rows}
              mayEdit={mayEdit}
              positionsByMonth={positionsByMonth}
              accounts={accounts.data ?? []}
            />
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
  const { t } = useTranslation()
  if (summary.unknownIban) {
    return (
      <div className="border-border bg-card flex flex-col gap-3 rounded-lg border p-4">
        <p className="text-sm">
          <Trans
            i18nKey="import.unknownIban"
            values={{ iban: summary.unknownIban }}
            components={{ iban: <span className="font-medium" /> }}
          />
        </p>
        <p className="text-muted-foreground text-sm">
          {t('import.rememberAccount')}
        </p>
        <Select onValueChange={onPickAccount}>
          <SelectTrigger className="max-w-xs">
            <SelectValue placeholder={t('import.pickAccount')} />
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
        <Trans
          i18nKey="import.read"
          values={{ number: summary.read }}
          components={{ number: <span className="font-medium" /> }}
        />
      </span>
      <span>
        <Trans
          i18nKey="import.parked"
          values={{ number: summary.parked }}
          components={{ number: <span className="font-medium" /> }}
        />
      </span>
      <span className="text-muted-foreground">
        {t('import.known', { number: summary.known })}
      </span>
      {!summary.balancesMatch && (
        <span className="text-destructive">
          {t('import.balancesDiffer')}
        </span>
      )}
    </div>
  )
}

/** The month an entry was booked in, and the one after it. */
function monthsFor(entry: ImportedEntry): { year: number; month: number }[] {
  const [year, month] = entry.occurredOn.split('-').map(Number)
  return [
    { year, month },
    month === 12 ? { year: year + 1, month: 1 } : { year, month: month + 1 },
  ]
}

/**
 * The two months this entry may belong to, each with its positions.
 *
 * A planning month is not a calendar month: rent leaves the account on the 28th
 * for the month starting on the 1st. Offering only the calendar month means the
 * entries easiest to place are the ones with no position to place them on.
 */
function choicesFor(
  entry: ImportedEntry,
  positionsByMonth: Map<string, PlanPosition[]>
): PositionMonth[] {
  return monthsFor(entry).map(({ year, month }) => ({
    year,
    month,
    positions: positionsByMonth.get(`${year}-${month}`) ?? [],
  }))
}

/**
 * The label of a suggested position, looked for in both months.
 *
 * Naming it matters more than it looks: a suggestion pointing at "Miete" is
 * checkable, one pointing at an id is not.
 */
function namePosition(
  entry: ImportedEntry,
  positionsByMonth: Map<string, PlanPosition[]>,
  positionId: string
): string {
  for (const { positions } of choicesFor(entry, positionsByMonth)) {
    const found = positions.find((position) => position.id === positionId)
    if (found) return found.label
  }
  return ''
}

/** Every month a pile of entries can reach, each once. */
function uniqueMonths(entries: ImportedEntry[]): { year: number; month: number }[] {
  const seen = new Map<string, { year: number; month: number }>()
  for (const entry of entries) {
    for (const { year, month } of monthsFor(entry)) {
      seen.set(`${year}-${month}`, { year, month })
    }
  }
  return [...seen.values()]
}

function EntryTable({
  entries,
  mayEdit,
  positionsByMonth,
  accounts,
}: {
  entries: ImportedEntry[]
  mayEdit: boolean
  positionsByMonth: Map<string, PlanPosition[]>
  accounts: Account[]
}) {
  const assign = useAssignEntry()
  const { t } = useTranslation()
  const accept = useAcceptSuggestion()
  const book = useBookEntry()
  const discard = useDiscardEntry()

  let previousDay: string | null = null

  return (
    <div className="border-border bg-card overflow-x-auto rounded-lg border">
      <table className="w-full min-w-[52rem] border-collapse">
        <thead>
          <tr className="border-border border-b">
            {[
              'import.table.date',
              'import.table.counterparty',
              'import.table.purpose',
              'common.amount',
              'import.table.position',
              'common.category',
              '',
            ].map(
              (head, index) => (
                <th
                  key={head || index}
                  className={`text-muted-foreground px-3 py-2 text-xs font-medium tracking-wide uppercase ${
                    head === 'common.amount' ? 'text-right' : 'text-left'
                  }`}
                >
                  {head && t(head)}
                </th>
              )
            )}
          </tr>
        </thead>
        <tbody>
          {entries.map((entry) => {
            const sameDay = entry.occurredOn === previousDay
            previousDay = entry.occurredOn

            const suggestion = entry.suggestion

            return (
              <Fragment key={entry.id}>
              <tr
                className={`hover:bg-accent/40 ${sameDay ? '' : 'border-border border-t'} ${
                  suggestion ? 'border-b-0' : ''
                }`}
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
                  {entry.counterAccountId === null ? (
                    <PositionPicker
                      months={choicesFor(entry, positionsByMonth)}
                      value={entry.positionId}
                      disabled={!mayEdit}
                      className="h-8 max-w-[12rem]"
                      onChange={(positionId) =>
                        assign.mutate({ id: entry.id, positionId })
                      }
                    />
                  ) : (
                    // Eine Umbuchung füllt keinen Posten. Das Feld wegzulassen
                    // ist die Aussage — ein leeres, das trotzdem anklickbar
                    // wäre, lädt zu einer Zuordnung ein, die beim Buchen
                    // wieder verschwindet.
                    <span className="text-muted-foreground text-sm">—</span>
                  )}
                </td>
                <td className="px-3 py-2">
                  <div className="flex items-center gap-2">
                    {entry.budget && (
                      <span
                        className={`size-2 shrink-0 rounded-full ${BUDGET_DOT[entry.budget]}`}
                      />
                    )}
                    {entry.counterAccountId !== null ? (
                      // Umbuchung: keine Kategorie, sondern ein Ziel. Die
                      // Frage lautet wohin, nicht wofür — das Geld wurde nicht
                      // ausgegeben, es liegt woanders.
                      <span className="flex items-center gap-2 text-sm">
                        <ArrowLeftRight className="text-muted-foreground size-3.5" />
                        {entry.incoming ? t('import.from') : t('import.to')}{' '}
                        {accounts.find(
                          (account) => account.id === entry.counterAccountId
                        )?.name ?? t('import.otherAccount')}
                        {mayEdit && (
                          <button
                            type="button"
                            className="text-muted-foreground hover:text-foreground text-xs underline"
                            onClick={() =>
                              assign.mutate({
                                id: entry.id,
                                counterAccountId: null,
                              })
                            }
                          >
                            {t('import.undo')}
                          </button>
                        )}
                      </span>
                    ) : entry.positionId !== null ? (
                      // The position carries the category, so it is a
                      // consequence here and not a question.
                      <span
                        className="text-sm"
                        title={t('import.fromPosition')}
                      >
                        {entry.category ? categoryLabel(entry.category) : '—'}
                      </span>
                    ) : (
                      <CategoryPicker
                        value={entry.category}
                        disabled={!mayEdit}
                        placeholder={t('import.pick')}
                        className="h-8 max-w-[13rem]"
                        onChange={(category) =>
                          assign.mutate({ id: entry.id, category })
                        }
                      />
                    )}
                  </div>
                </td>
                <td className="px-3 py-2 text-right whitespace-nowrap">
                  {mayEdit && (
                    <>
                      <Button
                        size="sm"
                        variant="ghost"
                        disabled={
                          (entry.category === null &&
                            entry.counterAccountId === null) ||
                          book.isPending
                        }
                        onClick={() => book.mutate(entry.id)}
                      >
                        {t('monthBook.book')}
                      </Button>
                      <Button
                        size="sm"
                        variant="ghost"
                        className="text-muted-foreground"
                        onClick={() => discard.mutate(entry.id)}
                      >
                        {t('import.discard')}
                      </Button>
                    </>
                  )}
                </td>
              </tr>

              {/* Der Vorschlag steht **unter** der Zeile, nicht in ihren Feldern.
                  Ein vorausgefülltes Feld ist von einem gewählten nicht zu
                  unterscheiden — hier ist auf einen Blick klar, was Duofy meint
                  und was du entschieden hast. */}
              {suggestion && (
                <tr className="hover:bg-accent/40">
                  <td />
                  <td />
                  <td colSpan={5} className="px-3 pb-2">
                    {suggestion.kind === 'already_booked' ? (
                      /* Kein Vorschlag, sondern eine Warnung. Diese Zeile ist
                         die zweite Hälfte einer Bewegung, die schon im Buch
                         steht — sie zu buchen zählt dasselbe Geld doppelt.
                         Deshalb steht hier kein „Übernehmen". */
                      <div className="text-muted-foreground flex flex-wrap items-center gap-2 text-sm">
                        <AlertTriangle className="size-4 text-amber-600 dark:text-amber-500" />
                        <span className="text-foreground">
                          {suggestion.certain
                            ? t('import.alreadyBooked', { name: suggestion.counterAccountName })
                            : t('import.maybeBooked', { name: suggestion.counterAccountName })}
                        </span>
                        {mayEdit && (
                          <Button
                            size="sm"
                            variant="outline"
                            className="ml-1 h-7"
                            disabled={discard.isPending}
                            onClick={() => discard.mutate(entry.id)}
                          >
                            {t('import.discard')}
                          </Button>
                        )}
                      </div>
                    ) : (
                      <div className="text-muted-foreground flex flex-wrap items-center gap-2 text-sm">
                        <span className="text-primary">{t('import.suggestion')}</span>
                        {suggestion.kind === 'transfer' ? (
                          <span className="text-foreground flex items-center gap-1.5">
                            <ArrowLeftRight className="size-3.5" />
                            {/* Geraten wird als Frage formuliert, Erkanntes als
                                Aussage. Beides gleich zu zeigen ließe den
                                sicheren Fall so wacklig aussehen wie den
                                anderen. */}
                            {suggestion.certain ? t('budget.transfer') : t('import.transferGuess')}{' '}
                            {entry.incoming ? t('import.from') : t('import.to')}{' '}
                            {suggestion.counterAccountName}
                          </span>
                        ) : (
                          <>
                            <span className="text-foreground">
                              {suggestion.category
                                ? categoryLabel(suggestion.category)
                                : ''}
                            </span>
                            {suggestion.positionId && (
                              <span>
                                · {t('import.suggestedPosition')}{' '}
                                {namePosition(
                                  entry,
                                  positionsByMonth,
                                  suggestion.positionId
                                )}
                              </span>
                            )}
                          </>
                        )}
                        <span className="text-xs">({suggestion.reason})</span>
                        {mayEdit && (
                          <Button
                            size="sm"
                            variant="outline"
                            className="ml-1 h-7"
                            disabled={accept.isPending}
                            onClick={() =>
                              accept.mutate({
                                id: entry.id,
                                category: suggestion.category,
                                positionId: suggestion.positionId,
                                counterAccountId: suggestion.counterAccountId,
                              })
                            }
                          >
                            {suggestion.kind === 'transfer'
                              ? t('import.bookAsTransfer')
                              : t('import.accept')}
                          </Button>
                        )}
                      </div>
                    )}
                  </td>
                </tr>
              )}
              </Fragment>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
