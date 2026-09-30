import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { ChevronRight, Plus } from 'lucide-react'

import { CreatePlanDialog } from '@/components/CreatePlanDialog'
import { EmptyState } from '@/components/EmptyState'
import { QueryState } from '@/components/QueryState'
import { Button } from '@/components/ui/button'
import { scopeSearch, useActiveMember } from '@/hooks/use-active-member'
import { useHouseholds, useHouseholdPlans, usePlans } from '@/lib/queries'
import {
  BUDGET_DOT,
  budgetLabel,
  BUDGETS,
  monthLabel,
  nextMissingMonth,
  QUOTA_KEY,
  atLeast,
  euro,
  isViewOnly,
  unallocated,
  type Budget,
  type PlanSummary,
} from '@/lib/domain'
import { formatNumber } from '@/lib/format'

/**
 * Overview of every monthly plan. One click opens a plan in detail.
 *
 * Each card shows target against actual for the three budgets. The totals arrive
 * ready-made from the backend — the overview does not load every position of every
 * month just to add them up.
 */

/** Beyond this the quota is broken. */
const OVER_QUOTA = 100

export function PlansPage() {
  const { t } = useTranslation()
  // `?member=` shows the months of a person who granted insight — see
  // `MemberSwitcher`. At level `edit` a month can also be created for them: the
  // positions come from **their** commitments, so nothing of the helper ends up
  // in it. Someone who is allowed to plan along needs to be able to start the
  // month, otherwise the first empty month stops them.
  const active = useActiveMember()
  // `?household=` shows the months of the household plan — reached from "Alle
  // Pläne" on that plan. The household owns nothing, so there is no month to list
  // for it directly; the server reads the months off every member's
  // positions, not just the viewer's — a month only a partner planned into still
  // belongs on this list. Both are chosen in the sidebar, never on this page.
  const householdId = active.householdId
  const inHousehold = householdId !== null
  // Kein `mayDelete` hier: einen ganzen Monat löschen gibt es nicht. Für den
  // Haushalt gibt es gar kein Anlegen — der Haushaltsplan wird zusammengesetzt,
  // nie selbst erzeugt.
  const planLevel = active.levelFor('plan')
  const mayEdit = !inHousehold && atLeast(planLevel, 'create')
  const isPlanViewOnly = !inHousehold && isViewOnly(planLevel)
  const ownPlans = usePlans(active.id, !inHousehold)
  const householdPlans = useHouseholdPlans(inHousehold ? householdId : null)
  const plans = inHousehold ? householdPlans : ownPlans
  const households = useHouseholds()
  const [creating, setCreating] = useState(false)

  const names = Object.fromEntries(
    (households.data ?? []).map((household) => [household.id, household.name])
  )
  const householdName = householdId ? (names[householdId] ?? t('plans.household')) : null

  const rows = plans.data ?? []

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-col gap-2">
          <h1 className="font-heading text-3xl font-semibold">{t('plans.title')}</h1>
          <p className="text-muted-foreground">
            {inHousehold
              ? t('plans.leadHousehold', { name: householdName })
              : active.member === null
                ? t('plans.lead')
                : `${t('plans.leadMember', { name: active.member.firstName })}${
                    isPlanViewOnly
                      ? ` ${t('plans.leadMemberView', { name: active.member.firstName })}`
                      : ''
                  }`}
          </p>
        </div>
        {mayEdit && (
          <Button onClick={() => setCreating(true)}>
            <Plus className="size-4" />
            {t('plans.create')}
          </Button>
        )}
      </header>

      <QueryState
        isPending={plans.isPending}
        error={plans.error}
        onRetry={() => void plans.refetch()}
        notShared={
          !inHousehold && active.member
            ? t('plan.notShared', { name: active.member.firstName })
            : undefined
        }
      >
        {rows.length === 0 ? (
          <EmptyState
            action={
              mayEdit && (
                <Button onClick={() => setCreating(true)}>
                  <Plus className="size-4" />
                  {t('plans.create')}
                </Button>
              )
            }
          >
            {inHousehold
              ? t('plans.emptyHousehold', { name: householdName })
              : active.member === null
                ? t('plans.empty')
                : mayEdit
                  ? t('plans.emptyMemberEdit', { name: active.member.firstName })
                  : t('plans.emptyMember', { name: active.member.firstName })}
          </EmptyState>
        ) : (
          <ul className="flex flex-col gap-3">
            {rows.map((plan) => (
              <li key={`${plan.year}-${plan.month}`}>
                <PlanCard
                  plan={plan}
                  search={scopeSearch(active)}
                />
              </li>
            ))}
          </ul>
        )}
      </QueryState>

      <CreatePlanDialog
        {...nextMissingMonth(plans.data ?? [])}
        open={creating}
        onOpenChange={setCreating}
        ownerId={active.id}
        ownerName={active.member?.firstName ?? null}
      />
    </div>
  )
}

