import { useState } from 'react'
import { useTranslation } from 'react-i18next'

import { AccountCards } from '@/components/AccountCards'
import { BookFlow } from '@/components/BookFlow'
import { MonthBook } from '@/components/MonthBook'
import { MonthSwitch } from '@/components/MonthSwitch'
import { useActiveMember } from '@/hooks/use-active-member'
import { OWN_SCOPE, atLeast, type BookScope } from '@/lib/domain'
import { usePlan } from '@/lib/queries'

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
  const mayEdit = atLeast(active.levelFor('accounts'), 'edit')

  const today = new Date()
  const [year, setYear] = useState(today.getFullYear())
  const [month, setMonth] = useState(today.getMonth() + 1)

  const scope: BookScope =
    active.id === null ? OWN_SCOPE : { kind: 'member', ownerId: active.id }

  // A month without a plan is a normal answer here, not a failure — the whole
  // point of the page is that the book does not need one.
  const plan = usePlan(year, month, true, active.id)
  const positions = plan.data?.positions ?? []

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-col gap-2">
          <h1 className="font-heading text-3xl font-semibold">{t('book.title')}</h1>
          <p className="text-muted-foreground">
            {active.member === null
              ? t('book.lead')
              : `${t('book.leadMember', { name: active.member.firstName })}${
                  mayEdit ? '' : ` ${t('book.leadMemberView', { name: active.member.firstName })}`
                }`}
          </p>
        </div>

        <MonthSwitch
          year={year}
          month={month}
          onChange={(nextYear, nextMonth) => {
            setYear(nextYear)
            setMonth(nextMonth)
          }}
        />
      </header>

      <AccountCards scope={scope} />

      <BookFlow year={year} month={month} scope={scope} />

      {plan.data === undefined && !plan.isPending && (
        <p className="text-muted-foreground border-border rounded-lg border border-dashed px-4 py-3 text-sm">
          {t('book.noPlan')}
        </p>
      )}

      <MonthBook
        positions={positions}
        year={year}
        month={month}
        scope={scope}
        readOnly={!mayEdit}
      />
    </div>
  )
}
