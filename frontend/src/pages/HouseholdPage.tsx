import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { LogOut, MoreHorizontal, Pencil, Plus, UserPlus } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { QueryState } from '@/components/QueryState'
import { errorText } from '@/lib/api'
import {
  useAcceptInvitation,
  useCreateHousehold,
  useDeclineInvitation,
  useHouseholds,
  useInvite,
  useLeaveHousehold,
  useMe,
  useMyInvitations,
  useSetMyAccess,
} from '@/lib/queries'
import {
  accessHint,
  accessLabel,
  ACCESS_ORDER,
  AREA_FIELD,
  areaLabel,
  AREA_ORDER,
  type AccessLevel,
  type Household,
  type Member,
  type Role,
} from '@/lib/domain'
import { shortDate } from '@/lib/dates'

/**
 * The household as a planning layer.
 *
 * A household **owns nothing** — no accounts, no commitments, no positions. It only
 * says who plans together. That is why no amount appears here: the figures live in
 * the plan, not on the household.
 *
 * A user can belong to several households at once, so the page lists all of them.
 *
 * TODO: make quotas and buffer editable per household. The columns exist by now
 * (`target_needs` and friends on `Household`), the interface for them does not.
 */

/** Catalog keys of the roles. */
const ROLE_LABEL: Record<Role, string> = {
  owner: 'household.roles.owner',
  member: 'household.roles.member',
}

export function HouseholdPage() {
  const { t } = useTranslation()
  const households = useHouseholds()
  const me = useMe()
  const [invitingTo, setInvitingTo] = useState<Household | null>(null)
  const currentUserId = me.data?.id ?? ''

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-col gap-2">
          <h1 className="font-heading text-3xl font-semibold">{t('household.title')}</h1>
          <p className="text-muted-foreground max-w-2xl">
            {t('household.lead')}
          </p>
        </div>
        <CreateHouseholdButton />
      </header>

      <PendingInvitations />

      <QueryState isPending={households.isPending} error={households.error}>
      <ul className="flex flex-col gap-4">
        {households.data?.map((household) => (
          <li
            key={household.id}
            className="bg-card flex flex-col gap-4 rounded-xl p-5 ring-1 ring-foreground/10"
          >
            <HouseholdHeader
              household={household}
              currentUserId={currentUserId}
              onInvite={() => setInvitingTo(household)}
            />

            <ul className="flex flex-col">
              {household.members.map((member) => {
                const isMe = member.userId === currentUserId
                return (
                  <li
                    key={member.userId}
                    className="border-border/60 flex flex-wrap items-center gap-3 border-b py-2.5 last:border-b-0"
                  >
                    <span className="bg-muted text-muted-foreground flex size-8 items-center justify-center rounded-full text-xs font-semibold">
                      {member.firstName[0]}
                      {member.lastName[0]}
                    </span>

                    <span className="flex min-w-0 flex-col">
                      <span className="flex flex-wrap items-center gap-2">
                        <span className="font-medium">
                          {member.firstName} {member.lastName}
                        </span>
                        {isMe && (
                          <Badge variant="outline" className="font-normal">
                            {t('household.you')}
                          </Badge>
                        )}
                      </span>
                      <span className="text-muted-foreground truncate text-xs">
                        {member.email}
                      </span>
                    </span>

                    <Badge
                      variant={member.role === 'owner' ? 'secondary' : 'outline'}
                      className="ml-auto font-normal"
                    >
                      {t(ROLE_LABEL[member.role])}
                    </Badge>

                    {/* Die Freigabe steht bei der eigenen Zeile, weil man nur
                        die eigene setzen kann. Bei den anderen steht sie als
                        Text da — man soll sehen, was man von ihnen sieht. */}
                    <span className="w-full pl-11">
                      {isMe ? (
                        <AccessChoice householdId={household.id} member={member} />
                      ) : (
                        <span className="text-muted-foreground flex flex-col gap-0.5 text-xs">
                          {AREA_ORDER.map((area) => (
                            <span key={area}>
                              {areaLabel(area)}:{' '}
                              {accessLabel(area, member[AREA_FIELD[area]]).toLowerCase()}
                            </span>
                          ))}
                        </span>
                      )}
                    </span>
                  </li>
                )
              })}
            </ul>
          </li>
        ))}
      </ul>
      </QueryState>

      <InviteDialog
        household={invitingTo}
        onOpenChange={(open) => !open && setInvitingTo(null)}
      />
    </div>
  )
}

