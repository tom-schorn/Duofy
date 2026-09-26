import {
  Area,
  AreaChart,
  CartesianGrid,
  ReferenceDot,
  ReferenceLine,
  XAxis,
  YAxis,
} from 'recharts'
import { Trans, useTranslation } from 'react-i18next'

import { ChartContainer, ChartTooltip, type ChartConfig } from '@/components/ui/chart'
import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { Empty, EmptyDescription, EmptyHeader } from '@/components/ui/empty'
import { Label } from '@/components/ui/label'
import { QueryState } from '@/components/QueryState'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { euro, monthLabel, type FlowEntry, type FlowLimitsBy, type PlanFlow } from '@/lib/domain'
import { today } from '@/lib/dates'
import { formatNumber } from '@/lib/format'
import { useFlow, useSetFlowLimitsBy } from '@/lib/queries'

/**
 * The flow of money across the month: when does what come in and go out.
 *
 * The question behind it: **when in the month does money run short?** Rent on the
 * 1st and child benefit on the 20th add up in the monthly total, but not here.
 *
 * **The backend computes, this component draws.** Which account, plan versus
 * bookings, limits, the carry-over of a start, and the shortfall itself (a hint
 * from the pipe) all arrive ready-made. Nothing here works out a bottleneck of its
 * own, so there is one truth for every client.
 *
 * The curve starts at zero and shows the **change since the start of the month**,
 * not a balance — nothing is labelled a balance unless the curve starts at one.
 *
 * Steps rather than a smooth line: money moves in jumps.
 */

type Scope = { householdId?: string | null; ownerId?: string | null }

type Props = Scope & {
  year: number
  month: number
  /** Height of the plot area. Flatter for printing — see `PlanSankey`. */
  height?: string
  /** Print copies show the curve only, no switch. */
  print?: boolean
}

export function MonthFlow({ year, month, householdId, ownerId, height, print }: Props) {
  const query = useFlow(year, month, { householdId, ownerId })
  return (
    <QueryState isPending={query.isPending} error={query.error} rows={2}>
      {query.data && (
        <FlowView
          flow={query.data}
          shared={householdId != null}
          height={height}
          showSwitch={!print}
        />
      )}
    </QueryState>
  )
}

function LimitsSwitch({ value }: { value: FlowLimitsBy }) {
  const { t } = useTranslation()
  const save = useSetFlowLimitsBy()
  return (
    <div className="flex flex-col gap-1.5 print:hidden">
      <Label htmlFor="flow-limits-by">{t('monthFlow.limitsBy')}</Label>
      <Select value={value} onValueChange={(next) => save.mutate(next as FlowLimitsBy)}>
        <SelectTrigger id="flow-limits-by" className="w-56" disabled={save.isPending}>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="plan">{t('monthFlow.limitsByPlan')}</SelectItem>
          <SelectItem value="bookings">{t('monthFlow.limitsByBookings')}</SelectItem>
        </SelectContent>
      </Select>
      <p className="text-muted-foreground max-w-[60ch] text-xs">
        {t(value === 'plan' ? 'monthFlow.limitsByPlanHint' : 'monthFlow.limitsByBookingsHint')}
      </p>
    </div>
  )
}

