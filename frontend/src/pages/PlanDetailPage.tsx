import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router'
import { ArrowLeft, Plus } from 'lucide-react'

import { useActiveMember } from '@/hooks/use-active-member'
import { MonthSwitch } from '@/components/MonthSwitch'
import { CreatePlanDialog } from '@/components/CreatePlanDialog'
import { PlanView } from '@/components/PlanView'
import { parseMonth } from '@/lib/dates'
import { OWN_RIGHTS, PLAN_TABS, type PlanRights } from '@/lib/plan-rights'
import { NotFoundBody } from '@/pages/NotFoundPage'
import { ApiError } from '@/lib/api'
import {
  Empty,
  EmptyDescription,
  EmptyHeader,
  EmptyTitle,
} from '@/components/ui/empty'
import { QueryState } from '@/components/QueryState'
import { Button } from '@/components/ui/button'
import {
  useDeletePlan,
  useHouseholdPlan,
  useHouseholds,
  useMe,
  usePlan,
} from '@/lib/queries'
import {
  atLeast,
  monthLabel,
  OWN_SCOPE,
  type AccessLevel,
  type BookScope,
  type HouseholdPlanDetail,
  type HouseholdPosition,
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

  const tab = PLAN_TABS.has(params.get('tab') ?? '') ? params.get('tab')! : 'plan'

  // The one view behind all three plans: where the data comes from and what the
  // viewer may do with it. Everything else is `PlanView` (#251).
  const me = useMe()
  const myId = me.data?.id ?? null
  const householdMembers =
    (households.data ?? []).find((household) => household.id === householdId)?.members ?? []
  let view: {
    plan: PlanDetail | HouseholdPlanDetail
    scope: BookScope
    rights: PlanRights
    lead: string
    standIn: boolean
  } | null = null
  if (shared && householdPlan.data) {
    // #218: a position is one's own, or somebody else's shared into the household.
    // Somebody else's only open with their own grant — set on their membership,
    // never by the viewer — at `edit`; below that the row has no control at all.
    const levelOf = (ownerId: string): AccessLevel =>
      householdMembers.find((member) => member.userId === ownerId)?.grantsPlan ?? 'plan'
    const ownerOf = (position: PlanPosition) => (position as HouseholdPosition).ownerId
    view = {
      plan: householdPlan.data,
      scope: { kind: 'household', householdId: householdPlan.data.householdId },
      rights: {
        // The household owns nothing: a position or booking always starts in the
        // own plan (#218 Nicht im Umfang), and it has no month to delete.
        addPosition: false,
        addBooking: false,
        deleteMonth: false,
        editPosition: (position) =>
          ownerOf(position) === myId || atLeast(levelOf(ownerOf(position)), 'edit'),
        deletePosition: (position) =>
          ownerOf(position) === myId || atLeast(levelOf(ownerOf(position)), 'delete'),
      },
      lead: t('plan.householdLead'),
      standIn: false,
    }
  } else if (foreign && memberPlan.data) {
    // Acting on their behalf: at level `edit` everything the owner can do except
    // deleting. Only decides which controls are offered; the endpoint checks again.
    const name = active.member?.firstName ?? ''
    const mayEdit = atLeast(active.levelFor('plan'), 'edit')
    const mayDelete = atLeast(active.levelFor('plan'), 'delete')
    view = {
      plan: memberPlan.data,
      scope: { kind: 'member', ownerId: memberId ?? '' },
      rights: {
        addPosition: mayEdit,
        addBooking: atLeast(active.levelFor('accounts'), 'edit'),
        deleteMonth: mayDelete,
        editPosition: () => mayEdit,
        deletePosition: () => mayDelete,
      },
      lead: `${t('plan.memberLead', { name })} ${
        !mayEdit
          ? t('plan.viewOnly', { name })
          : mayDelete
            ? t('plan.mayDelete')
            : t('plan.mayEdit')
      }`,
      standIn: mayEdit,
    }
  } else if (!shared && !foreign && ownPlan.data) {
    view = {
      plan: ownPlan.data,
      scope: OWN_SCOPE,
      rights: OWN_RIGHTS,
      lead: t('plan.lead'),
      standIn: false,
    }
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
        {view && (
          <PlanView
            plan={view.plan}
            scope={view.scope}
            rights={view.rights}
            ownerName={foreign ? (active.member?.firstName ?? '') : null}
            lead={view.lead}
            standIn={view.standIn}
            tab={tab}
            onTab={setTab}
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
