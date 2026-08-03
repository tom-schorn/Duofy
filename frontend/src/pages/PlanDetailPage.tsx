import { useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router'
import { ArrowLeft, Eye, Pencil, Plus, Printer, Users } from 'lucide-react'

import { AccountCards } from '@/components/AccountCards'
import { BookFlow } from '@/components/BookFlow'
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
import { MonthBook } from '@/components/MonthBook'
import { Metric } from '@/components/Metric'
import { PlanPrintout } from '@/components/PlanPrintout'
import { PlanSankey } from '@/components/PlanSankey'
import { langesDatum, today } from '@/lib/dates'
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
  useConfirmPlan,
  useDeletePosition,
  useAccounts,
  useHouseholdPlan,
  useMemberPlan,
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
  stillDue,
  type Block,
  type HouseholdPlanDetail,
  type HouseholdPosition,
  type BookScope,
  type Member,
  type MemberPlanDetail,
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
  const [params, setParams] = useSearchParams()
  const householdId = params.get('household')
  // `?member=` zeigt den Plan einer Person, die Einblick gegeben hat. Dieselbe
  // Begründung wie beim Haushalt: ein Ort in der URL, kein globaler Zustand.
  const memberId = params.get('member')
  const shared = householdId !== null
  const foreign = memberId !== null

  // Alle drei Hooks stehen immer da — React erlaubt keine bedingten Hooks. Die
  // ungenutzten sind über `enabled` abgeschaltet und laden nichts.
  const ownPlan = usePlan(Number(year), Number(month), !shared && !foreign)
  const householdPlan = useHouseholdPlan(
    householdId,
    Number(year),
    Number(month)
  )
  const memberPlan = useMemberPlan(memberId, Number(year), Number(month))
  const query = shared ? householdPlan : foreign ? memberPlan : ownPlan

  const households = useHouseholds()

  const names = Object.fromEntries(
    (households.data ?? []).map((household) => [household.id, household.name])
  )

  // replace: der Reiterwechsel soll den Zurück-Knopf nicht mit
  // Zwischenschritten volllaufen lassen.
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
                tab={params.get('tab') ?? 'plan'}
                onTab={setTab}
              />
            )
          : foreign
            ? memberPlan.data && (
                <MemberPlanBody
                  plan={memberPlan.data}
                  householdNames={names}
                  tab={params.get('tab') ?? 'plan'}
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
  // Der Posten, für den gerade das Buchungsfenster offen ist.
  const [booking, setBooking] = useState<PlanPosition | null>(null)
  /**
   * Verengt das Diagramm auf Papierbreite, kurz bevor gedruckt wird.
   *
   * Recharts zeichnet ein SVG in der Größe, die es beim Messen vorgefunden hat
   * — am Bildschirm 936 px. Auf A4 stehen 703 px zur Verfügung, und beim
   * Drucken misst Recharts nicht neu: sein `ResizeObserver` läuft asynchron und
   * kommt nach dem Druckbild. Ergebnis war ein abgeschnittenes oder gar nicht
   * gezeichnetes Diagramm.
   *
   * Deshalb passiert die Verengung **vorher und im laufenden Betrieb**, wo das
   * Nachmessen zuverlässig greift.
   */
  const [druckbreite, setDruckbreite] = useState(false)
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
      total +
      group.rows.reduce(
        // Durchlaufendes ist nie Budget gewesen — weder oben in `plan.budget`
        // noch hier. Zöge man es nur hier ab, wäre „Verplanbar" zu groß.
        (sum, row) => (row.passThrough ? sum : sum + Number(row.amountPlanned)),
        0
      ),
    0
  )
  const free = Number(plan.budget) - allocated

  // „Noch offen" = was diesen Monat noch bezahlt werden muss. Schon erfasste
  // Teilbeträge sind abgezogen, siehe `stillDue`.
  const unpaid = plan.positions.reduce((sum, row) => sum + stillDue(row), 0)

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
      {/* Nur auf Papier: ohne Topbar fehlte sonst jeder Hinweis, was das Blatt
          ist und von wann es stammt. */}
      <p className="text-muted-foreground hidden text-xs print:block">
        Duofy · Monatsplan {MONTH_LABEL[plan.month - 1]} {plan.year} · gedruckt
        am {langesDatum(today())}
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
            onClick={async () => {
              setTab('plan')
              setDruckbreite(true)
              // Zwei Frames: einer für Reiter und Breite, einer damit Recharts
              // nachgemessen und neu gezeichnet hat.
              await new Promise((fertig) =>
                requestAnimationFrame(() => requestAnimationFrame(fertig))
              )
              // Zurückgesetzt wird bei `afterprint`, nicht direkt nach dem
              // Aufruf: `window.print()` blockiert nicht in jedem Browser, und
              // dann wäre das Diagramm schon wieder breit, bevor das Druckbild
              // entsteht.
              const zurueck = () => {
                setDruckbreite(false)
                window.removeEventListener('afterprint', zurueck)
              }
              window.addEventListener('afterprint', zurueck)
              window.print()
              // Notausgang, falls der Browser kein `afterprint` schickt.
              setTimeout(zurueck, 5000)
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
        <TabsList data-print="hide">
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

          <BookFlow year={plan.year} month={plan.month} />

          <MonthBook
            positions={plan.positions}
            year={plan.year}
            month={plan.month}
          />
        </TabsContent>

        <TabsContent value="plan" className="flex flex-col gap-8">
          {/* Zuerst das Bild, dann die Listen: „wohin geht es" beantwortet die
              Frage, mit der man sich hinsetzt. Die Posten darunter sind das
              Werkzeug, um daran zu drehen. */}
          {/* 672 px ist die A4-Breite abzüglich der Ränder — dieselbe Zahl wie
              das `min-w-[42rem]` des Diagramms. */}
          <div className={druckbreite ? 'w-[672px]' : undefined}>
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

      <footer
        data-print="hide"
        className="flex flex-wrap items-center justify-between gap-4 border-t pt-6"
      >
        <p className="text-muted-foreground text-sm">
          {plan.status === 'draft'
            ? 'Entwurf — noch nicht bestätigt.'
            : 'Bestätigt. Der Monat läuft.'}
        </p>
        {/* TODO: Im Haushalt müssen beide bestätigen. Dafür fehlt im Backend
            eine Tabelle, die je Mitglied festhält wer zugestimmt hat. */}
        <Button
          data-print="hide"
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
/**
 * Der Plan einer anderen Person — reiner Einblick.
 *
 * Nicht dasselbe wie der gemeinsame Plan: der fasst alle Mitglieder zusammen
 * und zeigt nur Posten mit Haushalt. Hier steht eine Person für sich, samt
 * ihrer privaten Posten. Beantwortet „wie steht Jasmin da", nicht „tragen wir
 * den Monat".
 *
 * Read-only, auch bei Stufe „darf ändern" — das Ändern kommt als eigener
 * Schritt, und bis dahin wäre ein Knopf, der 403 liefert, schlimmer als keiner.
 */
function MemberPlanBody({
  plan,
  householdNames,
  tab,
  onTab,
}: {
  plan: MemberPlanDetail
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

  // Vertretung: abhaken und ändern, wenn die Stufe es hergibt. Neu **anlegen**
  // bleibt aus, siehe `canAdd` in BudgetSection.
  const scope: BookScope = { kind: 'member', ownerId: plan.ownerId }

  const togglePaid = useTogglePaid()
  const savePosition = useSavePosition()
  const [editing, setEditing] = useState<PlanPosition | null>(null)
  const [dialogOpen, setDialogOpen] = useState(false)

  function openEditor(position: PlanPosition) {
    setEditing(position)
    setDialogOpen(true)
  }

  // Ohne Datumsfenster: das gehört dem Besitzer. Als Vertretung hakt man ab,
  // was fällig war, mit dem geplanten Betrag von heute.
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
            {plan.ownerName}
          </Badge>
          {plan.mayEdit && (
            <Badge variant="outline" className="gap-1 font-normal">
              <Pencil className="size-3" />
              Vertretung
            </Badge>
          )}
          <Badge variant={plan.status === 'draft' ? 'outline' : 'secondary'}>
            {PLAN_STATUS_LABEL[plan.status]}
          </Badge>
        </div>
        <p className="text-muted-foreground">
          {plan.ownerName}s ganzer Monat, auch die privaten Posten — so
          freigegeben.{' '}
          {plan.mayEdit
            ? 'Du darfst abhaken und ändern; jede Änderung wird protokolliert.'
            : 'Nur zum Ansehen.'}
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
          <AccountCards scope={scope} />
          <BookFlow year={plan.year} month={plan.month} scope={scope} />
          <MonthBook
            positions={plan.positions}
            year={plan.year}
            month={plan.month}
            scope={scope}
            readOnly
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
              onAdd={() => {}}
              onTogglePaid={toggle}
              readOnly={!plan.mayEdit}
              canAdd={false}
            />

            {groups.map((group) => (
              <BudgetSection
                key={group.block}
                block={group.block}
                target={group.target}
                positions={group.rows}
                householdNames={householdNames}
                onEdit={openEditor}
                onAdd={() => {}}
                onTogglePaid={toggle}
                readOnly={!plan.mayEdit}
                canAdd={false}
              />
            ))}
          </div>
        </TabsContent>
      </Tabs>

      {/* Kein Löschen als Vertretung: ändern ist protokolliert und umkehrbar,
          löschen ist beides nicht. Der Endpunkt lehnt es ohnehin ab. */}
      <PositionDialog
        position={editing}
        block={editing?.block ?? 'needs'}
        planId={plan.id}
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        onSave={(position) =>
          savePosition.mutate(
            { ...position, planId: plan.id },
            { onSuccess: () => setDialogOpen(false) }
          )
        }
        onDelete={null}
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
  /** Für den Hinweis, wenn jemand seine Zahlen nicht teilt. */
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
        // Durchlaufendes ist nie Budget gewesen — weder oben in `plan.budget`
        // noch hier. Zöge man es nur hier ab, wäre „Verplanbar" zu groß.
        (sum, row) => (row.passThrough ? sum : sum + Number(row.amountPlanned)),
        0
      ),
    0
  )
  const free = Number(plan.budget) - allocated

  const unpaid = plan.positions.reduce((sum, row) => sum + stillDue(row), 0)

  // Steht in der Zeile hinter der Kategorie: „Miete · 1. · Jasmin".
  const ownerName = (position: PlanPosition) =>
    (position as HouseholdPosition).ownerName ?? null

  const noPositions = plan.positions.length === 0

  const scope: BookScope = { kind: 'household', householdId: plan.householdId }
  // Wer nur die gemeinsamen Posten teilt, fehlt in allen Buch-Summen.
  const stillPrivate = members
    .filter((member) => member.grantsAccess === 'plan')
    .map((member) => member.firstName)

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
              <AccountCards scope={scope} />
              <BookFlow year={plan.year} month={plan.month} scope={scope} />
              <MonthBook
                positions={plan.positions}
                year={plan.year}
                month={plan.month}
                scope={scope}
                readOnly
              />
            </TabsContent>

            <TabsContent value="plan" className="flex flex-col gap-8">
              <PlanSankey positions={plan.positions} budget={plan.budget} />

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
            </TabsContent>
          </Tabs>
        </>
      )}
    </>
  )
}

