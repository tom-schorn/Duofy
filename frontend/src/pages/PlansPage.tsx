import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { ChevronRight, Plus, Users } from 'lucide-react'

import { QueryState } from '@/components/QueryState'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { errorText } from '@/lib/api'
import { useActiveMember } from '@/hooks/use-active-member'
import { useCreatePlan, useHouseholds, usePlans } from '@/lib/queries'
import {
  BLOCK_DOT,
  blockLabel,
  BUDGETS,
  MONTHS,
  monthLabel,
  QUOTA_KEY,
  atLeast,
  euro,
  unallocated,
  type Block,
  type PlanSummary,
} from '@/lib/domain'
import { formatNumber } from '@/lib/format'

/**
 * Overview of every monthly plan. One click opens a plan in detail.
 *
 * Each card shows target against actual for the three blocks. The totals arrive
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
  // Kein `mayDelete` hier: einen ganzen Monat löschen gibt es nicht.
  const mayEdit = atLeast(active.levelFor('plan'), 'edit')
  const plans = usePlans(active.id)
  const households = useHouseholds()
  const [creating, setCreating] = useState(false)

  const names = Object.fromEntries(
    (households.data ?? []).map((household) => [household.id, household.name])
  )

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-col gap-2">
          <h1 className="font-heading text-3xl font-semibold">{t('plans.title')}</h1>
          <p className="text-muted-foreground">
            {active.member === null
              ? t('plans.lead')
              : t('plans.leadMember', { name: active.member.firstName })}
          </p>
        </div>
        {mayEdit && (
          <Button onClick={() => setCreating(true)}>
            <Plus className="size-4" />
            {t('plans.create')}
          </Button>
        )}
      </header>

      <QueryState isPending={plans.isPending} error={plans.error}>
        {plans.data?.length === 0 ? (
          <p className="text-muted-foreground border-border rounded-lg border border-dashed p-10 text-center text-sm">
            {active.member === null
              ? t('plans.empty')
              : mayEdit
              ? t('plans.emptyMemberEdit', { name: active.member.firstName })
              : t('plans.emptyMember', { name: active.member.firstName })}
          </p>
        ) : (
          <ul className="flex flex-col gap-3">
            {plans.data?.map((plan) => (
              <li key={`${plan.year}-${plan.month}`}>
                <PlanCard plan={plan} householdNames={names} ownerId={active.id} />
              </li>
            ))}
          </ul>
        )}
      </QueryState>

      <CreatePlanDialog
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
  householdNames,
  ownerId,
}: {
  plan: PlanSummary
  householdNames: Record<string, string>
  /** Whose month this is, or null for your own. */
  ownerId: string | null
}) {
  const unpaid = Number(plan.unpaid)
  const { t } = useTranslation()
  const unpaidLabel = unpaid > 0 ? euro.format(unpaid) : t('plans.allPaid')
  // What is left to allocate is the free remainder of the budget, not the budget.
  const free = unallocated(plan)

  return (
    <Link
      // The person travels with the link — without it a foreign month would open
      // your own August, or nothing at all.
      to={{
        pathname: `/plan/${plan.year}/${String(plan.month).padStart(2, '0')}`,
        search: ownerId === null ? '' : `?member=${ownerId}`,
      }}
      className="bg-card ring-foreground/10 hover:ring-ring focus-visible:ring-ring flex flex-col gap-4 rounded-xl p-5 ring-1 transition-[box-shadow]"
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-heading text-xl font-semibold">
            {monthLabel(plan.month)} {plan.year}
          </span>
          {/* Zeigt, dass dieser Plan Posten in einen Haushalt einspeist.
              Der Haushaltsplan ist keine eigene Tabelle — er entsteht aus
              genau diesen Posten. */}
          {plan.householdIds.map((id) => (
            <Badge key={id} variant="secondary" className="gap-1 font-normal">
              <Users className="size-3" />
              {householdNames[id] ?? t('plans.household')}
            </Badge>
          ))}
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
        {BUDGETS.map((block) => (
          <BudgetRow key={block} plan={plan} block={block} />
        ))}
      </div>
    </Link>
  )
}

function BudgetRow({ plan, block }: { plan: PlanSummary; block: Block }) {
  const key = block as keyof typeof QUOTA_KEY
  const { t } = useTranslation()
  const quota = Number(plan[QUOTA_KEY[key]])
  const target = Number(plan.budget) * (quota / 100)
  const actual = Number(plan.spent[key])
  const percent = target > 0 ? (actual / target) * 100 : 0
  const isOver = percent > OVER_QUOTA

  return (
    <div className="grid grid-cols-[7rem_1fr_auto] items-center gap-3 text-xs">
      <span className="flex items-center gap-2">
        <span className={`size-2 rounded-sm ${BLOCK_DOT[block]}`} />
        {blockLabel(block)}
        <span className="text-muted-foreground">
          {t('common.percent', { value: formatNumber(quota) })}
        </span>
      </span>

      <span className="bg-muted h-1.5 overflow-hidden rounded-full">
        <span
          className={`block h-full rounded-full ${isOver ? 'bg-destructive' : BLOCK_DOT[block]}`}
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

function CreatePlanDialog({
  ownerId,
  ownerName,
  open,
  onOpenChange,
}: {
  ownerId: string | null
  ownerName: string | null
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const today = new Date()
  const { t } = useTranslation()
  const [year, setYear] = useState(today.getFullYear())
  const [month, setMonth] = useState(today.getMonth() + 1)
  const create = useCreatePlan()

  // This year and the next few — planning retroactively rarely makes sense.
  const years = [today.getFullYear(), today.getFullYear() + 1]

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <form
          onSubmit={(event) => {
            event.preventDefault()
            create.mutate(
              { year, month, ownerId },
              { onSuccess: () => onOpenChange(false) }
            )
          }}
          className="flex flex-col gap-5"
        >
          <DialogHeader>
            <DialogTitle className="font-heading text-xl">
              {ownerName === null
                ? t('plans.create')
                : t('plans.createFor', { name: ownerName })}
            </DialogTitle>
            <DialogDescription>
              {ownerName === null
                ? t('plans.createHint')
                : t('plans.createForHint', { name: ownerName })}
            </DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-2">
              <Label>{t('plans.month')}</Label>
              <Select
                value={String(month)}
                onValueChange={(value) => setMonth(Number(value))}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {MONTHS.map((month) => (
                    <SelectItem key={month} value={String(month)}>
                      {monthLabel(month)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="flex flex-col gap-2">
              <Label>{t('plans.year')}</Label>
              <Select
                value={String(year)}
                onValueChange={(value) => setYear(Number(value))}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {years.map((value) => (
                    <SelectItem key={value} value={String(value)}>
                      {value}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          {create.isError && (
            <p className="border-destructive bg-destructive/10 text-destructive rounded-md border px-3 py-2 text-sm">
              {errorText(create.error)}
            </p>
          )}

          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => onOpenChange(false)}
            >
              {t('common.cancel')}
            </Button>
            <Button type="submit" disabled={create.isPending}>
              {create.isPending ? t('plans.creating') : t('common.create')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
