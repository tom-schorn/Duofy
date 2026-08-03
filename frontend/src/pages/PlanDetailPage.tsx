import { useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router'
import { ArrowLeft, Plus, Users } from 'lucide-react'

import { AccountCards } from '@/components/AccountCards'
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
import { MonthBook } from '@/components/MonthBook'
import { MonthFlow } from '@/components/MonthFlow'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { PositionDialog } from '@/components/PositionDialog'
import { QueryState } from '@/components/QueryState'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import {
  useConfirmPlan,
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
  PLAN_STATUS_LABEL,
  QUOTA_KEY,
  euro,
  isPaid,
  type Block,
  type HouseholdPlanDetail,
  type HouseholdPosition,
  type PlanDetail,
  type PlanPosition,
} from '@/lib/domain'

/**
 * Ein Monatsplan im Detail — das Herzstück.
 *
 * Der Ablauf folgt dem Ritual: Einnahmen erwarten, Puffer abziehen, den Rest
 * auf die drei Budgets verteilen, prüfen ob es aufgeht, bestätigen.
 *
 * Die Quoten sind **Richtwerte**, keine Regel. Es gibt ein Soll, daneben steht
 * das Ist, und man schaut dass es passt.
 */
export function PlanDetailPage() {
  const { year, month } = useParams()
  // Der Haushalt steht in der URL, nicht in einem globalen Umschalter. Damit
  // ist die gemeinsame Sicht ein Ort, den man verlinken und neu laden kann —
  // und es ist sichtbar, warum die Seite anders aussieht.
  const [params] = useSearchParams()
  const householdId = params.get('household')
  const shared = householdId !== null

  // Beide Hooks stehen immer da — React erlaubt keine bedingten Hooks. Der
  // jeweils ungenutzte ist über `enabled` abgeschaltet und lädt nichts.
  const ownPlan = usePlan(Number(year), Number(month), !shared)
  const householdPlan = useHouseholdPlan(
    householdId,
    Number(year),
    Number(month)
  )
  const query = shared ? householdPlan : ownPlan

  const households = useHouseholds()

  const names = Object.fromEntries(
    (households.data ?? []).map((household) => [household.id, household.name])
  )

  return (
    <div className="flex flex-col gap-8">
      <Link
        to="/plan"
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
  // Der Posten, für den gerade das Buchungsfenster offen ist.
  const [booking, setBooking] = useState<PlanPosition | null>(null)
  const confirmPlan = useConfirmPlan()

  // Tab in der URL: sonst landet man nach jedem Neuladen wieder im Plan,
  // obwohl man gerade im Buch gearbeitet hat. Ein Link auf den Verlauf eines
  // Monats bleibt so teilbar.
  //
  // Die Werte sind englisch, die Beschriftung deutsch: die Oberfläche wird
  // später übersetzt, eine URL soll dabei stabil bleiben.
  // Tab in der URL: sonst landet man nach jedem Neuladen wieder im Plan,
  // obwohl man gerade im Buch gearbeitet hat. Ein Link auf den Verlauf eines
  // Monats bleibt so teilbar.
  //
  // Die Werte sind englisch, die Beschriftung deutsch: die Oberfläche wird
  // später übersetzt, eine URL soll dabei stabil bleiben.
  const [params, setParams] = useSearchParams()
  const tab = params.get('tab') ?? 'plan'

  const [editing, setEditing] = useState<PlanPosition | null>(null)
  const [addingTo, setAddingTo] = useState<Block>('wants')
  const [dialogOpen, setDialogOpen] = useState(false)

  // Für die Rückfrage beim Enthaken: welche Buchung hängt an welchem Posten.
  const transactions = useTransactions(plan.year, plan.month)
  const accounts = useAccounts().data ?? []
  const hasDefaultAccount = accounts.some(
    (account) => account.active && account.isDefault
  )

  /** Der Posten, dessen selbst erzeugte Buchung gleich verschwinden würde. */
  const [confirming, setConfirming] = useState<PlanPosition | null>(null)
  /** Zuletzt abgehakt, ohne dass gebucht werden konnte. */
  const [noAccountFor, setNoAccountFor] = useState<string | null>(null)

  const autoBookedOf = (position: PlanPosition) =>
    transactions.data?.find(
      (entry) => entry.positionId === position.id && entry.autoBooked
    )

  /**
   * Abhaken und Enthaken sind nicht symmetrisch:
   *
   * Abhaken legt still eine Buchung an — außer es gibt kein Konto, dann folgt
   * ein Hinweis. Enthaken **entfernt** die Buchung wieder, und das ist ein
   * Datenverlust, über den man Bescheid wissen will.
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

    // Steht schon ein Ist da, sind Buchungen erfasst — dann bucht der Haken
    // nichts dazu und es gibt nichts zu fragen. Sonst nach Datum und Betrag
    // fragen, denn beides wandert genau so ins Buch.
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

  // Einnahmen stehen bewusst außerhalb von `groups`: sie haben keine Quote und
  // dürfen nicht in `allocated` einfließen, sonst stimmt „Verplanbar" nicht.
  const incomeRows = plan.positions.filter((row) => row.block === 'income')

  // „Verplanbar" = was vom Budget noch frei ist, nicht das Budget selbst.
  const allocated = groups.reduce(
    (total, group) =>
      total + group.rows.reduce((sum, row) => sum + Number(row.amountPlanned), 0),
    0
  )
  const free = Number(plan.budget) - allocated

  // „Noch offen" = was diesen Monat noch bezahlt werden muss.
  const unpaid = plan.positions
    .filter((row) => row.block !== 'income' && !isPaid(row))
    .reduce((sum, row) => sum + Number(row.amountPlanned), 0)

  // Wie viele Posten in welchen Haushalt laufen. Die Zahl steht im Badge, weil
  // ein blanker Name wie ein Besitzverhältnis aussieht — der Plan gehört aber
  // dir, es ist nur ein Teil davon gemeinsam.
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
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-col gap-2">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="font-heading text-3xl font-semibold">
              {MONTH_LABEL[plan.month - 1]} {plan.year}
            </h1>
            <Badge variant={plan.status === 'draft' ? 'outline' : 'secondary'}>
              {PLAN_STATUS_LABEL[plan.status]}
            </Badge>
            {Object.entries(householdCounts).map(([id, count]) => (
              <Badge key={id} variant="secondary" className="gap-1 font-normal">
                <Users className="size-3" />
                {count} Posten aus {householdNames[id] ?? 'Haushalt'}
              </Badge>
            ))}
          </div>
          <p className="text-muted-foreground">
            Verplane den Monat, bevor er anfängt. Die Quoten sind Richtwerte —
            es zählt, dass es aufgeht.
          </p>
        </div>
        <Button onClick={() => handleAdd('wants')}>
          <Plus className="size-4" />
          Posten hinzufügen
        </Button>
      </header>

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

      {noAccountFor && (
        <p
          role="alert"
          className="border-border bg-muted/40 flex flex-wrap items-center gap-2 rounded-lg border p-3 text-sm"
        >
          <span>
            <span className="font-medium">{noAccountFor}</span> ist abgehakt,
            aber es wurde nichts gebucht — es fehlt ein Standardkonto.
          </span>
          <Link to="/accounts" className="underline underline-offset-4">
            Konto anlegen
          </Link>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="ml-auto"
            onClick={() => setNoAccountFor(null)}
          >
            Verstanden
          </Button>
        </p>
      )}

      {/* Tabs statt Untereinander: der Verlauf beantwortet eine andere Frage
          als die Postenliste — „geht der Monat auf" gegen „was steht drin".
          Später kommt „Buch" als dritter Tab dazu. */}
      <Tabs
        value={tab}
        onValueChange={(value) =>
          // replace statt push: der Tabwechsel soll den Zurück-Knopf nicht
          // mit Zwischenschritten volllaufen lassen.
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
        <TabsList>
          <TabsTrigger value="plan">Plan</TabsTrigger>
          <TabsTrigger value="flow">Verlauf</TabsTrigger>
          <TabsTrigger value="book">Buch</TabsTrigger>
        </TabsList>

        <TabsContent value="flow">
          <MonthFlow
            positions={plan.positions}
            year={plan.year}
            month={plan.month}
          />
        </TabsContent>

        <TabsContent value="book" className="flex flex-col gap-6">
          {/* Die Kontostände gehören zum Buch, nicht zum Plan: sie sagen, was
              wirklich da ist. Unter den Plan-Karten, damit man beides in
              einem Blick hat. */}
          <AccountCards />

          <MonthBook
            positions={plan.positions}
            year={plan.year}
            month={plan.month}
          />
        </TabsContent>

        <TabsContent value="plan">
      <div className="flex flex-col gap-8">
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

      <footer className="flex flex-wrap items-center justify-between gap-4 border-t pt-6">
        <p className="text-muted-foreground text-sm">
          {plan.status === 'draft'
            ? 'Entwurf — noch nicht bestätigt.'
            : 'Bestätigt. Der Monat läuft.'}
        </p>
        {/* TODO: Im Haushalt müssen beide bestätigen. Dafür fehlt im Backend
            eine Tabelle, die je Mitglied festhält wer zugestimmt hat. */}
        <Button
          onClick={() => confirmPlan.mutate(plan.id)}
          disabled={plan.status === 'confirmed' || confirmPlan.isPending}
        >
          Monat bestätigen
        </Button>
      </footer>

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
 * Der gemeinsame Plan — zusammengesetzt, nicht gespeichert.
 *
 * Deshalb fehlen hier zwei Dinge, die es im eigenen Plan gibt: „Monat anlegen"
 * und „Monat bestätigen". Es existiert kein Objekt, das man anlegen oder
 * bestätigen könnte — die Ansicht entsteht aus den Posten aller Mitglieder.
 *
 * Geändert wird deshalb im eigenen Plan. Hier steht nur, wer was trägt.
 */
function HouseholdPlanBody({
  plan,
  householdNames,
}: {
  plan: HouseholdPlanDetail
  householdNames: Record<string, string>
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
      total + group.rows.reduce((sum, row) => sum + Number(row.amountPlanned), 0),
    0
  )
  const free = Number(plan.budget) - allocated

  const unpaid = plan.positions
    .filter((row) => row.block !== 'income' && !isPaid(row))
    .reduce((sum, row) => sum + Number(row.amountPlanned), 0)

  // Steht in der Zeile hinter der Kategorie: „Miete · 1. · Jasmin".
  const ownerName = (position: PlanPosition) =>
    (position as HouseholdPosition).ownerName ?? null

  const noPositions = plan.positions.length === 0

  return (
    <>
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
        <p className="text-muted-foreground">
          Alle Posten, die ihr gemeinsam tragt. Zusammengesetzt aus den Plänen
          aller Mitglieder — geändert wird im eigenen Plan.
        </p>
      </header>

      {noPositions ? (
        <p className="text-muted-foreground bg-card border-border rounded-lg border p-6 text-sm">
          Für diesen Monat ist noch nichts als gemeinsam markiert. Setz im
          eigenen Plan bei einem Posten den Haushalt — dann taucht er hier auf.
        </p>
      ) : (
        <>
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

          <div className="flex flex-col gap-8">
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
        </>
      )}
    </>
  )
}

function Metric({
  label,
  value,
  hint,
  strong = false,
  tone = 'neutral',
}: {
  label: string
  value: number
  hint?: string
  strong?: boolean
  tone?: 'neutral' | 'over'
}) {
  return (
    <div className="bg-card border-border flex flex-col gap-1 rounded-lg border p-4">
      <span className="text-muted-foreground text-[11px] font-semibold tracking-widest uppercase">
        {label}
      </span>
      <span
        className={`font-mono tabular-nums ${strong ? 'text-xl font-semibold' : 'text-lg font-medium'} ${
          tone === 'over' ? 'text-destructive' : ''
        }`}
      >
        {euro.format(value)}
      </span>
      {hint && <span className="text-muted-foreground text-xs">{hint}</span>}
    </div>
  )
}
