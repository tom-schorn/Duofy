import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { LogOut, Percent, UserPlus } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { DialogFrame } from '@/components/DialogFrame'
import { QuotaDialog } from '@/components/QuotaDialog'
import { ListRow } from '@/components/ListRow'
import { MemberDialog } from '@/components/MemberDialog'
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
import { FormError } from '@/components/FormError'
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
  useDeclineInvitation,
  useHouseholds,
  useInvite,
  useLeaveHousehold,
  useUpdateHousehold,
  useMe,
  useMyInvitations,
  useSetMyAccess,
} from '@/lib/queries'
import {
  accessHint,
  accessLabel,
  ACCESS_ORDER,
  areaLabel,
  AREA_ORDER,
  lowestLevel,
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
 * Everybody belongs to exactly one household, their own or the one they were
 * invited into. The list keeps its shape, but holds one entry.
 *
 * Admins (several possible) invite, remove members, hand the admin role on and
 * set the household's quotas (decisions 58, 60). Rights on somebody's data stay
 * with that person — an admin sets none for others.
 */

/** Catalog keys of the roles. */
const ROLE_LABEL: Record<Role, string> = {
  admin: 'household.roles.admin',
  member: 'household.roles.member',
}

export function HouseholdPage() {
  const { t } = useTranslation()
  const households = useHouseholds()
  const me = useMe()
  const [invitingTo, setInvitingTo] = useState<Household | null>(null)
  // The household a preset is currently offered for: right after joining.
  const [presetFor, setPresetFor] = useState<Household | null>(null)
  const currentUserId = me.data?.id ?? ''
  // After leaving, the card is gone; the focus goes to the page heading (rule 13).
  const heading = useRef<HTMLHeadingElement>(null)
  const [openMember, setOpenMember] = useState<{ householdId: string; member: Member } | null>(
    null
  )
  const isAdminOf = (household: Household) =>
    household.members.some(
      (member) => member.userId === currentUserId && member.role === 'admin'
    )

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-col gap-2">
          <h1
            ref={heading}
            tabIndex={-1}
            className="font-heading text-3xl font-semibold outline-none"
          >
            {t('household.title')}
          </h1>
          <p className="text-muted-foreground max-w-2xl">
            {t('household.lead')}
          </p>
        </div>
      </header>

      <PendingInvitations onJoined={setPresetFor} />

      <QueryState isPending={households.isPending} error={households.error} onRetry={() => void households.refetch()}>
      <ul className="flex flex-col gap-4">
        {households.data?.map((household) => (
          <li
            key={household.id}
            className="bg-card flex flex-col gap-4 rounded-xl p-5 ring-1 ring-foreground/10"
          >
            <HouseholdHeader
              household={household}
              isAdmin={isAdminOf(household)}
              onInvite={() => setInvitingTo(household)}
              onLeft={() => heading.current?.focus()}
              isLastAdmin={
                household.members.filter((member) => member.role === 'admin').length === 1 &&
                household.members.some(
                  (member) => member.role === 'admin' && member.userId === currentUserId
                )
              }
            />

            <ul className="flex flex-col">
              {household.members.map((member) => {
                const isMe = member.userId === currentUserId
                const roleBadge = (
                  <Badge
                    variant={member.role === 'admin' ? 'secondary' : 'outline'}
                    className="font-normal"
                  >
                    {t(ROLE_LABEL[member.role])}
                  </Badge>
                )
                if (!isMe) {
                  // An admin opens the others for their role and to remove them;
                  // everybody else only reads the row (rule 3).
                  return (
                    <ListRow
                      key={member.userId}
                      onOpen={
                        isAdminOf(household)
                          ? () => setOpenMember({ householdId: household.id, member })
                          : undefined
                      }
                      trailing={roleBadge}
                    >
                      <span className="flex items-center gap-3">
                        <Initials member={member} />
                        <span className="flex min-w-0 flex-col">
                          <span className="font-medium">
                            {member.firstName} {member.lastName}
                          </span>
                          <span className="text-muted-foreground truncate text-xs">
                            {member.email}
                          </span>
                        </span>
                      </span>
                      {/* What I see of them, as text: only they set it. */}
                      <span className="text-muted-foreground flex flex-col gap-0.5 pl-11 text-xs">
                        {AREA_ORDER.map((area) => (
                          <span key={area}>
                            {areaLabel(area)}:{' '}
                            {accessLabel(area, member.grantsToMe[area]).toLowerCase()}
                          </span>
                        ))}
                      </span>
                    </ListRow>
                  )
                }
                return (
                  <li
                    key={member.userId}
                    className="border-border/60 flex flex-wrap items-center gap-3 border-b py-2.5 last:border-b-0"
                  >
                    <Initials member={member} />

                    <span className="flex min-w-0 flex-col">
                      <span className="flex flex-wrap items-center gap-2">
                        <span className="font-medium">
                          {member.firstName} {member.lastName}
                        </span>
                        <Badge variant="outline" className="font-normal">
                          {t('household.you')}
                        </Badge>
                      </span>
                      <span className="text-muted-foreground truncate text-xs">
                        {member.email}
                      </span>
                    </span>

                    <span className="ml-auto">{roleBadge}</span>

                    {/* Die Freigabe steht bei der eigenen Zeile, weil man nur
                        die eigene setzen kann. Bei den anderen steht sie als
                        Text da — man soll sehen, was man von ihnen sieht. */}
                    <span className="w-full pl-11">
                      <AccessChoice household={household} myId={member.userId} />
                    </span>
                  </li>
                )
              })}
            </ul>
          </li>
        ))}
      </ul>
      </QueryState>

      <SharingPresetDialog
        household={presetFor}
        onOpenChange={(open) => !open && setPresetFor(null)}
      />

      <InviteDialog
        household={invitingTo}
        onOpenChange={(open) => !open && setInvitingTo(null)}
      />

      <MemberDialog
        householdId={openMember?.householdId ?? ''}
        member={openMember?.member ?? null}
        onOpenChange={(open) => !open && setOpenMember(null)}
        // A removed member's row is gone; the heading takes the focus (rule 13).
        returnFocus={() => heading.current}
      />
    </div>
  )
}

