import { Fragment } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { AlertTriangle, ArrowLeft } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { QueryState } from '@/components/QueryState'
import { useHouseholds, useMe, useSetGrants } from '@/lib/queries'
import {
  accessLabel,
  ACCESS_ORDER,
  areaLabel,
  AREA_ORDER,
  type AccessLevel,
  type Member,
} from '@/lib/domain'
import { GRANT_PRESETS, grantHints, presetOf, PRESET_ORDER } from '@/lib/grants'

/**
 * What I allow each other member to do with my data (decisions 57, 62, 63).
 *
 * One card per person, because the decision concerns exactly that person. Every
 * choice takes effect at once and reports itself (UI guideline rule 24) — there is
 * no form to submit. Admins see nothing extra here: nobody sets rights on somebody
 * else's data (decision 58).
 */
export function GrantsPage() {
  const { t } = useTranslation()
  const households = useHouseholds()
  const me = useMe()
  // Everybody belongs to exactly one household.
  const household = households.data?.[0]
  const others = household?.members.filter((member) => member.userId !== me.data?.id) ?? []

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-col gap-2">
        <Link
          to="/household"
          className="text-muted-foreground hover:text-foreground flex w-fit items-center gap-1.5 text-sm"
        >
          <ArrowLeft className="size-4" />
          {t('grants.back')}
        </Link>
        <h1 className="font-heading text-3xl font-semibold">{t('grants.title')}</h1>
        <p className="text-muted-foreground max-w-2xl">{t('grants.lead')}</p>
      </header>

      <QueryState
        isPending={households.isPending || me.isPending}
        error={households.error ?? me.error}
        onRetry={() => void Promise.all([households.refetch(), me.refetch()])}
      >
        {household && others.length === 0 ? (
          <p className="text-muted-foreground">{t('grants.empty')}</p>
        ) : (
          <ul className="flex flex-col gap-4">
            {household &&
              others.map((member) => (
                <GrantCard key={member.userId} householdId={household.id} member={member} />
              ))}
          </ul>
        )}
      </QueryState>
    </div>
  )
}

function GrantCard({ householdId, member }: { householdId: string; member: Member }) {
  const { t } = useTranslation()
  const save = useSetGrants(householdId)
  const levels = member.myGrants
  const current = presetOf(levels)
  const name = `${member.firstName} ${member.lastName}`
  const headingId = `grants-${member.userId}`

  return (
    <li>
      <section
        aria-labelledby={headingId}
        className="bg-card flex flex-col gap-4 rounded-xl p-5 ring-1 ring-foreground/10"
      >
        <div className="flex flex-wrap items-center gap-2">
          <h2 id={headingId} className="font-heading text-xl font-semibold">
            {name}
          </h2>
          <Badge
            variant={member.role === 'admin' ? 'secondary' : 'outline'}
            className="font-normal"
          >
            {t(`household.roles.${member.role}`)}
          </Badge>
        </div>

        <fieldset className="flex min-w-0 flex-wrap items-center gap-2">
          <legend className="sr-only">
            {t('grants.presetsFor', { name: member.firstName })}
          </legend>
          {PRESET_ORDER.map((key) => (
            <Button
              key={key}
              type="button"
              size="sm"
              variant={current === key ? 'secondary' : 'outline'}
              aria-pressed={current === key}
              onClick={() => save.mutate({ member, levels: GRANT_PRESETS[key] })}
            >
              {t(`grants.presets.${key}`)}
            </Button>
          ))}
          {current === null && (
            <Badge variant="outline" className="font-normal">
              {t('grants.ownChoice')}
            </Badge>
          )}
        </fieldset>

        <div className="grid items-center gap-x-4 gap-y-2 sm:grid-cols-[10rem_minmax(0,20rem)]">
          {AREA_ORDER.map((area) => {
            const id = `grant-${member.userId}-${area}`
            return (
              <Fragment key={area}>
                <Label htmlFor={id}>{areaLabel(area)}</Label>
                <Select
                  value={levels[area]}
                  // Only this area travels; the others keep their level.
                  onValueChange={(next) =>
                    save.mutate({ member, levels: { [area]: next as AccessLevel } })
                  }
                >
                  <SelectTrigger id={id} className="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {ACCESS_ORDER.map((option) => (
                      <SelectItem key={option} value={option}>
                        {accessLabel(area, option)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Fragment>
            )
          })}
        </div>

        {/* Hints only, nothing is raised by itself (decision 62). Polite, so a
            screen reader hears a hint that appears after a choice. */}
        <div aria-live="polite" className="flex flex-col gap-1">
          {grantHints(levels).map((hint) => (
            <p key={hint} className="flex items-start gap-2 text-sm">
              <AlertTriangle
                aria-hidden
                className="mt-0.5 size-4 shrink-0 text-amber-600 dark:text-amber-500"
              />
              {t(`grants.hints.${hint}`, { name: member.firstName })}
            </p>
          ))}
        </div>
      </section>
    </li>
  )
}
