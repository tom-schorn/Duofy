import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Label } from '@/components/ui/label'
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
import { DialogFrame } from '@/components/DialogFrame'
import { FormError } from '@/components/FormError'
import { useRemoveMember, useSetMemberRole } from '@/lib/queries'
import type { Member } from '@/lib/domain'

/**
 * Another member, opened by an admin (decisions 58, 68): the admin role and
 * removing them. Rights on their data are not here — nobody sets those for
 * somebody else.
 *
 * Removing is consequential, so it asks once (UI guideline rule 7) and sits left
 * in the footer (rule 6). Afterwards the row is gone; the focus goes to the page
 * heading via `returnFocus`.
 */
export function MemberDialog({
  householdId,
  member,
  onOpenChange,
  returnFocus,
}: {
  householdId: string
  member: Member | null
  onOpenChange: (open: boolean) => void
  returnFocus: () => HTMLElement | null
}) {
  const { t } = useTranslation()
  const setRole = useSetMemberRole(householdId)
  const remove = useRemoveMember(householdId)
  const [admin, setAdmin] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const [removed, setRemoved] = useState(false)

  const isOpen = member !== null
  const resetRole = setRole.reset
  const resetRemove = remove.reset
  useEffect(() => {
    if (isOpen) {
      setAdmin(member.role === 'admin')
      setRemoved(false)
      resetRole()
      resetRemove()
    }
    // Only a fresh opening starts over; the member object changes on every reload.
  }, [isOpen, member?.userId]) // eslint-disable-line react-hooks/exhaustive-deps

  const name = member?.firstName ?? ''

  return (
    <>
      <DialogFrame
        open={isOpen}
        onOpenChange={onOpenChange}
        title={t('household.memberTitle', { name: `${name} ${member?.lastName ?? ''}`.trim() })}
        submitLabel={t('common.save')}
        onSubmit={(event) => {
          event.preventDefault()
          if (!member) return
          setRole.mutate(
            { userId: member.userId, role: admin ? 'admin' : 'member' },
            { onSuccess: () => onOpenChange(false) }
          )
        }}
        dirty={member !== null && admin !== (member.role === 'admin')}
        pending={setRole.isPending}
        error={setRole.isError ? setRole.error : null}
        returnFocus={() => (removed ? returnFocus() : null)}
        start={
          <Button
            type="button"
            variant="ghost"
            className="text-destructive hover:text-destructive"
            disabled={setRole.isPending}
            onClick={() => setConfirming(true)}
          >
            {t('household.remove')}
          </Button>
        }
      >
        <div className="flex flex-col gap-2">
          <div className="flex items-center gap-2">
            <Checkbox
              id="member-admin"
              checked={admin}
              onCheckedChange={(checked) => setAdmin(checked === true)}
            />
            <Label htmlFor="member-admin">{t('household.adminRole')}</Label>
          </div>
          <p className="text-muted-foreground text-xs">{t('household.adminRoleHint')}</p>
        </div>
      </DialogFrame>

      <AlertDialog open={confirming} onOpenChange={setConfirming}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle className="font-heading">
              {t('household.removeTitle', { name })}
            </AlertDialogTitle>
            <AlertDialogDescription>{t('household.removeText', { name })}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            {/* Focus starts on the safe button (rule 13). */}
            <AlertDialogCancel>{t('common.cancel')}</AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              disabled={remove.isPending}
              onClick={(event) => {
                // Stays open until the server has said yes.
                event.preventDefault()
                if (!member) return
                remove.mutate(member, {
                  onSuccess: () => {
                    setRemoved(true)
                    setConfirming(false)
                    onOpenChange(false)
                  },
                })
              }}
            >
              {t('household.remove')}
            </AlertDialogAction>
          </AlertDialogFooter>
          {remove.isError && <FormError error={remove.error} />}
        </AlertDialogContent>
      </AlertDialog>
    </>
  )
}
