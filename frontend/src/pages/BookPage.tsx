import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router'

import { AccountCards } from '@/components/AccountCards'
import { BookFlow } from '@/components/BookFlow'
import { EmptyState } from '@/components/EmptyState'
import { MonthBook } from '@/components/MonthBook'
import { MonthSwitch } from '@/components/MonthSwitch'
import { useActiveMember } from '@/hooks/use-active-member'
import { ApiError } from '@/lib/api'
import { parseMonth } from '@/lib/dates'
import { OWN_SCOPE, atLeast, isViewOnly, type BookScope } from '@/lib/domain'
import { useAccounts, usePlan } from '@/lib/queries'

/**
 * The book, on its own.
 *
 * It used to live inside a month plan, which tied it to something it does not
 * depend on: a booking belongs to an **account**, not to a plan. The
 * consequence was worse than untidy — bookings in a month nobody had planned
 * were invisible while still moving the balances, which is exactly what an
 * import produces.
 *
 * So: its own page, its own month picker, and a plan that may or may not exist.
 * Without one there is nothing to assign a booking to, and everything else
 * works as before.
 */
export function BookPage() {
  const { t } = useTranslation()
  const active = useActiveMember()
  const accountsLevel = active.levelFor('accounts')
  const mayEdit = atLeast(accountsLevel, 'edit')
  const isAccountsViewOnly = isViewOnly(accountsLevel)

  // The month lives in the address (`?month=2026-09`), so a reload and a link keep
  // it. Anything that is not a valid month falls back to the current one.
  const [params, setParams] = useSearchParams()
  const raw = params.get('month') ?? ''
  const [rawYear, rawMonth] = raw.split('-')
  const parsed = /^\d{4}-\d{2}$/.test(raw) ? parseMonth(rawYear, rawMonth) : null
  const today = new Date()
  const year = parsed?.year ?? today.getFullYear()
  const month = parsed?.month ?? today.getMonth() + 1

  const scope: BookScope =
    active.id === null ? OWN_SCOPE : { kind: 'member', ownerId: active.id }

  // A month without a plan is a normal answer here, not a failure — the whole
  // point of the page is that the book does not need one.
  const plan = usePlan(year, month, true, active.id)
  const positions = plan.data?.positions ?? []

  // Accounts and the book can be shared without the plan — the two grants are
  // separate. A missing plan grant is not "no plan for this month" (that hint is
  // for a month nobody has created yet); it is honestly a missing share (#217).
  const planNotShared =
    active.member !== null &&
    plan.error instanceof ApiError &&
    plan.error.code === 'no_insight_granted'

  // Loaded here, once, so a missing grant can gate the whole book instead of
  // `AccountCards` and `MonthBook` each finding their own way to say nothing
  // (#217). `useAccounts` shares its cache with theirs, so this costs no extra
  // request.
  const accounts = useAccounts(scope)

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-col gap-2">
          <h1 className="font-heading text-3xl font-semibold">{t('book.title')}</h1>
          <p className="text-muted-foreground">
            {active.member === null
              ? t('book.lead')
              : `${t('book.leadMember', { name: active.member.firstName })}${
                  isAccountsViewOnly
                    ? ` ${t('book.leadMemberView', { name: active.member.firstName })}`
                    : ''
                }`}
          </p>
        </div>

        <MonthSwitch
          year={year}
          month={month}
          onChange={(nextYear, nextMonth) =>
            setParams((current: URLSearchParams) => {
              current.set('month', `${nextYear}-${String(nextMonth).padStart(2, '0')}`)
              return current
            })
          }
        />
      </header>

      {active.member &&
      accounts.error instanceof ApiError &&
      accounts.error.code === 'no_insight_granted' ? (
        <EmptyState>
          {t('book.notShared', { name: active.member.firstName })}
        </EmptyState>
      ) : (
        <>
          <AccountCards scope={scope} />

          <BookFlow
            year={year}
            month={month}
            scope={scope}
            notShared={
              active.member && t('accounts.notShared', { name: active.member.firstName })
            }
          />

          {planNotShared ? (
            <p className="text-muted-foreground border-border rounded-lg border border-dashed px-4 py-3 text-sm">
              {t('book.planNotShared', { name: active.member!.firstName })}
            </p>
          ) : (
            plan.data === undefined &&
            !plan.isPending && (
              <p className="text-muted-foreground border-border rounded-lg border border-dashed px-4 py-3 text-sm">
                {t('book.noPlan')}
              </p>
            )
          )}

          <MonthBook
            positions={positions}
            year={year}
            month={month}
            scope={scope}
            readOnly={!mayEdit}
          />
        </>
      )}
    </div>
  )
}
