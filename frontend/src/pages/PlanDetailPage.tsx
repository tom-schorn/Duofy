import { useEffect, useState } from 'react'
import { Trans, useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router'
import { ArrowLeft, Eye, Pencil, Plus, Printer, Trash2, Users } from 'lucide-react'

import { useActiveMember } from '@/hooks/use-active-member'
import { AddBookingButton } from '@/components/AddBookingButton'
import { BookMetrics } from '@/components/BookMetrics'
import { EmptyState } from '@/components/EmptyState'
import { MonthBook, type BookFilter } from '@/components/MonthBook'
import { BudgetSection } from '@/components/BudgetSection'
import { PaidDialog } from '@/components/PaidDialog'
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
import { Metric } from '@/components/Metric'
import { MonthSwitch } from '@/components/MonthSwitch'
import { MonthHints } from '@/components/MonthHints'
import { PlanPrintout } from '@/components/PlanPrintout'
import { PlanSankey } from '@/components/PlanSankey'
import { CreatePlanDialog } from '@/components/CreatePlanDialog'
import { longDate, parseMonth, today } from '@/lib/dates'
import { NotFoundBody } from '@/pages/NotFoundPage'
import { CarryOverCard } from '@/components/CarryOverCard'
import { MonthFlow } from '@/components/MonthFlow'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { PositionDialog } from '@/components/PositionDialog'
import { ApiError, errorText } from '@/lib/api'
import { positionHasBookings } from '@/lib/paid'
import {
  Empty,
  EmptyDescription,
  EmptyHeader,
  EmptyTitle,
} from '@/components/ui/empty'
import { QueryState } from '@/components/QueryState'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import {
  useAccounts,
  useDeletePlan,
  useDeletePosition,
  useHouseholdPlan,
  useHouseholds,
  useMe,
  useTransactions,
  usePlan,
  useSavePosition,
  useTogglePaid,
} from '@/lib/queries'
import {
  BUDGETS,
  monthLabel,
  QUOTA_KEY,
  euro,
  isPaid,
  stillDue,
  type AccessLevel,
  type Budget,
  atLeast,
  OWN_SCOPE,
  type HouseholdPlanDetail,
  type HouseholdPosition,
  type BookScope,
  type Member,
  type PlanDetail,
  type PlanPosition,
} from '@/lib/domain'

/**
 * One monthly plan in detail — the heart of the app.
 *
 * The flow follows the ritual: expect the income, distribute it across the three
 * budgets, check whether it works out, confirm.
 *
 * The quotas are **guidelines**, not rules. There is a target, the actual figure
 * stands next to it, and one decides whether that is acceptable.
 */
const TABS = new Set(['plan', 'book', 'flow'])

/**
 * An invalid month in the address (`/plan/2026/13`, `/plan/abc/x`) is the not-found
 * page; a valid one without a plan offers to create exactly that month.
 */
export function PlanDetailPage() {
  const { year, month } = useParams()
  const parsed = parseMonth(year, month)
  // The key resets the page when the address moves to another month — otherwise
  // the create dialog would keep offering the month it was first opened for.
  return parsed === null ? (
    <NotFoundBody />
  ) : (
    <PlanMonthPage
      key={`${parsed.year}-${parsed.month}`}
      year={parsed.year}
      month={parsed.month}
    />
  )
}

function PlanMonthPage({ year, month }: { year: number; month: number }) {
  const { t } = useTranslation()
  const [creating, setCreating] = useState(false)
  // The household lives in the URL, not in a global switcher. That makes the
  // shared view a place one can link to and reload — and it is visible why the page
  // looks different.
  const [params, setParams] = useSearchParams()
  const navigate = useNavigate()
  const householdId = params.get('household')
  // `?member=` shows the plan of a person who granted insight. Same reasoning as
  // for the household: a place in the URL, not global state.
  const memberId = params.get('member')
  const shared = householdId !== null
  const foreign = memberId !== null

  // All three hooks are always present — React does not allow conditional hooks.
  // The unused ones are switched off through `enabled` and load nothing.
  const ownPlan = usePlan(year, month, !shared && !foreign)
  // Name and level come from the member list the sidebar already loaded — the plan
  // itself says nothing about whose it is, and it does not have to.
  const active = useActiveMember()
  const householdPlan = useHouseholdPlan(
    householdId,
    year,
    month
  )
  const memberPlan = usePlan(year, month, foreign, memberId)
  const query = shared ? householdPlan : foreign ? memberPlan : ownPlan

  const households = useHouseholds()
  const deletePlan = useDeletePlan()
  // Set by `onHide`/`onRestore` below — the plan has no row of its own to hide
  // the way a list entry does, so the missing-month state stands in for it
  // (decisions 21/22: leaves at once, undo brings it back without a request).
  const [deletingMonth, setDeletingMonth] = useState(false)
  // A month nobody has created yet: not a failure, an invitation. Only for a
  // person's own plan — a household plan is composed, never created.
  const missing =
    !shared &&
    (deletingMonth ||
      (query.error instanceof ApiError && query.error.code === 'plan_not_found'))
  const mayCreate = atLeast(active.levelFor('plan'), 'edit')
  // A household month exists only once every current member has planned it.
  // Until then this is a half plan, not the household's: the backend sends
  // empty positions and names who is still missing instead, so a calm notice
  // replaces the numbers.
  const missingMembers = shared ? (householdPlan.data?.missingMembers ?? []) : []

  function handleDeleteMonth() {
    deletePlan({
      year,
      month,
      ownerId: foreign ? memberId : null,
      onHide: () => setDeletingMonth(true),
      onRestore: () => setDeletingMonth(false),
    })
  }

  // replace: switching tabs must not fill the back button with intermediate steps.
  const setTab = (value: string) =>
    setParams(
      (current: URLSearchParams) => {
        current.set('tab', value)
        return current
      },
      { replace: true }
    )

  // The "Ungeplant" rows lead into the book, already filtered to what hangs on no
  // position (#240). Pushed, not replaced: this is a step to go back from.
  const openUnplanned = () =>
    setParams((current: URLSearchParams) => {
      current.set('tab', 'book')
      current.set('filter', 'unplanned')
      return current
    })

  return (
    <div className="flex flex-col gap-8">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <Link
          // The overview must open in the same scope: the household's months for
          // the household plan, the other person's months for their plan — a bare
          // `/plan` would silently drop back to your own.
          to={{
            pathname: '/plan',
            search: shared
              ? `?household=${householdId}`
              : foreign
                ? `?member=${memberId}`
                : '',
          }}
          data-print="hide"
          className="text-muted-foreground hover:text-foreground flex w-fit items-center gap-1.5 text-sm"
        >
          <ArrowLeft className="size-4" />
          {t('plan.allPlans')}
        </Link>
        {/* Household, member and tab stay in the address: only the month moves. */}
        <MonthSwitch
          year={year}
          month={month}
          onChange={(nextYear, nextMonth) =>
            navigate({
              pathname: `/plan/${nextYear}/${String(nextMonth).padStart(2, '0')}`,
              search: params.toString() === '' ? '' : `?${params.toString()}`,
            })
          }
        />
      </div>

      {missing ? (
        <Empty>
          <EmptyHeader>
            <EmptyTitle>{t('plan.missingTitle', { month: `${monthLabel(month)} ${year}` })}</EmptyTitle>
            <EmptyDescription>
              {mayCreate ? t('plan.missingText') : t('plan.missingNoRight')}
            </EmptyDescription>
          </EmptyHeader>
          {mayCreate && (
            <Button onClick={() => setCreating(true)}>
              <Plus className="size-4" />
              {t('plans.create')}
            </Button>
          )}
        </Empty>
      ) : missingMembers.length > 0 ? (
        <Empty>
          <EmptyHeader>
            <EmptyTitle>{t('plan.incompleteTitle')}</EmptyTitle>
            <EmptyDescription>
              {t(
                missingMembers.length === 1 ? 'plan.incompleteOne' : 'plan.incompleteMany',
                {
                  names: missingMembers.join(` ${t('common.and')} `),
                  month: `${monthLabel(month)} ${year}`,
                }
              )}
            </EmptyDescription>
          </EmptyHeader>
        </Empty>
      ) : (
      <QueryState
        isPending={query.isPending}
        error={query.error}
        onRetry={() => void query.refetch()}
        rows={4}
        notShared={
          foreign && active.member
            ? t('plan.notShared', { name: active.member.firstName })
            : undefined
        }
      >
        {shared
          ? householdPlan.data && (
              <HouseholdPlanBody
                plan={householdPlan.data}
                members={
                  (households.data ?? []).find(
                    (household) => household.id === householdId
                  )?.members ?? []
                }
                tab={TABS.has(params.get('tab') ?? '') ? params.get('tab')! : 'plan'}
                onTab={setTab}
                onOpenUnplanned={openUnplanned}
              />
            )
          : foreign
            ? memberPlan.data && (
                <MemberPlanBody
                  plan={memberPlan.data}
                  ownerId={memberId ?? ''}
                  ownerName={active.member?.firstName ?? ''}
                  mayEdit={atLeast(active.levelFor('plan'), 'edit')}
                  mayDelete={atLeast(active.levelFor('plan'), 'delete')}
                  mayBook={atLeast(active.levelFor('accounts'), 'edit')}
                  tab={TABS.has(params.get('tab') ?? '') ? params.get('tab')! : 'plan'}
                  onTab={setTab}
                  onOpenUnplanned={openUnplanned}
                  onDeleteMonth={handleDeleteMonth}
                />
              )
            : ownPlan.data && (
              <PlanBody
                plan={ownPlan.data}
                onOpenUnplanned={openUnplanned}
                onDeleteMonth={handleDeleteMonth}
              />
            )}
      </QueryState>
      )}

      <CreatePlanDialog
        open={creating}
        onOpenChange={setCreating}
        ownerId={active.id}
        ownerName={active.member?.firstName ?? null}
        year={year}
        month={month}
      />
    </div>
  )
}

/**
 * The book's tab, with how many bookings the month holds — "Buch · 7". A carry-over
 * is a balance, not a booking, so it is not counted. Where the book cannot be read
 * (no grant yet, still loading) it is just "Buch".
 */
function BookTabTrigger({
  year,
  month,
  scope = OWN_SCOPE,
}: {
  year: number
  month: number
  scope?: BookScope
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
 * The book tab of the plan page (#241): the bookings of this plan month.
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
  scope = OWN_SCOPE,
  readOnly = false,
  notSharedWith,
}: {
  year: number
  month: number
  positions: PlanPosition[]
  scope?: BookScope
  readOnly?: boolean
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

function PlanBody({
  plan,
  onOpenUnplanned,
  onDeleteMonth,
}: {
  plan: PlanDetail
  onOpenUnplanned: () => void
  onDeleteMonth: () => void
}) {
  const { t } = useTranslation()
  const savePosition = useSavePosition()
  const deletePosition = useDeletePosition()
  const togglePaid = useTogglePaid()
  // The position whose booking dialog is currently open.
  const [booking, setBooking] = useState<PlanPosition | null>(null)

  // The tab lives in the URL: otherwise every reload lands back in the plan even
  // though one was just working in the book. A link to the flow of a month stays
  // shareable that way.
  //
  // The values are English while the labels are German: the interface will be
  // translated later and a URL should stay stable through that.
  const [params, setParams] = useSearchParams()
  const tab = TABS.has(params.get('tab') ?? '') ? params.get('tab')! : 'plan'
  const setTab = (value: string) =>
    setParams(
      (current: URLSearchParams) => {
        current.set('tab', value)
        return current
      },
      { replace: true }
    )

  const [editing, setEditing] = useState<PlanPosition | null>(null)
  const [addingTo, setAddingTo] = useState<Budget>('wants')
  const [dialogOpen, setDialogOpen] = useState(false)
  // An error from the last try must not greet the next opening.
  const resetSaveposition = savePosition.reset
  useEffect(() => {
    if (dialogOpen) resetSaveposition()
  }, [dialogOpen, resetSaveposition])

  // For the confirmation when un-ticking: which booking hangs off which position.
  const transactions = useTransactions(plan.year, plan.month)

  // Where the curve starts: the carry-over of the default account, if it has one.
  const accounts = useAccounts()
  const defaultAccount = accounts.data?.find(
    (account) => account.active && account.isDefault
  )
  const carryOver = transactions.data?.find(
    (entry) =>
      entry.kind === 'carry_over' && entry.accountId === defaultAccount?.id
  )

  /** The position whose self-created booking is about to disappear. */
  const [confirming, setConfirming] = useState<PlanPosition | null>(null)

  const autoBookedOf = (position: PlanPosition) =>
    transactions.data?.find(
      (entry) => entry.positionId === position.id && entry.autoBooked
    )

  /**
   * Ticking and un-ticking are not symmetric:
   *
   * Ticking quietly creates a booking — unless there is no account, in which case a
   * hint follows. Un-ticking **removes** the booking again, and that is a loss of
   * data one wants to know about.
   */
  function togglePaidWithGuard(position: PlanPosition) {
    const paid = isPaid(position)

    if (paid) {
      if (autoBookedOf(position)) {
        setConfirming(position)
        return
      }
      togglePaid.mutate({ id: position.id, paid: false })
      return
    }

    // Always ask, even for a position that already has bookings: the dialog says
    // that date and amount are not used then, and shows a rejected tick in place.
    setBooking(position)
  }

  const groups = BUDGETS.map((budget) => {
    const key = budget as keyof typeof QUOTA_KEY
    return {
      budget,
      rows: plan.positions.filter((row) => row.budget === budget),
      quota: Number(plan[QUOTA_KEY[key]]),
      target: Number(plan.distributable) * (Number(plan[QUOTA_KEY[key]]) / 100),
    }
  })

  // Income deliberately sits outside `groups`: it has no quota and must not flow
  // into `allocated`, otherwise the remainder would be wrong.
  const incomeRows = plan.positions.filter((row) => row.budget === 'income')

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
  const unpaid = plan.positions.reduce((sum, row) => sum + stillDue(row), 0)

  // How many positions run into which household. The count is in the badge because
  // a bare name would read like ownership — the plan is yours, only part of it is
  // shared.

  function handleAdd(budget: Budget) {
    setEditing(null)
    setAddingTo(budget)
    setDialogOpen(true)
  }

  return (
    <>
      {/* Nur auf Papier: ohne Topbar fehlte sonst jeder Hinweis, was das Blatt
          ist und von wann es stammt. */}
      <p className="text-muted-foreground hidden text-xs print:block">
        {t('plan.printHeader', {
          month: monthLabel(plan.month),
          year: plan.year,
          date: longDate(today()),
        })}
      </p>

      <header className="flex flex-wrap items-end justify-between gap-4">
        {/* `min-w-0 flex-1`: ohne das nimmt sich der Textblock die volle Breite
            und schiebt die Knopfgruppe auf eine eigene Zeile — dort steht sie
            dann links statt rechts oben. */}
        <div className="flex min-w-0 flex-1 flex-col gap-2">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="font-heading text-3xl font-semibold">
              {monthLabel(plan.month)} {plan.year}
            </h1>
          </div>
          <p className="text-muted-foreground print:hidden">
            {t('plan.lead')}
          </p>
        </div>
        {/* Eigene Gruppe: der Kopf hat `justify-between` und genau zwei
            Kinder. Ein dritter Knopf direkt daneben landete in der Mitte,
            statt rechts bei den anderen zu bleiben. */}
        <div
          className="flex shrink-0 flex-wrap items-center gap-2"
          data-print="hide"
        >
          {/* Drucken wechselt vorher auf den Plan: gedruckt wird, was im DOM
              steht, und bei offenem Buch-Reiter wäre das das Buch. */}
          <Button
            variant="outline"
            onClick={() => {
              // Only switch the tab: what gets printed is what is in the DOM. The
              // print version of the charts is permanently mounted, so Ctrl+P works
              // without this button too.
              setTab('plan')
              requestAnimationFrame(() => window.print())
            }}
          >
            <Printer className="size-4" />
            {t('plan.print')}
          </Button>
          <AddBookingButton
            year={plan.year}
            month={plan.month}
            positions={plan.positions}
          />
          <Button onClick={() => handleAdd('wants')}>
            <Plus className="size-4" />
            {t('positionDialog.addTitle')}
          </Button>
          <Button
            variant="ghost"
            className="text-destructive"
            disabled={!plan.deletable}
            onClick={onDeleteMonth}
          >
            <Trash2 className="size-4" />
            {t('plan.deleteMonth')}
          </Button>
        </div>
      </header>

      {/* Nur sichtbar, solange der Knopf oben deaktiviert ist — dieselbe
          Erklärung wie im Fehlerfall (409), nur schon vorher gesagt. */}
      {!plan.deletable && (
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
          positions={plan.positions}
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
          als die Postenliste — „geht der Monat auf" gegen „was steht drin".
          Später kommt „Buch" als dritter Tab dazu. */}
      <Tabs
        value={tab}
        onValueChange={(value) =>
          // replace rather than push: switching tabs must not fill the back button
          // with intermediate steps.
          setParams(
            (current: URLSearchParams) => {
              current.set('tab', value)
              return current
            },
            { replace: true }
          )
        }
        className="gap-6"
      >
        <TabsList data-print="hide">
          <TabsTrigger value="plan">{t('plan.tabPlan')}</TabsTrigger>
          <BookTabTrigger year={plan.year} month={plan.month} />
          <TabsTrigger value="flow">{t('plan.tabFlow')}</TabsTrigger>
        </TabsList>

        <TabsContent value="book">

          <PlanBook

                    year={plan.year}

                    month={plan.month}

                    positions={plan.positions}

                  />

        </TabsContent>


        <TabsContent value="flow" className="flex flex-col gap-4">
          {defaultAccount && (
            <CarryOverCard
              account={defaultAccount}
              carryOver={carryOver}
              year={plan.year}
              month={plan.month}
            />
          )}
          <MonthFlow
            year={plan.year}
            month={plan.month}
          />
        </TabsContent>

        <TabsContent value="plan" className="flex flex-col gap-8">
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
              height="h-32"
              print
            />
            <PlanSankey
              positions={plan.positions}
              distributable={plan.distributable}
              height="h-56"
              threshold={0.05}
            />
          </div>

          <div className="print:hidden">
            <PlanSankey positions={plan.positions} distributable={plan.distributable} />
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
          onEdit={(position) => {
            setEditing(position)
            setDialogOpen(true)
          }}
          onTogglePaid={togglePaidWithGuard}
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
              onEdit={(position) => {
              setEditing(position)
              setDialogOpen(true)
            }}
            onTogglePaid={togglePaidWithGuard}
          />
        ))}
      </div>
        </TabsContent>
      </Tabs>

      <PlanPrintout plan={plan} />

      {/* Enthaken entfernt die vom Haken erzeugte Buchung. Der Betrag steht
          in der Frage, damit man sieht, was verloren geht — falls er nach dem
          Abhaken von Hand korrigiert wurde. */}
      <PaidDialog
        position={booking}
        onClose={() => {
          setBooking(null)
          togglePaid.reset()
        }}
        onConfirm={({ occurredOn, amount }) => {
          if (booking) {
            togglePaid.mutate(
              {
                id: booking.id,
                paid: true,
                occurredOn,
                amount,
                inlineError: true,
                hasBookings: positionHasBookings(booking.id, transactions.data),
              },
              { onSuccess: () => setBooking(null) }
            )
          }
        }}
        pending={togglePaid.isPending}
        planMonth={{ year: plan.year, month: plan.month }}
        hasBookings={
          booking ? positionHasBookings(booking.id, transactions.data) : false
        }
        error={togglePaid.isError ? errorText(togglePaid.error) : null}
      />

      <AlertDialog
        open={confirming !== null}
        onOpenChange={(open) => !open && setConfirming(null)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('plan.untickTitle')}</AlertDialogTitle>
            <AlertDialogDescription>
              {confirming && (
                <>
                  <Trans
                    i18nKey="plan.untickText"
                    values={{
                      amount: euro.format(
                        Number(autoBookedOf(confirming)?.amount ?? 0)
                      ),
                    }}
                    components={{
                      amount: (
                        <span className="text-foreground font-mono font-medium" />
                      ),
                    }}
                  />
                </>
              )}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t('common.cancel')}</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                if (confirming) {
                  togglePaid.mutate({ id: confirming.id, paid: false })
                }
                setConfirming(null)
              }}
            >
              {t('plan.untick')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <PositionDialog
        position={editing}
        budget={editing?.budget ?? addingTo}
        planId={plan.id}
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        pending={savePosition.isPending}
        error={savePosition.error}
        onSave={(position) =>
          savePosition.mutate(
            { ...position, planId: plan.id },
            { onSuccess: () => setDialogOpen(false) }
          )
        }
        onDelete={(position) => deletePosition(position)}
      />
    </>
  )
}

/**
 * The shared plan — composed, not stored.
 *
 * Two things are therefore missing that the own plan has: creating a month and
 * confirming it. There is no object to create or confirm — the view arises from the
 * positions of every member.
 *
 * Changes are made in the own plan. This page only says who carries what.
 */
/**
 * Another person plan — insight only.
 *
 * Not the same as the shared plan: that one merges every member and shows positions
 * with a household only. Here one person stands alone, private positions included.
 * It answers "how is this person doing", not "does the household carry the month".
 */
function MemberPlanBody({
  plan,
  ownerId,
  ownerName,
  mayEdit,
  mayDelete,
  mayBook,
  tab,
  onTab,
  onOpenUnplanned,
  onDeleteMonth,
}: {
  plan: PlanDetail
  ownerId: string
  ownerName: string
  /** Only decides whether buttons are offered. The endpoint checks it again. */
  mayEdit: boolean
  /** A step above `mayEdit`: deleting is neither logged nor reversible. */
  mayDelete: boolean
  /** The accounts grant reaches `edit`: adding and changing bookings is offered. */
  mayBook: boolean
  tab: string
  onTab: (value: string) => void
  onOpenUnplanned: () => void
  onDeleteMonth: () => void
}) {
  const { t } = useTranslation()
  const groups = BUDGETS.map((budget) => {
    const key = budget as keyof typeof QUOTA_KEY
    return {
      budget,
      rows: plan.positions.filter((row) => row.budget === budget),
      target: Number(plan.distributable) * (Number(plan[QUOTA_KEY[key]]) / 100),
    }
  })

  const incomeRows = plan.positions.filter((row) => row.budget === 'income')

  const allocated = groups.reduce(
    (total, group) =>
      total +
      group.rows.reduce(
        (sum, row) => (row.passThrough ? sum : sum + Number(row.amountPlanned)),
        0
      ),
    0
  )
  const free = Number(plan.distributable) - allocated
  const unpaid = plan.positions.reduce((sum, row) => sum + stillDue(row), 0)

  // Acting on their behalf: at level `edit` everything the owner can do except
  // deleting. Adding used to be excluded on the grounds that a new position is a
  // decision, not a correction — but somebody who helps plan runs into a missing
  // position immediately, and sending them away at that point makes the whole
  // delegation useless. Deleting stays out: changing is logged and reversible,
  // deleting is neither.
  const scope: BookScope = { kind: 'member', ownerId }

  const togglePaid = useTogglePaid()
  const savePosition = useSavePosition()
  const deletePosition = useDeletePosition()
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

  // No date dialog: that belongs to the owner. Acting on their behalf means
  // ticking off what was due, with the planned amount and today.
  const toggle = (position: PlanPosition) =>
    togglePaid.mutate({ id: position.id, paid: !isPaid(position) })

  return (
    <>
      <header className="flex flex-col gap-2">
        <div className="flex flex-wrap items-center gap-2">
          <h1 className="font-heading text-3xl font-semibold">
            {monthLabel(plan.month)} {plan.year}
          </h1>
          <Badge variant="secondary" className="gap-1 font-normal">
            <Eye className="size-3" />
            {ownerName}
          </Badge>
          {mayEdit && (
            <Badge variant="outline" className="gap-1 font-normal">
              <Pencil className="size-3" />
              {t('plan.standIn')}
            </Badge>
          )}
          <span className="ml-auto flex items-center gap-2">
            <AddBookingButton
              year={plan.year}
              month={plan.month}
              positions={plan.positions}
              scope={scope}
              readOnly={!mayBook}
            />
            {mayEdit && (
              <Button size="sm" onClick={() => handleAdd('wants')}>
                <Plus className="size-4" />
                {t('positionDialog.addTitle')}
              </Button>
            )}
          </span>
          {mayDelete && (
            <Button
              size="sm"
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
        {mayDelete && !plan.deletable && (
          <p className="text-muted-foreground text-sm">
            {t('errors.plan_has_transactions')}
          </p>
        )}
        <p className="text-muted-foreground">
          {t('plan.memberLead', { name: ownerName })}{' '}
          {!mayEdit
            ? t('plan.viewOnly', { name: ownerName })
            : mayDelete
              ? t('plan.mayDelete')
              : t('plan.mayEdit')}
        </p>
      </header>

      {tab === 'book' ? (
        <BookMetrics
          year={plan.year}
          month={plan.month}
          positions={plan.positions}
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

      <Tabs value={tab} onValueChange={onTab} className="gap-6">
        <TabsList>
          <TabsTrigger value="plan">{t('plan.tabPlan')}</TabsTrigger>
          <BookTabTrigger year={plan.year} month={plan.month} scope={scope} />
          <TabsTrigger value="flow">{t('plan.tabFlow')}</TabsTrigger>
        </TabsList>

        <TabsContent value="book">

          <PlanBook

                    year={plan.year}

                    month={plan.month}

                    positions={plan.positions}

                    scope={scope}

                    readOnly={!mayBook}

                    notSharedWith={ownerName}

                  />

        </TabsContent>


        <TabsContent value="flow">
          <MonthFlow
            year={plan.year}
            month={plan.month}
            ownerId={ownerId}
            ownerName={ownerName}
          />
        </TabsContent>

        <TabsContent value="plan">
          <div className="flex flex-col gap-8">
            <BudgetSection
              budget="income"
              unplanned={Number(plan.unplanned.income)}
              onOpenUnplanned={onOpenUnplanned}
              target={null}
              positions={incomeRows}
              hints={plan.hints}
                  onEdit={openEditor}
              onTogglePaid={toggle}
              readOnly={!mayEdit}
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
                readOnly={!mayEdit}
              />
            ))}
          </div>
        </TabsContent>
      </Tabs>

      {/* Löschen nur ab der Stufe `delete`: ändern steht im Protokoll und lässt
          sich zurücknehmen, löschen tut beides nicht. Der Endpunkt prüft es
          ohnehin noch einmal. */}
      <PositionDialog
        position={editing}
        budget={editing?.budget ?? addingTo}
        planId={plan.id}
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        pending={savePosition.isPending}
        error={savePosition.error}
        onSave={(position) =>
          savePosition.mutate(
            { ...position, planId: plan.id },
            { onSuccess: () => setDialogOpen(false) }
          )
        }
        onDelete={mayDelete ? (position) => deletePosition(position) : null}
        readOnly={!mayEdit}
        ownerName={ownerName}
      />
    </>
  )
}

function HouseholdPlanBody({
  plan,
  members,
  tab,
  onTab,
  onOpenUnplanned,
}: {
  plan: HouseholdPlanDetail
  /** For the notice shown when somebody does not share their figures. */
  members: Member[]
  tab: string
  onTab: (value: string) => void
  onOpenUnplanned: () => void
}) {
  const { t } = useTranslation()
  const groups = BUDGETS.map((budget) => {
    const key = budget as keyof typeof QUOTA_KEY
    return {
      budget,
      rows: plan.positions.filter((row) => row.budget === budget),
      target: Number(plan.distributable) * (Number(plan[QUOTA_KEY[key]]) / 100),
    }
  })

  const incomeRows = plan.positions.filter((row) => row.budget === 'income')

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

  const unpaid = plan.positions.reduce((sum, row) => sum + stillDue(row), 0)

  // Sits in the row behind the category, for example "Rent · 1st · Mia".
  const ownerName = (position: PlanPosition) =>
    (position as HouseholdPosition).ownerName ?? null

  const noPositions = plan.positions.length === 0

  const scope: BookScope = { kind: 'household', householdId: plan.householdId }

  // #218: a position is one's own, or somebody else's shared into the household.
  // Own positions behave exactly like the private plan (rule 4 of the UI
  // guideline). Somebody else's only open with their own grant — set on their
  // membership, never by the viewer — at `edit`; below that the row has no
  // control at all (rule 3: a missing right removes the control, not just
  // disables it).
  const me = useMe()
  const myId = me.data?.id ?? null
  const levelOf = (ownerId: string): AccessLevel =>
    members.find((member) => member.userId === ownerId)?.grantsPlan ?? 'plan'
  const mayEdit = (position: HouseholdPosition) =>
    position.ownerId === myId || atLeast(levelOf(position.ownerId), 'edit')
  const mayDelete = (position: HouseholdPosition) =>
    position.ownerId === myId || atLeast(levelOf(position.ownerId), 'delete')

  const savePosition = useSavePosition()
  const deletePosition = useDeletePosition()
  const togglePaid = useTogglePaid()
  const [editing, setEditing] = useState<PlanPosition | null>(null)
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

  // Ticking somebody else's position acts on their behalf, exactly as in the
  // member's own plan (#180): no booking-date question, ticked with today and
  // the planned amount.
  const toggle = (position: PlanPosition) =>
    togglePaid.mutate({ id: position.id, paid: !isPaid(position) })

  return (
    <>
      {/* Nur auf Papier: ohne Topbar fehlte jeder Hinweis, wessen Haushalt das
          Blatt zeigt und von wann es stammt. */}
      <p className="text-muted-foreground hidden text-xs print:block">
        {t('plan.printHeaderHousehold', {
          household: plan.householdName,
          month: monthLabel(plan.month),
          year: plan.year,
          date: longDate(today()),
        })}
      </p>

      <header className="flex flex-col gap-2">
        <div className="flex flex-wrap items-center gap-2">
          <h1 className="font-heading text-3xl font-semibold">
            {monthLabel(plan.month)} {plan.year}
          </h1>
          <Badge variant="secondary" className="gap-1 font-normal">
            <Users className="size-3" />
            {plan.householdName}
          </Badge>
        </div>
        <p className="text-muted-foreground print:hidden">
          {t('plan.householdLead')}
        </p>
      </header>

      <div className="flex justify-end" data-print="hide">
        <Button
          variant="outline"
          onClick={() => {
            onTab('plan')
            requestAnimationFrame(() => window.print())
          }}
        >
          <Printer className="size-4" />
          {t('plan.print')}
        </Button>
      </div>

      {tab === 'book' ? (
        <BookMetrics
          year={plan.year}
          month={plan.month}
          positions={plan.positions}
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
          <Metric
            label={t('plans.open')}
            value={unpaid}
            hint={t('plan.notPaid')}
          />
        </section>
      )}
      {tab !== 'book' && <MonthHints hints={plan.hints} />}

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
            positions={plan.positions}
            scope={scope}
            readOnly
          />
        </TabsContent>


        <TabsContent value="flow">
          <MonthFlow
            year={plan.year}
            month={plan.month}
            householdId={plan.householdId}
          />
        </TabsContent>

        <TabsContent value="plan" className="flex flex-col gap-8">
          {noPositions && (
            <Empty className="border-border rounded-xl border border-dashed">
              <EmptyHeader>
                <EmptyTitle>{t('plan.nothingShared')}</EmptyTitle>
                <EmptyDescription>{t('plan.nothingSharedHint')}</EmptyDescription>
              </EmptyHeader>
            </Empty>
          )}
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
              householdId={plan.householdId}
              height="h-32"
              print
            />
            <PlanSankey
              positions={plan.positions}
              distributable={plan.distributable}
              height="h-56"
              threshold={0.05}
            />
          </div>

          <div className="print:hidden">
            <PlanSankey positions={plan.positions} distributable={plan.distributable} />
          </div>

          {/* Auf Papier ersetzt `PlanPrintout` diese Liste. */}
          <div className="flex flex-col gap-8 print:hidden">
            <BudgetSection
              budget="income"
              unplanned={Number(plan.unplanned.income)}
              onOpenUnplanned={onOpenUnplanned}
              target={null}
              positions={incomeRows}
              hints={plan.hints}
              onEdit={openEditor}
              onTogglePaid={toggle}
              readOnly={(position) => !mayEdit(position as HouseholdPosition)}
              ownerName={ownerName}
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
                readOnly={(position) => !mayEdit(position as HouseholdPosition)}
                ownerName={ownerName}
              />
            ))}
          </div>
        </TabsContent>
      </Tabs>

      {/* Seite 2 des Ausdrucks. `ownerName` schaltet die Spalte „Wer" ein —
          beim gemeinsamen Plan ist genau das die Information. */}
      <PlanPrintout plan={plan} ownerName={ownerName} />


      {/* Kein „Anlegen" hier: der Haushalt besitzt nichts, ein Posten entsteht
          immer im eigenen Plan (#218 Nicht im Umfang). `planId` bleibt leer —
          `useSavePosition` verwirft es bei einem PATCH ohnehin, und dieser
          Dialog legt nie neu an. */}
      <PositionDialog
        position={editing}
        budget={editing?.budget ?? 'wants'}
        planId=""
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        pending={savePosition.isPending}
        error={savePosition.error}
        onSave={(position) =>
          savePosition.mutate(
            { ...position, planId: '' },
            { onSuccess: () => setDialogOpen(false) }
          )
        }
        onDelete={
          editing && mayDelete(editing as HouseholdPosition)
            ? (position) => deletePosition(position)
            : null
        }
        readOnly={editing !== null && !mayEdit(editing as HouseholdPosition)}
        ownerName={
          editing && (editing as HouseholdPosition).ownerId !== myId
            ? (editing as HouseholdPosition).ownerName
            : null
        }
      />
    </>
  )
}