function Initials({ member }: { member: Member }) {
  return (
    <span className="bg-muted text-muted-foreground flex size-8 shrink-0 items-center justify-center rounded-full text-xs font-semibold">
      {member.firstName[0]}
      {member.lastName[0]}
    </span>
  )
}

function HouseholdHeader({
  household,
  isAdmin,
  onInvite,
  onLeft,
  isLastAdmin,
}: {
  household: Household
  /** Quotas and inviting are the admins' (decisions 58, 60); the others do not see them. */
  isAdmin: boolean
  onInvite: () => void
  onLeft: () => void
  /** The only admin cannot leave; the dialog says so instead of offering it. */
  isLastAdmin: boolean
}) {
  const leave = useLeaveHousehold()
  const update = useUpdateHousehold()
  const [quotaOpen, setQuotaOpen] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const left = useRef(false)
  const { t } = useTranslation()

  return (
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-heading text-xl font-semibold">
          {household.name}
        </span>
        <span className="text-muted-foreground text-sm">
          {t('household.memberCount', { number: household.members.length })}
        </span>
        <span className="text-muted-foreground text-sm">
          {t('quota.current', {
            needs: Number(household.targetNeeds),
            wants: Number(household.targetWants),
            savings: Number(household.targetSavings),
          })}
        </span>
      </div>

      <div className="flex items-center gap-2">
        {isAdmin && (
          <>
            <Button variant="outline" size="sm" onClick={() => setQuotaOpen(true)}>
              <Percent className="size-4" />
              {t('quota.change')}
            </Button>

            <QuotaDialog
              open={quotaOpen}
              onOpenChange={setQuotaOpen}
              title={t('quota.householdTitle')}
              description={t('quota.householdDescription')}
              initial={household}
              pending={update.isPending}
              error={update.isError ? update.error : null}
              onSave={(values) =>
                update.mutate(
                  { id: household.id, ...values },
                  { onSuccess: () => setQuotaOpen(false) }
                )
              }
            />

            <Button variant="outline" size="sm" onClick={onInvite}>
              <UserPlus className="size-4" />
              {t('household.invite')}
            </Button>
          </>
        )}

        {/* Wer austritt, sieht die gemeinsamen Pläne nicht mehr, und die anderen
            sehen seine Posten dort in keinem Monat mehr, auch nicht in
            vergangenen. Gelöscht wird nichts; eigene Pläne, Konten und Verträge
            bleiben bei der Person. */}
        <Button
          variant="outline"
          size="sm"
          aria-label={t('household.leaveFrom', { name: household.name })}
          onClick={() => setConfirming(true)}
        >
          <LogOut className="size-4" />
          {t('household.leave')}
        </Button>

        <AlertDialog open={confirming} onOpenChange={setConfirming}>
          <AlertDialogContent
            onCloseAutoFocus={(event) => {
              // The household card is gone; the heading takes the focus (rule 13).
              if (left.current) {
                event.preventDefault()
                onLeft()
              }
            }}
          >
            <AlertDialogHeader>
              <AlertDialogTitle className="font-heading">
                {t('household.leaveTitle', { name: household.name })}
              </AlertDialogTitle>
              <AlertDialogDescription>
                {isLastAdmin ? t('errors.last_admin_cannot_leave') : t('household.leaveText')}
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              {/* Focus starts on the safe button (rule 13). */}
              <AlertDialogCancel>{t('common.cancel')}</AlertDialogCancel>
              {!isLastAdmin && (
              <AlertDialogAction
                disabled={leave.isPending}
                onClick={(event) => {
                  // Stays open until the server has said yes.
                  event.preventDefault()
                  leave.mutate(household.id, {
                    onSuccess: () => {
                      left.current = true
                      setConfirming(false)
                    },
                  })
                }}
              >
                {t('household.leave')}
              </AlertDialogAction>
              )}
            </AlertDialogFooter>
            {leave.isError && <FormError error={leave.error} />}
          </AlertDialogContent>
        </AlertDialog>
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

  // Every opening starts empty: closing without saving must really discard.
  const resetInvite = invite.reset
  const isOpen = household !== null
  useEffect(() => {
    if (isOpen) {
      setEmail('')
      resetInvite()
    }
  }, [isOpen, resetInvite])

  return (
    <DialogFrame
      open={household !== null}
      onOpenChange={onOpenChange}
      title={t('household.inviteTitle', { name: household?.name })}
      description={t('household.inviteDescription')}
      submitLabel={t('household.invite')}
      pendingLabel={t('household.sending')}
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
      dirty={email !== ''}
      pending={invite.isPending}
      error={invite.isError ? invite.error : null}
    >
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
    </DialogFrame>
  )
}


/**
 * The inbox for invitations.
 *
 * There is no email and no link anybody has to forward: whoever signs in with the
 * invited address finds the invitation here. Shows nothing while none is pending.
 */
function PendingInvitations({ onJoined }: { onJoined: (household: Household) => void }) {
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
              onClick={() => accept.mutate(invitation.token, { onSuccess: onJoined })}
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

function SharingPresetDialog({
  household,
  onOpenChange,
}: {
  household: Household | null
  onOpenChange: (open: boolean) => void
}) {
  const { t } = useTranslation()
  const me = useMe().data
  const save = useSetMyAccess(
    household?.id ?? '',
    (household?.members ?? [])
      .map((member) => member.userId)
      .filter((userId) => userId !== me?.id)
  )

  return (
    <DialogFrame
      open={household !== null}
      onOpenChange={onOpenChange}
      title={t('household.presetTitle', { name: household?.name })}
      description={t('household.presetDescription')}
      submitLabel={t('household.presetApply')}
      onSubmit={(event) => {
        event.preventDefault()
        save.mutate(
          Object.fromEntries(AREA_ORDER.map((area) => [area, 'edit'])),
          { onSuccess: () => onOpenChange(false) }
        )
      }}
      pending={save.isPending}
      error={save.isError ? save.error : null}
    >
      <p className="text-sm font-medium">{t('household.presetCouple')}</p>
      <p className="text-muted-foreground text-xs">{t('household.presetHint')}</p>
    </DialogFrame>
  )
}

/**
 * What I release to the others about myself.
 *
 * Deliberately here and not in the settings: the decision concerns exactly the
 * people listed next to it. Whoever makes it has to see who they are giving it to.
 *
 * The server keeps a level per person; until the grants page lets me choose per
 * person, this card sets the same level for everybody and shows the lowest one I
 * gave anybody — never more than everybody has.
 */
function AccessChoice({ household, myId }: { household: Household; myId: string }) {
  const others = household.members.filter((member) => member.userId !== myId)
  const save = useSetMyAccess(
    household.id,
    others.map((member) => member.userId)
  )
  const { t } = useTranslation()

  return (
    <span className="flex flex-col gap-3">
      <span className="text-muted-foreground text-xs">{t('household.youShare')}</span>

      {AREA_ORDER.map((area) => {
        const level = lowestLevel(others.map((member) => member.myGrants[area]))
        return (
          <span key={area} className="flex flex-col gap-1">
            <span className="flex flex-wrap items-center gap-2">
              <span className="w-32 text-xs font-medium">{areaLabel(area)}</span>
              <Select
                value={level}
                // Only this area travels. What the call leaves out keeps its
                // level, so the other areas are not touched.
                onValueChange={(next) => save.mutate({ [area]: next as AccessLevel })}
                disabled={save.isPending || others.length === 0}
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