function HouseholdHeader({
  household,
  currentUserId,
  onInvite,
}: {
  household: Household
  currentUserId: string
  onInvite: () => void
}) {
  const leave = useLeaveHousehold()
  const { t } = useTranslation()
  const me = household.members.find((member) => member.userId === currentUserId)
  const isOwner = me?.role === 'owner'

  return (
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-heading text-xl font-semibold">
          {household.name}
        </span>
        <span className="text-muted-foreground text-sm">
          {t('household.memberCount', { number: household.members.length })}
        </span>
      </div>

      <div className="flex items-center gap-2">
        <Button variant="outline" size="sm" onClick={onInvite}>
          <UserPlus className="size-4" />
          {t('household.invite')}
        </Button>

        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button
              variant="ghost"
              size="icon"
              className="size-8"
              aria-label={`${household.name} verwalten`}
            >
              <MoreHorizontal className="size-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            {/* Umbenennen darf nur, wem der Haushalt gehört. */}
            {isOwner && (
              // TODO: a dialog for renaming.
              <DropdownMenuItem className="gap-2">
                <Pencil className="size-4" />
                {t('household.rename')}
              </DropdownMenuItem>
            )}
            {/* Die eingebrachten Posten bleiben im Haushalt stehen — vergangene
                Monate werden nicht umgeschrieben. In neue Pläne fließt nichts
                mehr, dafür fehlt die Mitgliedschaft. */}
            <DropdownMenuItem
              variant="destructive"
              className="gap-2"
              onSelect={() => leave.mutate(household.id)}
            >
              <LogOut className="size-4" />
              {t('household.leave')}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </div>
  )
}