export function FlowView({
  flow,
  shared = false,
  height = 'h-60',
  showSwitch = true,
}: {
  flow: PlanFlow
  shared?: boolean
  height?: string
  showSwitch?: boolean
}) {
  const { t } = useTranslation()
  const config = {
    change: { label: t('monthFlow.change'), color: 'var(--foreground)' },
  } satisfies ChartConfig

  const days = flow.days.map((d) => ({ day: d.day, change: Number(d.balance) }))
  const hasCarryOver = Number(flow.start) !== 0
  const shortfall = flow.hints.find((hint) => hint.code === 'flow_shortfall')
  const values = days.map((d) => d.change)
  const low = Math.min(0, ...values)
  const high = Math.max(0, ...values)
  // Where zero sits in the area's own box, top = 0: the fill turns to the
  // shortfall colour below it, so the whole stretch under zero is marked.
  const zeroAt = high === low ? 1 : high / (high - low)

  const isCurrentMonth = today().startsWith(
    `${flow.year}-${String(flow.month).padStart(2, '0')}`
  )
  const todayDay = isCurrentMonth ? Number(today().slice(8, 10)) : null

  const heading =
    shortfall ? (
      <>
        <p className="text-2xl font-semibold">
          <Trans
            i18nKey={shared ? 'monthFlow.shortfallShared' : 'monthFlow.shortfall'}
            values={{ amount: euro.format(Number(shortfall.params.amount)) }}
            components={{ amount: <span className="font-mono tabular-nums" /> }}
          />
        </p>
        <p className="text-muted-foreground text-sm">
          {t('monthFlow.lowPoint', { day: shortfall.params.day })}
        </p>
      </>
    ) : (
      <>
        <p className="text-2xl font-semibold">
          {t(shared ? 'monthFlow.selfCarryingShared' : 'monthFlow.selfCarrying')}
        </p>
        <p className="text-muted-foreground text-sm">{t('monthFlow.selfCarryingHint')}</p>
      </>
    )

  return (
    <Card className="gap-5 px-5 py-5">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <header className="flex flex-col gap-1">{flow.entries.length === 0 ? null : heading}</header>
        {showSwitch && <LimitsSwitch value={flow.flowLimitsBy} />}
      </div>

      {flow.entries.length === 0 ? (
        <Empty className="border-border rounded-xl border border-dashed">
          <EmptyHeader>
            <EmptyDescription>{t('monthFlow.empty')}</EmptyDescription>
          </EmptyHeader>
        </Empty>
      ) : (
        <ChartContainer config={config} className={`${height} w-full`}>
          <AreaChart data={days} margin={{ left: 4, right: 4, top: 8 }}>
            <defs>
              <linearGradient id="flow-fill" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor="var(--color-change)" stopOpacity={0.16} />
                <stop offset={zeroAt} stopColor="var(--color-change)" stopOpacity={0.04} />
                <stop offset={zeroAt} stopColor="var(--destructive)" stopOpacity={0.3} />
                <stop offset="100%" stopColor="var(--destructive)" stopOpacity={0.3} />
              </linearGradient>
            </defs>

            <CartesianGrid vertical={false} />
            <XAxis
              dataKey="day"
              tickLine={false}
              axisLine={false}
              tickMargin={8}
              interval={4}
              tickFormatter={(day) => t('common.dueDay', { day })}
            />
            <YAxis
              tickLine={false}
              axisLine={false}
              width={52}
              domain={[low, high]}
              tickFormatter={(v) => formatNumber(v, { maximumFractionDigits: 0 })}
            />
            {/* Zero is the reference: below it money is missing. */}
            <ReferenceLine y={0} className="stroke-border" strokeWidth={1} />
            {/* The current month: what lies left of today is booked or due, what
                lies right is still a plan. */}
            {todayDay !== null && (
              <ReferenceLine
                x={todayDay}
                className="stroke-muted-foreground"
                strokeDasharray="4 4"
                label={{ value: t('monthFlow.today'), position: 'top', fontSize: 11 }}
              />
            )}

            <ChartTooltip
              content={({ active, label }) => {
                if (!active) return null
                const day = Number(label)
                const point = days.find((d) => d.day === day)
                const entries = flow.entries.filter((entry) => entry.day === day)
                return (
                  <div className="border-border/50 bg-background grid min-w-40 gap-1.5 rounded-lg border px-2.5 py-1.5 text-xs shadow-xl">
                    <div className="font-medium">
                      {t('common.dayOfMonth', { day, month: monthLabel(flow.month) })}
                    </div>
                    {entries.map((entry, index) => (
                      <div key={index} className="flex justify-between gap-3">
                        <span>{entryLabel(entry, t)}</span>
                        <span className="font-mono tabular-nums">{signed(entry.amount)}</span>
                      </div>
                    ))}
                    <div className="border-border/50 flex justify-between gap-3 border-t pt-1">
                      <span>{t(hasCarryOver ? 'monthFlow.balance' : 'monthFlow.change')}</span>
                      <span className="font-mono tabular-nums">
                        {euro.format(point?.change ?? 0)}
                      </span>
                    </div>
                  </div>
                )
              }}
            />

            <Area
              type="stepAfter"
              dataKey="change"
              stroke="var(--color-change)"
              strokeWidth={2}
              fill="url(#flow-fill)"
              dot={false}
              activeDot={{ r: 4 }}
            />

            {shortfall && (
              <ReferenceDot
                x={Number(shortfall.params.day)}
                y={-Number(shortfall.params.amount)}
                r={5}
                className="fill-destructive stroke-card"
                strokeWidth={2}
              />
            )}
          </AreaChart>
        </ChartContainer>
      )}

      {flow.entries.length > 0 && (
        // Off on paper: `PlanPrintout` carries the positions on page 2.
        <div className="w-full overflow-x-auto print:hidden">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-14">{t('monthFlow.day')}</TableHead>
                <TableHead>{t('monthFlow.position')}</TableHead>
                <TableHead>{t('monthFlow.kind')}</TableHead>
                <TableHead className="text-right">{t('common.amount')}</TableHead>
                <TableHead className="text-right">
                  {t(hasCarryOver ? 'monthFlow.balance' : 'monthFlow.change')}
                </TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {flow.entries.map((entry, index) => (
                <TableRow key={`${entry.date}-${entry.positionId}-${index}`}>
                  <TableCell className="text-muted-foreground tabular-nums">
                    {t('common.dueDay', { day: entry.day })}
                  </TableCell>
                  <TableCell>{entryLabel(entry, t)}</TableCell>
                  <TableCell>
                    <Badge variant={entry.kind === 'booking' ? 'secondary' : 'outline'}>
                      {t(entry.kind === 'booking' ? 'monthFlow.booked' : 'monthFlow.planned')}
                    </Badge>
                  </TableCell>
                  <TableCell className="text-right font-mono tabular-nums">
                    {signed(entry.amount)}
                  </TableCell>
                  <TableCell
                    className={`text-right font-mono tabular-nums ${Number(entry.balance) < 0 ? 'text-destructive' : ''}`}
                  >
                    {euro.format(Number(entry.balance))}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      <p className="text-muted-foreground max-w-[70ch] text-xs print:hidden">
        {t(hasCarryOver ? 'monthFlow.footnoteBalance' : 'monthFlow.footnote')}
      </p>
    </Card>
  )
}

function entryLabel(entry: FlowEntry, t: (key: string) => string): string {
  return entry.label === '' ? t('monthFlow.manualBooking') : entry.label
}

function signed(amount: string): string {
  const value = Number(amount)
  return `${value > 0 ? '+' : '−'}${euro.format(Math.abs(value))}`
}
