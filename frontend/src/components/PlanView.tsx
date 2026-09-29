import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router'
import { Eye, Pencil, Plus, Printer, Trash2, Users } from 'lucide-react'

import { AddBookingButton } from '@/components/AddBookingButton'
import { BookMetrics } from '@/components/BookMetrics'
import { BudgetSection } from '@/components/BudgetSection'
import { CarryOverCard } from '@/components/CarryOverCard'
import { EmptyState } from '@/components/EmptyState'
import { Metric } from '@/components/Metric'
import { MonthBook, type BookFilter } from '@/components/MonthBook'
import { MonthFlow } from '@/components/MonthFlow'
import { MonthHints } from '@/components/MonthHints'
import { usePaidFlow } from '@/components/PaidFlow'
import { PlanPrintout } from '@/components/PlanPrintout'
import { PlanSankey } from '@/components/PlanSankey'
import { PositionDialog } from '@/components/PositionDialog'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Empty, EmptyDescription, EmptyHeader, EmptyTitle } from '@/components/ui/empty'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { ApiError } from '@/lib/api'
import { longDate, today } from '@/lib/dates'
import {
  BUDGETS,
  isPaid,
  monthLabel,
  OWN_SCOPE,
  QUOTA_KEY,
  stillDue,
  type Budget,
  type BookScope,
  type HouseholdPlanDetail,
  type HouseholdPosition,
  type PlanDetail,
  type PlanPosition,
} from '@/lib/domain'
import type { PlanRights } from '@/lib/plan-rights'
import {
  useAccounts,
  useDeletePosition,
  useMe,
  useSavePosition,
  useTransactions,
} from '@/lib/queries'

/**
 * The book's tab, with how many bookings the month holds — "Buch · 7". A carry-over
 * is a balance, not a booking, so it is not counted. Where the book cannot be read
 * (no grant yet, still loading) it is just "Buch".
 */
function BookTabTrigger({
  year,
  month,
  scope,
}: {
  year: number
  month: number
  scope: BookScope
}) {
  const { t } = useTranslation()
  const rows = useTransactions(year, month, scope).data
  return (
    <TabsTrigger value="book">
      {rows
        ? t('plan.tabBookCount', {
            count: rows.filter((row) => row.kind !== 'carry_over').length,
          })
        : t('plan.tabBook')}
    </TabsTrigger>
  )
}

/**
 * The book tab (#241): the bookings of this plan month.
 *
 * The filter lives in the address (`?filter=unplanned`), so the "Ungeplant" rows of
 * the plan can link straight to it and a reload keeps it. For somebody else's plan
 * the book hangs on the accounts grant, not on the plan one — a missing grant is
 * said in words, not shown as an error.
 */
function PlanBook({
  year,
  month,
  positions,
  scope,
  readOnly,
  notSharedWith,
}: {
  year: number
  month: number
  positions: PlanPosition[]
  scope: BookScope
  readOnly: boolean
  /** First name of the person whose plan this is, when it is not one's own. */
  notSharedWith?: string
}) {
  const { t } = useTranslation()
  const [params, setParams] = useSearchParams()
  const filter: BookFilter = params.get('filter') === 'unplanned' ? 'unplanned' : 'all'
  const transactions = useTransactions(year, month, scope)

  if (
    notSharedWith &&
    transactions.error instanceof ApiError &&
    transactions.error.code === 'no_insight_granted'
  ) {
    return <EmptyState>{t('book.notShared', { name: notSharedWith })}</EmptyState>
  }

  return (
    <MonthBook
      positions={positions}
      year={year}
      month={month}
      scope={scope}
      readOnly={readOnly}
      filter={filter}
      onFilterChange={(next) =>
        setParams(
          (current: URLSearchParams) => {
            if (next === 'all') current.delete('filter')
            else current.set('filter', next)
            return current
          },
          { replace: true }
        )
      }
    />
  )
}

/** Where the curve starts: the carry-over of the default account, if it has one. */
function OwnCarryOver({ year, month }: { year: number; month: number }) {
  const accounts = useAccounts()
  const transactions = useTransactions(year, month)
  const defaultAccount = accounts.data?.find(
    (account) => account.active && account.isDefault
  )
  if (!defaultAccount) return null
  const carryOver = transactions.data?.find(
    (entry) => entry.kind === 'carry_over' && entry.accountId === defaultAccount.id
  )
  return (
    <CarryOverCard
      account={defaultAccount}
      carryOver={carryOver}
      year={year}
      month={month}
    />
  )
}

/**
 * One month of a plan, the same for everybody who looks at it (#251, decision 56).
 *
 * The own plan, another person's plan and the household plan are this one view:
 * header, figures, the three tabs, the position dialogs and the printout. The
 * callers only hand over where the data comes from (`scope`) and what the viewer
 * may do (`rights`). The household plan is composed, not stored — it has no `id`
 * and owns nothing, so it never adds anything and its dialog only edits.
 */
