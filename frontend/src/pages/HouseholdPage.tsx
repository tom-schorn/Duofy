import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { LogOut, Percent, Plus, UserPlus } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { DialogFrame } from '@/components/DialogFrame'
import { QuotaDialog } from '@/components/QuotaDialog'
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
  useCreateHousehold,
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
 * Every member may change the household's quotas and buffer (#84): the household
 * belongs to nobody, so no role decides for the others.
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
  // The household a preset is currently offered for: right after joining or creating.
  const [presetFor, setPresetFor] = useState<Household | null>(null)
  const currentUserId = me.data?.id ?? ''
  // After leaving, the card is gone; the focus goes to the page heading (rule 13).
  const heading = useRef<HTMLHeadingElement>(null)

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
        <CreateHouseholdButton onCreated={setPresetFor} />
      </header>

      <PendingInvitations onJoined={setPresetFor} />

      <QueryState isPending={households.isPending} error={households.error}>
      <ul className="flex flex-col gap-4">
        {households.data?.map((household) => (
          <li
            key={household.id}
            className="bg-card flex flex-col gap-4 rounded-xl p-5 ring-1 ring-foreground/10"
          >
            <HouseholdHeader
              household={household}
              onInvite={() => setInvitingTo(household)}
              onLeft={() => heading.current?.focus()}
              isLastOwner={
                household.members.filter((member) => member.role === 'owner').length === 1 &&
                household.members.some(
                  (member) => member.role === 'owner' && member.userId === currentUserId
                )
              }
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

      <SharingPresetDialog
        household={presetFor}
        onOpenChange={(open) => !open && setPresetFor(null)}
      />

      <InviteDialog
        household={invitingTo}
        onOpenChange={(open) => !open && setInvitingTo(null)}
      />
    </div>
  )
}

function HouseholdHeader({
  household,
  onInvite,
  onLeft,
  isLastOwner,
}: {
  household: Household
  onInvite: () => void
  onLeft: () => void
  /** The only owner cannot leave; the dialog says so instead of offering it. */
  isLastOwner: boolean
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
            buffer: Number(household.bufferPercent),
          })}
        </span>
      </div>

      <div className="flex items-center gap-2">
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
                {isLastOwner ? t('errors.last_owner_cannot_leave') : t('household.leaveText')}
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              {/* Focus starts on the safe button (rule 13). */}
              <AlertDialogCancel>{t('common.cancel')}</AlertDialogCancel>
              {!isLastOwner && (
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

export function CreateHouseholdButton({
  onCreated,
}: {
  onCreated?: (household: Household) => void
}) {
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const { t } = useTranslation()
  const create = useCreateHousehold()

  // Every opening starts empty: closing without saving must really discard.
  const resetCreate = create.reset
  useEffect(() => {
    if (open) {
      setName('')
      resetCreate()
    }
  }, [open, resetCreate])

  return (
    <>
      <Button onClick={() => setOpen(true)}>
        <Plus className="size-4" />
        {t('household.create')}
      </Button>

      <DialogFrame
        open={open}
        onOpenChange={setOpen}
        title={t('household.create')}
        description={t('household.createDescription')}
        submitLabel={t('common.create')}
        onSubmit={(event) => {
          event.preventDefault()
          create.mutate(
            { name },
            {
              onSuccess: (household) => {
                setName('')
                setOpen(false)
                onCreated?.(household)
              },
            }
          )
        }}
        dirty={name !== ''}
        pending={create.isPending}
        error={create.isError ? create.error : null}
      >
        <div className="flex flex-col gap-2">
          <Label htmlFor="household-name">{t('household.name')}</Label>
          <Input
            id="household-name"
            value={name}
            onChange={(event) => setName(event.target.value)}
            placeholder={t('household.namePlaceholder')}
            required
          />
        </div>
      </DialogFrame>
    </>
  )
}

/**
 * The couple preset, offered once after joining or creating a household.
 *
 * It is a suggestion to the person themselves and sets only their own levels —
 * nobody grants for someone else. Declining keeps the default (`plan` everywhere);
 * every area stays changeable below afterwards.
 */
function SharingPresetDialog({
  household,
  onOpenChange,
}: {
  household: Household | null
  onOpenChange: (open: boolean) => void
}) {
  const { t } = useTranslation()
  const save = useSetMyAccess(household?.id ?? '')

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
          { grantsPlan: 'edit', grantsCommitments: 'edit', grantsAccounts: 'edit' },
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
