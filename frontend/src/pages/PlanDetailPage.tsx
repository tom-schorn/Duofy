import { useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router'
import { ArrowLeft, Eye, Pencil, Plus, Printer, Users } from 'lucide-react'

import { useActiveMember } from '@/hooks/use-active-member'
import { BookMetrics } from '@/components/BookMetrics'
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
import { PlanPrintout } from '@/components/PlanPrintout'
import { PlanSankey } from '@/components/PlanSankey'
import { longDate, today } from '@/lib/dates'
import { MonthFlow } from '@/components/MonthFlow'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { PositionDialog } from '@/components/PositionDialog'
import {
  Alert,
  AlertAction,
  AlertDescription,
  AlertTitle,
} from '@/components/ui/alert'
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
  useDeletePosition,
  useAccounts,
  useHouseholdPlan,
  useHouseholds,
  useTransactions,
  usePlan,
  useSavePosition,
  useTogglePaid,
} from '@/lib/queries'
import {
  BUDGETS,
  MONTH_LABEL,
  QUOTA_KEY,
  euro,
  isPaid,
  stillDue,
  type Block,
  atLeast,
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
 * The flow follows the ritual: expect the income, subtract the buffer, distribute
 * the rest across the three blocks, check whether it works out, confirm.
 *
 * The quotas are **guidelines**, not rules. There is a target, the actual figure
 * stands next to it, and one decides whether that is acceptable.
 */
const TABS = new Set(['plan', 'flow'])

export function PlanDetailPage() {
  const { year, month } = useParams()
  // The household lives in the URL, not in a global switcher. That makes the
  // shared view a place one can link to and reload — and it is visible why the page
  // looks different.
  const [params, setParams] = useSearchParams()
  const householdId = params.get('household')
  // `?member=` shows the plan of a person who granted insight. Same reasoning as
  // for the household: a place in the URL, not global state.
  const memberId = params.get('member')
  const shared = householdId !== null
  const foreign = memberId !== null

  // All three hooks are always present — React does not allow conditional hooks.
  // The unused ones are switched off through `enabled` and load nothing.
  const ownPlan = usePlan(Number(year), Number(month), !shared && !foreign)
  // Name and level come from the member list the sidebar already loaded — the plan
  // itself says nothing about whose it is, and it does not have to.
  const active = useActiveMember()
  const householdPlan = useHouseholdPlan(
    householdId,
    Number(year),
    Number(month)
  )
  const memberPlan = usePlan(Number(year), Number(month), foreign, memberId)
  const query = shared ? householdPlan : foreign ? memberPlan : ownPlan

  const households = useHouseholds()

  const names = Object.fromEntries(
    (households.data ?? []).map((household) => [household.id, household.name])
  )

  // replace: switching tabs must not fill the back button with intermediate steps.
  const setTab = (value: string) =>
    setParams(
      (current: URLSearchParams) => {
        current.set('tab', value)
        return current
      },
      { replace: true }
    )

  return (
    <div className="flex flex-col gap-8">
      <Link
        to="/plan"
        data-print="hide"
        className="text-muted-foreground hover:text-foreground flex w-fit items-center gap-1.5 text-sm"
      >
        <ArrowLeft className="size-4" />
        Alle Pläne
      </Link>

      <QueryState isPending={query.isPending} error={query.error} rows={4}>
        {shared
          ? householdPlan.data && (
              <HouseholdPlanBody
                plan={householdPlan.data}
                householdNames={names}
                members={
                  (households.data ?? []).find(
                    (household) => household.id === householdId
                  )?.members ?? []
                }
                tab={TABS.has(params.get('tab') ?? '') ? params.get('tab')! : 'plan'}
                onTab={setTab}
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
                  householdNames={names}
                  tab={TABS.has(params.get('tab') ?? '') ? params.get('tab')! : 'plan'}
                  onTab={setTab}
                />
              )
            : ownPlan.data && (
              <PlanBody plan={ownPlan.data} householdNames={names} />
            )}
      </QueryState>
    </div>
  )
}

function PlanBody({
  plan,
  householdNames,
}: {
  plan: PlanDetail
  householdNames: Record<string, string>
}) {
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
  // `book` war einmal ein Reiter und ist jetzt eine eigene Seite. Alte Links
  // und Lesezeichen zeigen sonst auf einen Reiter ohne Inhalt.
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
  const [addingTo, setAddingTo] = useState<Block>('wants')
  const [dialogOpen, setDialogOpen] = useState(false)

  // For the confirmation when un-ticking: which booking hangs off which position.
  const transactions = useTransactions(plan.year, plan.month)
  const accounts = useAccounts().data ?? []
  const hasDefaultAccount = accounts.some(
    (account) => account.active && account.isDefault
  )

  /** The position whose self-created booking is about to disappear. */
  const [confirming, setConfirming] = useState<PlanPosition | null>(null)
  /** Last ticked off without a booking being possible. */
  const [noAccountFor, setNoAccountFor] = useState<string | null>(null)

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

    if (!position.isBudget && !position.accountId && !hasDefaultAccount) {
      setNoAccountFor(position.label)
      togglePaid.mutate({ id: position.id, paid: true })
      return
    }

    // If an actual amount is already there, bookings exist — then the tick adds
    // nothing and there is nothing to ask. Otherwise ask for date and amount,
    // because both go into the book exactly as entered.
    if (position.amountActual !== null) {
      togglePaid.mutate({ id: position.id, paid: true })
      return
    }

    setBooking(position)
  }

  const groups = BUDGETS.map((block) => {
    const key = block as keyof typeof QUOTA_KEY
    return {
      block,
      rows: plan.positions.filter((row) => row.block === block),
      quota: Number(plan[QUOTA_KEY[key]]),
      target: Number(plan.budget) * (Number(plan[QUOTA_KEY[key]]) / 100),
    }
  })

  // Income deliberately sits outside `groups`: it has no quota and must not flow
  // into `allocated`, otherwise the remaining budget would be wrong.
  const incomeRows = plan.positions.filter((row) => row.block === 'income')

  // What is left to allocate is the free remainder of the budget, not the budget.
  const allocated = groups.reduce(
    (total, group) =>
      total +
      group.rows.reduce(
        // Pass-through money was never budget — neither in `plan.budget` above nor
        // here. Subtracting it only here would make the remainder too large.
        (sum, row) => (row.passThrough ? sum : sum + Number(row.amountPlanned)),
        0
      ),
    0
  )
  const free = Number(plan.budget) - allocated

  // What is still open is what has to be paid this month. Partial amounts already
  // recorded are subtracted, see `stillDue`.
  const unpaid = plan.positions.reduce((sum, row) => sum + stillDue(row), 0)

  // How many positions run into which household. The count is in the badge because
  // a bare name would read like ownership — the plan is yours, only part of it is
  // shared.
  const householdCounts = plan.positions.reduce<Record<string, number>>(
    (counts, row) =>
      row.householdId === null
        ? counts
        : { ...counts, [row.householdId]: (counts[row.householdId] ?? 0) + 1 },
    {}
  )

  function handleAdd(block: Block) {
    setEditing(null)
    setAddingTo(block)
    setDialogOpen(true)
  }

  return (
    <>
      {/* Nur auf Papier: ohne Topbar fehlte sonst jeder Hinweis, was das Blatt
          ist und von wann es stammt. */}
      <p className="text-muted-foreground hidden text-xs print:block">
        Duofy · Monatsplan {MONTH_LABEL[plan.month - 1]} {plan.year} · gedruckt
        am {longDate(today())}
      </p>

      <header className="flex flex-wrap items-end justify-between gap-4">
        {/* `min-w-0 flex-1`: ohne das nimmt sich der Textblock die volle Breite
            und schiebt die Knopfgruppe auf eine eigene Zeile — dort steht sie
            dann links statt rechts oben. */}
        <div className="flex min-w-0 flex-1 flex-col gap-2">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="font-heading text-3xl font-semibold">
              {MONTH_LABEL[plan.month - 1]} {plan.year}
            </h1>
            {Object.entries(householdCounts).map(([id, count]) => (
              <Badge key={id} variant="secondary" className="gap-1 font-normal">
                <Users className="size-3" />
                {count} Posten aus {householdNames[id] ?? 'Haushalt'}
              </Badge>
            ))}
          </div>
          <p className="text-muted-foreground print:hidden">
            Verplane den Monat, bevor er anfängt. Die Quoten sind Richtwerte —
            es zählt, dass es aufgeht.
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
            Drucken
          </Button>
          <Button onClick={() => handleAdd('wants')}>
            <Plus className="size-4" />
            Posten hinzufügen
          </Button>
        </div>
      </header>

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
          <Metric label="Einnahmen" value={Number(plan.income)} />
          <Metric
            label="Verplanbar"
            value={free}
            hint="noch nicht verteilt"
            strong
            tone={free < 0 ? 'over' : 'neutral'}
          />
          <Metric label="Noch offen" value={unpaid} hint="noch nicht bezahlt" />
        </section>
      )}

      {noAccountFor && (
        <Alert>
          <AlertTitle>Abgehakt, aber nichts gebucht</AlertTitle>
          <AlertDescription>
            <span className="font-medium">{noAccountFor}</span> ist erledigt —
            es fehlt aber ein Standardkonto, auf das die Buchung gehen könnte.{' '}
            <Link to="/accounts" className="underline underline-offset-4">
              Konto anlegen
            </Link>
          </AlertDescription>
          <AlertAction>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setNoAccountFor(null)}
            >
              Verstanden
            </Button>
          </AlertAction>
        </Alert>
      )}

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
          <TabsTrigger value="plan">Plan</TabsTrigger>
          <TabsTrigger value="flow">Verlauf</TabsTrigger>
        </TabsList>

        <TabsContent value="flow">
          <MonthFlow
            positions={plan.positions}
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
              positions={plan.positions}
              year={plan.year}
              month={plan.month}
              height="h-32"
            />
            <PlanSankey
              positions={plan.positions}
              budget={plan.budget}
              height="h-56"
              threshold={0.05}
            />
          </div>

          <div className="print:hidden">
            <PlanSankey positions={plan.positions} budget={plan.budget} />
          </div>

      {/* Auf Papier ersetzt `PlanPrintout` diese Liste — dort trägt jede Zeile
          Abzeichen und einen Haken zum Klicken, und aus 26 Posten würden drei
          Seiten statt einer. */}
      <div className="flex flex-col gap-8 print:hidden">
        {/* Einnahmen zuerst — sie sind die Grundlage für alles darunter.
            target={null}, weil es für Einnahmen keine Quote gibt. */}
        <BudgetSection
          block="income"
          target={null}
          positions={incomeRows}
          householdNames={householdNames}
          onEdit={(position) => {
            setEditing(position)
            setDialogOpen(true)
          }}
          onAdd={handleAdd}
          onTogglePaid={togglePaidWithGuard}
        />

        {groups.map((group) => (
          <BudgetSection
            key={group.block}
            block={group.block}
            target={group.target}
            positions={group.rows}
            householdNames={householdNames}
            onEdit={(position) => {
              setEditing(position)
              setDialogOpen(true)
            }}
            onAdd={handleAdd}
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
        onClose={() => setBooking(null)}
        onConfirm={({ occurredOn, amount }) => {
          if (booking) {
            togglePaid.mutate({ id: booking.id, paid: true, occurredOn, amount })
          }
          setBooking(null)
        }}
        pending={togglePaid.isPending}
      />

      <AlertDialog
        open={confirming !== null}
        onOpenChange={(open) => !open && setConfirming(null)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Buchung mit entfernen?</AlertDialogTitle>
            <AlertDialogDescription>
              {confirming && (
                <>
                  Das nimmt die Buchung über{' '}
                  <span className="text-foreground font-mono font-medium">
                    {euro.format(
                      Number(autoBookedOf(confirming)?.amount ?? 0)
                    )}
                  </span>{' '}
                  aus dem Haushaltsbuch. Von Hand erfasste Buchungen an diesem
                  Posten bleiben stehen.
                </>
              )}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Abbrechen</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                if (confirming) {
                  togglePaid.mutate({ id: confirming.id, paid: false })
                }
                setConfirming(null)
              }}
            >
              Haken wegnehmen
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <PositionDialog
        position={editing}
        block={editing?.block ?? addingTo}
        planId={plan.id}
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        onSave={(position) => savePosition.mutate({ ...position, planId: plan.id })}
        onDelete={(position) => deletePosition.mutate(position.id)}
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
  householdNames,
  tab,
  onTab,
}: {
  plan: PlanDetail
  ownerId: string
  ownerName: string
  /** Only decides whether buttons are offered. The endpoint checks it again. */
  mayEdit: boolean
  /** A step above `mayEdit`: deleting is neither logged nor reversible. */
  mayDelete: boolean
  householdNames: Record<string, string>
  tab: string
  onTab: (value: string) => void
}) {
  const groups = BUDGETS.map((block) => {
    const key = block as keyof typeof QUOTA_KEY
    return {
      block,
      rows: plan.positions.filter((row) => row.block === block),
      target: Number(plan.budget) * (Number(plan[QUOTA_KEY[key]]) / 100),
    }
  })

  const incomeRows = plan.positions.filter((row) => row.block === 'income')

  const allocated = groups.reduce(
    (total, group) =>
      total +
      group.rows.reduce(
        (sum, row) => (row.passThrough ? sum : sum + Number(row.amountPlanned)),
        0
      ),
    0
  )
  const free = Number(plan.budget) - allocated
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
  const [addingTo, setAddingTo] = useState<Block>('wants')
  const [dialogOpen, setDialogOpen] = useState(false)

  function openEditor(position: PlanPosition) {
    setEditing(position)
    setDialogOpen(true)
  }

  function handleAdd(block: Block) {
    setEditing(null)
    setAddingTo(block)
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
            {MONTH_LABEL[plan.month - 1]} {plan.year}
          </h1>
          <Badge variant="secondary" className="gap-1 font-normal">
            <Eye className="size-3" />
            {ownerName}
          </Badge>
          {mayEdit && (
            <Badge variant="outline" className="gap-1 font-normal">
              <Pencil className="size-3" />
              Vertretung
            </Badge>
          )}
          {mayEdit && (
            <Button size="sm" className="ml-auto" onClick={() => handleAdd('wants')}>
              <Plus className="size-4" />
              Posten hinzufügen
            </Button>
          )}
        </div>
        <p className="text-muted-foreground">
          {ownerName}s ganzer Monat, auch die privaten Posten — so
          freigegeben.{' '}
          {!mayEdit
            ? 'Nur zum Ansehen.'
            : mayDelete
              ? 'Du darfst abhaken, ändern, dazuschreiben und löschen; jede Änderung wird protokolliert.'
              : 'Du darfst abhaken, ändern und Posten dazuschreiben; jede Änderung wird protokolliert. Löschen bleibt beim Besitzer.'}
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
          <Metric label="Einnahmen" value={Number(plan.income)} />
          <Metric
            label="Verplanbar"
            value={free}
            hint="noch nicht verteilt"
            strong
            tone={free < 0 ? 'over' : 'neutral'}
          />
          <Metric label="Noch offen" value={unpaid} hint="noch nicht bezahlt" />
        </section>
      )}

      <Tabs value={tab} onValueChange={onTab} className="gap-6">
        <TabsList>
          <TabsTrigger value="plan">Plan</TabsTrigger>
          <TabsTrigger value="flow">Verlauf</TabsTrigger>
        </TabsList>

        <TabsContent value="flow">
          <MonthFlow
            positions={plan.positions}
            year={plan.year}
            month={plan.month}
          />
        </TabsContent>

        <TabsContent value="plan">
          <div className="flex flex-col gap-8">
            <BudgetSection
              block="income"
              target={null}
              positions={incomeRows}
              householdNames={householdNames}
              onEdit={openEditor}
              onAdd={handleAdd}
              onTogglePaid={toggle}
              readOnly={!mayEdit}
              canAdd={mayEdit}
            />

            {groups.map((group) => (
              <BudgetSection
                key={group.block}
                block={group.block}
                target={group.target}
                positions={group.rows}
                householdNames={householdNames}
                onEdit={openEditor}
                onAdd={handleAdd}
                onTogglePaid={toggle}
                readOnly={!mayEdit}
                canAdd={mayEdit}
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
        block={editing?.block ?? addingTo}
        planId={plan.id}
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        onSave={(position) =>
          savePosition.mutate(
            { ...position, planId: plan.id },
            { onSuccess: () => setDialogOpen(false) }
          )
        }
        onDelete={
          mayDelete && editing
            ? () => {
                deletePosition.mutate(editing.id)
                setDialogOpen(false)
              }
            : null
        }
      />
    </>
  )
}

function HouseholdPlanBody({
  plan,
  householdNames,
  members,
  tab,
  onTab,
}: {
  plan: HouseholdPlanDetail
  householdNames: Record<string, string>
  /** For the notice shown when somebody does not share their figures. */
  members: Member[]
  tab: string
  onTab: (value: string) => void
}) {
  const groups = BUDGETS.map((block) => {
    const key = block as keyof typeof QUOTA_KEY
    return {
      block,
      rows: plan.positions.filter((row) => row.block === block),
      target: Number(plan.budget) * (Number(plan[QUOTA_KEY[key]]) / 100),
    }
  })

  const incomeRows = plan.positions.filter((row) => row.block === 'income')

  const allocated = groups.reduce(
    (total, group) =>
      total +
      group.rows.reduce(
        // Pass-through money was never budget — neither in `plan.budget` above nor
        // here. Subtracting it only here would make the remainder too large.
        (sum, row) => (row.passThrough ? sum : sum + Number(row.amountPlanned)),
        0
      ),
    0
  )
  const free = Number(plan.budget) - allocated

  const unpaid = plan.positions.reduce((sum, row) => sum + stillDue(row), 0)

  // Sits in the row behind the category, for example "Rent · 1st · Mia".
  const ownerName = (position: PlanPosition) =>
    (position as HouseholdPosition).ownerName ?? null

  const noPositions = plan.positions.length === 0

  const scope: BookScope = { kind: 'household', householdId: plan.householdId }
  // Anybody sharing only the joint positions is missing from every book total.
  // The book hangs on the accounts grant, not on the plan one — somebody can show
  // their whole month and still keep their bookings to themselves.
  const stillPrivate = members
    .filter((member) => member.grantsAccounts === 'plan')
    .map((member) => member.firstName)

  return (
    <>
      {/* Nur auf Papier: ohne Topbar fehlte jeder Hinweis, wessen Haushalt das
          Blatt zeigt und von wann es stammt. */}
      <p className="text-muted-foreground hidden text-xs print:block">
        Duofy · Haushalt {plan.householdName} · {MONTH_LABEL[plan.month - 1]}{' '}
        {plan.year} · gedruckt am {longDate(today())}
      </p>

      <header className="flex flex-col gap-2">
        <div className="flex flex-wrap items-center gap-2">
          <h1 className="font-heading text-3xl font-semibold">
            {MONTH_LABEL[plan.month - 1]} {plan.year}
          </h1>
          <Badge variant="secondary" className="gap-1 font-normal">
            <Users className="size-3" />
            {plan.householdName}
          </Badge>
        </div>
        <p className="text-muted-foreground print:hidden">
          Alle Posten, die ihr gemeinsam tragt. Zusammengesetzt aus den Plänen
          aller Mitglieder — geändert wird im eigenen Plan.
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
          Drucken
        </Button>
      </div>

      {noPositions ? (
        <Empty className="border-border rounded-xl border border-dashed">
          <EmptyHeader>
            <EmptyTitle>Noch nichts Gemeinsames</EmptyTitle>
            <EmptyDescription>
              Setz im eigenen Plan bei einem Posten den Haushalt — dann taucht
              er hier auf.
            </EmptyDescription>
          </EmptyHeader>
        </Empty>
      ) : (
        <>
          {tab === 'book' ? (
            <BookMetrics
              year={plan.year}
              month={plan.month}
              positions={plan.positions}
              scope={scope}
            />
          ) : (
            <section className="grid gap-3 sm:grid-cols-3">
              <Metric label="Einnahmen" value={Number(plan.income)} />
              <Metric
                label="Verplanbar"
                value={free}
                hint="noch nicht verteilt"
                strong
                tone={free < 0 ? 'over' : 'neutral'}
              />
              <Metric
                label="Noch offen"
                value={unpaid}
                hint="noch nicht bezahlt"
              />
            </section>
          )}

          {/* Fehlt jemand, sind alle Summen im Buch unvollständig. Das muss
              dastehen — eine Zahl, der jemand fehlt, ohne dass man es sieht,
              wäre schlimmer als keine. */}
          {tab === 'book' && stillPrivate.length > 0 && (
            <p
              role="status"
              className="border-border bg-muted/40 rounded-lg border p-3 text-sm"
            >
              {stillPrivate.join(' und ')} teil
              {stillPrivate.length === 1 ? 't' : 'en'} noch keine Zahlen — die
              Summen unten sind unvollständig. Umstellen lässt sich das nur von{' '}
              {stillPrivate.length === 1 ? 'ihr oder ihm' : 'ihnen'} selbst,
              unter „Haushalt".
            </p>
          )}

          <Tabs value={tab} onValueChange={onTab} className="gap-6">
            <TabsList data-print="hide">
              <TabsTrigger value="plan">Plan</TabsTrigger>
              <TabsTrigger value="flow">Verlauf</TabsTrigger>
            </TabsList>

            <TabsContent value="flow">
              <MonthFlow
                positions={plan.positions}
                year={plan.year}
                month={plan.month}
                height="h-32"
              />
            </TabsContent>

            <TabsContent value="plan" className="flex flex-col gap-8">
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
                  positions={plan.positions}
                  year={plan.year}
                  month={plan.month}
                  height="h-32"
                />
                <PlanSankey
                  positions={plan.positions}
                  budget={plan.budget}
                  height="h-56"
                  threshold={0.05}
                />
              </div>

              <div className="print:hidden">
                <PlanSankey positions={plan.positions} budget={plan.budget} />
              </div>

              {/* Auf Papier ersetzt `PlanPrintout` diese Liste. */}
              <div className="flex flex-col gap-8 print:hidden">
                <BudgetSection
                  block="income"
                  target={null}
                  positions={incomeRows}
                  householdNames={householdNames}
                  onEdit={() => {}}
                  onAdd={() => {}}
                  onTogglePaid={() => {}}
                  readOnly
                  ownerName={ownerName}
                />

                {groups.map((group) => (
                  <BudgetSection
                    key={group.block}
                    block={group.block}
                    target={group.target}
                    positions={group.rows}
                    householdNames={householdNames}
                    onEdit={() => {}}
                    onAdd={() => {}}
                    onTogglePaid={() => {}}
                    readOnly
                    ownerName={ownerName}
                  />
                ))}
              </div>
            </TabsContent>
          </Tabs>

          {/* Seite 2 des Ausdrucks. `ownerName` schaltet die Spalte „Wer" ein —
              beim gemeinsamen Plan ist genau das die Information. */}
          <PlanPrintout plan={plan} ownerName={ownerName} />
        </>
      )}
    </>
  )
}