export function PlanView({
  plan,
  scope = OWN_SCOPE,
  rights,
  ownerName = null,
  lead,
  standIn = false,
  tab,
  onTab,
  onOpenUnplanned,
  onDeleteMonth,
}: {
  plan: PlanDetail | HouseholdPlanDetail
  scope?: BookScope
  rights: PlanRights
  /** First name of the person whose plan this is; only for another person's. */
  ownerName?: string | null
  /** The sentence under the title. */
  lead: string
  /** The viewer acts on somebody's behalf and may change things. */
  standIn?: boolean
  tab: string
  onTab: (value: string) => void
  onOpenUnplanned: () => void
  onDeleteMonth?: () => void
}) {
  const { t } = useTranslation()
  const household = scope.kind === 'household'
  const member = scope.kind === 'member'
  const planId = 'id' in plan ? plan.id : ''
  const positions: PlanPosition[] = plan.positions

  const groups = BUDGETS.map((budget) => {
    const key = budget as keyof typeof QUOTA_KEY
    return {
      budget,
      rows: positions.filter((row) => row.budget === budget),
      target: Number(plan.distributable) * (Number(plan[QUOTA_KEY[key]]) / 100),
    }
  })

  // Income deliberately sits outside `groups`: it has no quota and must not flow
  // into `allocated`, otherwise the remainder would be wrong.
  const incomeRows = positions.filter((row) => row.budget === 'income')

  // What is left to allocate is the free remainder of the distributable amount,
  // not the amount itself.
  const allocated = groups.reduce(
    (total, group) =>
      total +
      group.rows.reduce(
        // Pass-through money was never distributable — neither in
        // `plan.distributable` above nor here. Subtracting it only here would make
        // the remainder too large.
        (sum, row) => (row.passThrough ? sum : sum + Number(row.amountPlanned)),
        0
      ),
    0
  )
  const free = Number(plan.distributable) - allocated

  // What is still open is what has to be paid this month. Partial amounts already
  // recorded are subtracted, see `stillDue`.
  const unpaid = positions.reduce((sum, row) => sum + stillDue(row), 0)

  const me = useMe()
  const myId = me.data?.id ?? null
  const savePosition = useSavePosition()
  const deletePosition = useDeletePosition()
  const { toggle, dialogs: paidDialogs } = usePaidFlow(plan.year, plan.month, scope)
  // The box goes both ways: an open one asks for ticking, a ticked one for taking
  // the tick back — the two need different steps of the book grant.
  const tickReadOnly = (position: PlanPosition) =>
    isPaid(position) ? !rights.untickPosition(position) : !rights.tickPosition(position)
  const [editing, setEditing] = useState<PlanPosition | null>(null)
  const [addingTo, setAddingTo] = useState<Budget>('wants')
  const [dialogOpen, setDialogOpen] = useState(false)
  // An error from the last try must not greet the next opening.
  const resetSaveposition = savePosition.reset
  useEffect(() => {
    if (dialogOpen) resetSaveposition()
  }, [dialogOpen, resetSaveposition])

  function openEditor(position: PlanPosition) {
    setEditing(position)
    setDialogOpen(true)
  }

  function handleAdd(budget: Budget) {
    setEditing(null)
    setAddingTo(budget)
    setDialogOpen(true)
  }

  // Sits in the row behind the category, for example "Rent · 1st · Mia" — only
  // where "who carries what" is the point, in the household plan.
  const rowOwner = household
    ? (position: PlanPosition) => (position as HouseholdPosition).ownerName ?? null
    : undefined

  // Whose position the dialog shows, when it is not the viewer's own.
  const dialogOwner = member
    ? ownerName
    : household && editing && (editing as HouseholdPosition).ownerId !== myId
      ? (editing as HouseholdPosition).ownerName
      : null

  const flowScope = {
    householdId: scope.kind === 'household' ? scope.householdId : null,
    ownerId: scope.kind === 'member' ? scope.ownerId : null,
  }

  const printHeader = household
    ? t('plan.printHeaderHousehold', {
        household: (plan as HouseholdPlanDetail).householdName,
        month: monthLabel(plan.month),
        year: plan.year,
        date: longDate(today()),
      })
    : member && ownerName
      ? t('plan.printHeaderMember', {
          name: ownerName,
          month: monthLabel(plan.month),
          year: plan.year,
          date: longDate(today()),
        })
      : t('plan.printHeader', {
          month: monthLabel(plan.month),
          year: plan.year,
          date: longDate(today()),
        })

  return (
    <>
      {/* Nur auf Papier: ohne Topbar fehlte sonst jeder Hinweis, was das Blatt
          ist und von wann es stammt. */}
      <p className="text-muted-foreground hidden text-xs print:block">{printHeader}</p>

      <header className="flex flex-wrap items-end justify-between gap-4">
        {/* `min-w-0 flex-1`: ohne das nimmt sich der Textblock die volle Breite
            und schiebt die Knopfgruppe auf eine eigene Zeile — dort steht sie
            dann links statt rechts oben. */}
        <div className="flex min-w-0 flex-1 flex-col gap-2">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="font-heading text-3xl font-semibold">
              {monthLabel(plan.month)} {plan.year}
            </h1>
            {member && ownerName && (
              <Badge variant="secondary" className="gap-1 font-normal">
                <Eye className="size-3" />
                {ownerName}
              </Badge>
            )}
            {household && (
              <Badge variant="secondary" className="gap-1 font-normal">
                <Users className="size-3" />
                {(plan as HouseholdPlanDetail).householdName}
              </Badge>
            )}
            {standIn && (
              <Badge variant="outline" className="gap-1 font-normal">
                <Pencil className="size-3" />
                {t('plan.standIn')}
              </Badge>
            )}
          </div>
          <p className="text-muted-foreground print:hidden">{lead}</p>
        </div>
        {/* Eigene Gruppe: der Kopf hat `justify-between` und genau zwei
            Kinder. Ein dritter Knopf direkt daneben landete in der Mitte,
            statt rechts bei den anderen zu bleiben. */}
        <div className="flex shrink-0 flex-wrap items-center gap-2" data-print="hide">
          {/* Drucken wechselt vorher auf den Plan: gedruckt wird, was im DOM
              steht, und bei offenem Buch-Reiter wäre das das Buch. */}
          <Button
            variant="outline"
            onClick={() => {
              // Only switch the tab: what gets printed is what is in the DOM. The
              // print version of the charts is permanently mounted, so Ctrl+P works
              // without this button too.
              onTab('plan')
              requestAnimationFrame(() => window.print())
            }}
          >
            <Printer className="size-4" />
            {t('plan.print')}
          </Button>
          {rights.addBooking && (
            <AddBookingButton
              year={plan.year}
              month={plan.month}
              positions={positions}
              scope={scope}
            />
          )}
          {rights.addPosition && (
            <Button onClick={() => handleAdd('wants')}>
              <Plus className="size-4" />
              {t('positionDialog.addTitle')}
            </Button>
          )}
          {rights.deleteMonth && (
            <Button
              variant="ghost"
              className="text-destructive"
              disabled={!plan.deletable}
              onClick={onDeleteMonth}
            >
              <Trash2 className="size-4" />
              {t('plan.deleteMonth')}
            </Button>
          )}
        </div>
      </header>

      {/* Nur sichtbar, solange der Knopf oben deaktiviert ist — dieselbe
          Erklärung wie im Fehlerfall (409), nur schon vorher gesagt. */}
      {rights.deleteMonth && !plan.deletable && (
        <p className="text-muted-foreground text-sm print:hidden" data-print="hide">
          {t('errors.plan_has_transactions')}
        </p>
      )}

      {/* Zwei Sichten, zwei Kartensätze. Der Plan rechnet mit dem Soll —
          „Verplanbar" ist Budget minus verteilte Posten und darf sich während
          des Monats nicht bewegen, sonst taugt es zum Planen nicht. Das Buch
          zeigt daneben, was wirklich geflossen ist. */}
      {tab === 'book' ? (
        <BookMetrics
          year={plan.year}
          month={plan.month}
          positions={positions}
          scope={scope}
        />
      ) : (
        <section className="grid gap-3 sm:grid-cols-3">
          <Metric label={t('plan.income')} value={Number(plan.income)} />
          <Metric
            label={t('plans.allocatable')}
            value={free}
            hint={t('plan.notDistributed')}
            strong
            tone={free < 0 ? 'over' : 'neutral'}
          />
          <Metric label={t('plans.open')} value={unpaid} hint={t('plan.notPaid')} />
        </section>
      )}
      {tab !== 'book' && <MonthHints hints={plan.hints} />}

      {/* Tabs statt Untereinander: der Verlauf beantwortet eine andere Frage
          als die Postenliste — „geht der Monat auf" gegen „was steht drin". */}
      <Tabs value={tab} onValueChange={onTab} className="gap-6">
        <TabsList data-print="hide">
          <TabsTrigger value="plan">{t('plan.tabPlan')}</TabsTrigger>
          <BookTabTrigger year={plan.year} month={plan.month} scope={scope} />
          <TabsTrigger value="flow">{t('plan.tabFlow')}</TabsTrigger>
        </TabsList>

        <TabsContent value="book">
          <PlanBook
            year={plan.year}
            month={plan.month}
            positions={positions}
            scope={scope}
            readOnly={!rights.addBooking}
            notSharedWith={member && ownerName ? ownerName : undefined}
          />
        </TabsContent>

        <TabsContent value="flow" className="flex flex-col gap-4">
          {/* Only in the own plan. Somebody else's carry-over asks their accounts
              grant, not the book (decision 69): offering it here would need
              accounts edit to set or change it and accounts delete to remove it. */}
          {scope.kind === 'own' && <OwnCarryOver year={plan.year} month={plan.month} />}
          <MonthFlow
            year={plan.year}
            month={plan.month}
            {...flowScope}
            ownerName={member ? ownerName : null}
          />
        </TabsContent>

        <TabsContent value="plan" className="flex flex-col gap-8">
          {household && positions.length === 0 && (
            <Empty className="border-border rounded-xl border border-dashed">
              <EmptyHeader>
                <EmptyTitle>{t('plan.nothingShared')}</EmptyTitle>
                <EmptyDescription>{t('plan.nothingSharedHint')}</EmptyDescription>
              </EmptyHeader>
            </Empty>
          )}

          {/* Zuerst das Bild, dann die Listen: „wohin geht es" beantwortet die
              Frage, mit der man sich hinsetzt. Die Posten darunter sind das
              Werkzeug, um daran zu drehen. */}
          {/* Paper version of the charts.
           *
           *  **Permanently mounted**, only parked outside the picture. It used to be
           *  narrowed on clicking Print — anyone using Ctrl+P bypassed that, and the
           *  charts then went onto the paper at screen width and were cut off.
           *
           *  Absolutely positioned rather than `hidden`: `display: none` would give a
           *  width of 0, and Recharts then draws nothing. This way it measures 672px
           *  once — A4 minus the margins — and keeps it.
           */}
          <div
            aria-hidden
            className="pointer-events-none absolute -left-[9999px] top-0 w-[672px] print:static print:left-auto print:flex print:flex-col print:gap-4"
          >
            <MonthFlow
              year={plan.year}
              month={plan.month}
              {...flowScope}
              ownerName={member ? ownerName : null}
              height="h-32"
              print
            />
            <PlanSankey
              positions={positions}
              distributable={plan.distributable}
              height="h-56"
              threshold={0.05}
            />
          </div>

          <div className="print:hidden">
            <PlanSankey positions={positions} distributable={plan.distributable} />
          </div>

          {/* Auf Papier ersetzt `PlanPrintout` diese Liste — dort trägt jede Zeile
              Abzeichen und einen Haken zum Klicken, und aus 26 Posten würden drei
              Seiten statt einer. */}
          <div className="flex flex-col gap-8 print:hidden">
            {/* Einnahmen zuerst — sie sind die Grundlage für alles darunter.
                target={null}, weil es für Einnahmen keine Quote gibt. */}
            <BudgetSection
              budget="income"
              unplanned={Number(plan.unplanned.income)}
              onOpenUnplanned={onOpenUnplanned}
              target={null}
              positions={incomeRows}
              hints={plan.hints}
              onEdit={openEditor}
              onTogglePaid={toggle}
              readOnly={tickReadOnly}
              ownerName={rowOwner}
            />

            {groups.map((group) => (
              <BudgetSection
                key={group.budget}
                budget={group.budget}
                unplanned={Number(plan.unplanned[group.budget])}
                onOpenUnplanned={onOpenUnplanned}
                target={group.target}
                positions={group.rows}
                hints={plan.hints}
                onEdit={openEditor}
                onTogglePaid={toggle}
                readOnly={tickReadOnly}
                ownerName={rowOwner}
              />
            ))}
          </div>
        </TabsContent>
      </Tabs>

      {/* Seite 2 des Ausdrucks. `ownerName` schaltet die Spalte „Wer" ein —
          beim gemeinsamen Plan ist genau das die Information. */}
      <PlanPrintout plan={plan} ownerName={rowOwner} />

      {paidDialogs}

      {/* Löschen nur mit dem Recht dazu: ändern steht im Protokoll und lässt sich
          zurücknehmen, löschen tut beides nicht. Der Endpunkt prüft es ohnehin noch
          einmal. Im Haushaltsplan bleibt `planId` leer: `useSavePosition` verwirft
          es bei einem PATCH ohnehin, und dieser Dialog legt dort nie neu an. */}
      <PositionDialog
        position={editing}
        budget={editing?.budget ?? addingTo}
        planId={planId}
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        pending={savePosition.isPending}
        error={savePosition.error}
        onSave={(position) =>
          savePosition.mutate(
            { ...position, planId },
            { onSuccess: () => setDialogOpen(false) }
          )
        }
        onDelete={
          editing && rights.deletePosition(editing)
            ? (position) => deletePosition(position)
            : null
        }
        readOnly={editing !== null && !rights.editPosition(editing)}
        ownerName={dialogOwner}
      />
    </>
  )
}