function PlanCard({
  plan,
  search,
}: {
  plan: PlanSummary
  /** `?member=` or `?household=` to carry along, or '' for your own plan. */
  search: string
}) {
  const unpaid = Number(plan.unpaid)
  const { t } = useTranslation()
  const unpaidLabel = unpaid > 0 ? euro.format(unpaid) : t('plans.allPaid')
  // What is left to allocate is the free remainder of the distributable amount,
  // not the amount itself.
  const free = unallocated(plan)

  return (
    <Link
      // The scope travels with the link — without it a foreign or household month
      // would open your own August, or nothing at all.
      to={{
        pathname: `/plan/${plan.year}/${String(plan.month).padStart(2, '0')}`,
        search,
      }}
      className="bg-card ring-foreground/10 hover:ring-ring focus-visible:ring-ring flex flex-col gap-4 rounded-xl p-5 ring-1 transition-[box-shadow]"
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-heading text-xl font-semibold">
            {monthLabel(plan.month)} {plan.year}
          </span>
        </div>
        <ChevronRight className="text-muted-foreground size-4" />
      </div>

      <div className="flex flex-wrap gap-x-8 gap-y-2 text-sm">
        <span>
          <span className="text-muted-foreground">{t('plans.allocatable')}{' '}</span>
          <span
            className={`font-medium tabular-nums ${free < 0 ? 'text-destructive' : ''}`}
          >
            {euro.format(free)}
          </span>
        </span>
        <span>
          <span className="text-muted-foreground">{t('plans.open')}{' '}</span>
          <span
            className={`font-medium tabular-nums ${unpaid > 0 ? '' : 'text-muted-foreground'}`}
          >
            {unpaidLabel}
          </span>
        </span>
      </div>

      <div className="flex flex-col gap-2">
        {BUDGETS.map((budget) => (
          <BudgetRow key={budget} plan={plan} budget={budget} />
        ))}
      </div>
    </Link>
  )
}

function BudgetRow({ plan, budget }: { plan: PlanSummary; budget: Budget }) {
  const key = budget as keyof typeof QUOTA_KEY
  const { t } = useTranslation()
  const quota = Number(plan[QUOTA_KEY[key]])
  const target = Number(plan.distributable) * (quota / 100)
  const actual = Number(plan.spent[key])
  const percent = target > 0 ? (actual / target) * 100 : 0
  const isOver = percent > OVER_QUOTA

  return (
    <div className="grid grid-cols-[8rem_1fr_auto] items-center gap-3 text-xs">
      {/* whitespace-nowrap: "Grundbedarf 50 %" no longer fit the old 7rem column
          without the percent breaking onto its own line. */}
      <span className="flex items-center gap-2 whitespace-nowrap">
        <span className={`size-2 rounded-sm ${BUDGET_DOT[budget]}`} />
        {budgetLabel(budget)}
        <span className="text-muted-foreground">
          {t('common.percent', { value: formatNumber(quota) })}
        </span>
      </span>

      <span className="bg-muted h-1.5 overflow-hidden rounded-full">
        <span
          className={`block h-full rounded-full ${isOver ? 'bg-destructive' : BUDGET_DOT[budget]}`}
          style={{ width: `${Math.min(percent, 100)}%` }}
        />
      </span>

      <span className="tabular-nums">
        <span
          className={isOver ? 'text-destructive font-semibold' : 'font-medium'}
        >
          {euro.format(actual)}
        </span>
        <span className="text-muted-foreground">
          {' '}
          {t('budget.of', { amount: euro.format(target) })}
        </span>
      </span>
    </div>
  )
}