function InviteDialog({
  household,
  onOpenChange,
}: {
  household: Household | null
  onOpenChange: (open: boolean) => void
}) {
  const [email, setEmail] = useState('')
  const { t } = useTranslation()
  const invite = useInvite(household?.id ?? '')

  return (
    <Dialog open={household !== null} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <form
          onSubmit={(event) => {
            event.preventDefault()
            invite.mutate(
              { email },
              {
                onSuccess: () => {
                  setEmail('')
                  onOpenChange(false)
                },
              }
            )
          }}
          className="flex flex-col gap-5"
        >
          <DialogHeader>
            <DialogTitle className="font-heading text-xl">
              {t('household.inviteTitle', { name: household?.name })}
            </DialogTitle>
            <DialogDescription>
              {t('household.inviteDescription')}
            </DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-2">
            <Label htmlFor="invite-email">{t('auth.email')}</Label>
            <Input
              id="invite-email"
              type="email"
              placeholder={t('household.invitePlaceholder')}
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              required
            />
            <p className="text-muted-foreground text-xs">
              {t('household.inviteHint')}
            </p>
          </div>

          {invite.isError && (
            <p className="border-destructive bg-destructive/10 text-destructive rounded-md border px-3 py-2 text-sm">
              {errorText(invite.error)}
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
            <Button type="submit" disabled={invite.isPending}>
              {invite.isPending ? t('household.sending') : t('household.sendInvite')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}


/**
 * The inbox for invitations.
 *
 * There is no email and no link anybody has to forward: whoever signs in with the
 * invited address finds the invitation here. Shows nothing while none is pending.
 */
function PendingInvitations() {
  const invitations = useMyInvitations()
  const { t } = useTranslation()
  const accept = useAcceptInvitation()
  const decline = useDeclineInvitation()

  if (!invitations.data?.length) return null

  return (
    <ul className="flex flex-col gap-3">
      {invitations.data.map((invitation) => (
        <li
          key={invitation.token}
          className="border-primary/40 bg-primary/5 flex flex-wrap items-center justify-between gap-4 rounded-lg border p-4"
        >
          <span className="flex min-w-0 items-center gap-3">
            <UserPlus className="text-primary size-5 shrink-0" />
            <span className="flex min-w-0 flex-col">
              <span className="font-medium">
                {t('household.invitedBy', {
                  name: invitation.invitedBy,
                  household: invitation.householdName,
                })}
              </span>
              <span className="text-muted-foreground text-xs">
                {t('household.validUntil', { date: shortDate(invitation.expiresAt) })}
              </span>
            </span>
          </span>

          <span className="flex shrink-0 gap-2">
            <Button
              variant="ghost"
              size="sm"
              onClick={() => decline.mutate(invitation.token)}
              disabled={decline.isPending || accept.isPending}
            >
              {t('household.decline')}
            </Button>
            <Button
              size="sm"
              onClick={() => accept.mutate(invitation.token)}
              disabled={accept.isPending || decline.isPending}
            >
              {t('household.join')}
            </Button>
          </span>
        </li>
      ))}

      {(accept.isError || decline.isError) && (
        // role="alert" so a screen reader announces the error — it appears after
        // the fact, without focus moving there.
        <li role="alert" className="text-destructive text-sm">
          {errorText(accept.error ?? decline.error)}
        </li>
      )}
    </ul>
  )
}

function CreateHouseholdButton() {
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const { t } = useTranslation()
  const create = useCreateHousehold()

  return (
    <>
      <Button onClick={() => setOpen(true)}>
        <Plus className="size-4" />
        {t('household.create')}
      </Button>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="sm:max-w-md">
          <form
            onSubmit={(event) => {
              event.preventDefault()
              create.mutate(
                { name },
                {
                  onSuccess: () => {
                    setName('')
                    setOpen(false)
                  },
                }
              )
            }}
            className="flex flex-col gap-5"
          >
            <DialogHeader>
              <DialogTitle className="font-heading text-xl">
                {t('household.create')}
              </DialogTitle>
              <DialogDescription>
                {t('household.createDescription')}
              </DialogDescription>
            </DialogHeader>

            <div className="flex flex-col gap-2">
              <Label htmlFor="household-name">{t('household.name')}</Label>
              <Input
                id="household-name"
                value={name}
                onChange={(event) => setName(event.target.value)}
                placeholder={t('household.namePlaceholder')}
                required
                autoFocus
              />
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
                onClick={() => setOpen(false)}
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
    </>
  )
}

/**
 * What I release to the others about myself.
 *
 * Deliberately here and not in the settings: the decision concerns exactly the
 * people listed next to it. Whoever makes it has to see who they are giving it to.
 */
function AccessChoice({
  householdId,
  member,
}: {
  householdId: string
  member: Member
}) {
  const save = useSetMyAccess(householdId)
  const { t } = useTranslation()

  return (
    <span className="flex flex-col gap-3">
      <span className="text-muted-foreground text-xs">{t('household.youShare')}</span>

      {AREA_ORDER.map((area) => {
        const level = member[AREA_FIELD[area]]
        return (
          <span key={area} className="flex flex-col gap-1">
            <span className="flex flex-wrap items-center gap-2">
              <span className="w-32 text-xs font-medium">{areaLabel(area)}</span>
              <Select
                value={level}
                // Only this area travels. What the call leaves out keeps its
                // level, so the other two are not touched.
                onValueChange={(next) =>
                  save.mutate({ [AREA_FIELD[area]]: next as AccessLevel })
                }
                disabled={save.isPending}
              >
                <SelectTrigger className="h-8 w-64 text-xs">
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
            </span>
            <span className="text-muted-foreground pl-34 text-xs">
              {accessHint(area, level)}
            </span>
          </span>
        )
      })}

      {save.isError && (
        <span role="alert" className="text-destructive text-xs">
          {errorText(save.error)}
        </span>
      )}
    </span>
  )
}
