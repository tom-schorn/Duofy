import { useState } from 'react'
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
} from '@/lib/queries'
import type { Household, Role } from '@/lib/domain'

/**
 * Der Haushalt als Planungsebene.
 *
 * Der Haushalt **besitzt nichts** — keine Konten, keine Verträge, keine
 * Posten. Er sagt nur, wer zusammen plant. Deshalb steht hier auch keine
 * Zahl: die Beträge stehen im Plan, nicht am Haushalt.
 *
 * Ein Nutzer kann in mehreren Haushalten sein (WG und Partnerin
 * gleichzeitig) — die Seite listet deshalb alle, nicht nur den aktiven.
 * Welchen Plan man sieht, entscheidet der Umschalter in der Sidebar.
 *
 * TODO: Quoten und Puffer je Haushalt einstellbar machen. Die Spalten gibt
 * es inzwischen (`target_needs` & Co. auf `Household`), die Oberfläche dazu
 * fehlt noch.
 */

const ROLE_LABEL: Record<Role, string> = {
  owner: 'Besitzer',
  member: 'Mitglied',
}

export function HouseholdPage() {
  const households = useHouseholds()
  const me = useMe()
  const [invitingTo, setInvitingTo] = useState<Household | null>(null)
  const currentUserId = me.data?.id ?? ''

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-col gap-2">
          <h1 className="font-heading text-3xl font-semibold">Haushalt</h1>
          <p className="text-muted-foreground max-w-2xl">
            Ein Haushalt sagt nur, wer zusammen plant. Er besitzt nichts —
            Konten und Verträge gehören immer einer Person.
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
            className="bg-card border-border flex flex-col gap-4 rounded-lg border p-5"
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
                            du
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
                      {ROLE_LABEL[member.role]}
                    </Badge>
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
  const me = household.members.find((member) => member.userId === currentUserId)
  const isOwner = me?.role === 'owner'

  return (
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-heading text-xl font-semibold">
          {household.name}
        </span>
        <span className="text-muted-foreground text-sm">
          {household.members.length} Mitglieder
        </span>
      </div>

      <div className="flex items-center gap-2">
        <Button variant="outline" size="sm" onClick={onInvite}>
          <UserPlus className="size-4" />
          Einladen
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
              // TODO: Dialog zum Umbenennen.
              <DropdownMenuItem className="gap-2">
                <Pencil className="size-4" />
                Umbenennen
              </DropdownMenuItem>
            )}
            {/* Die eingebrachten Posten bleiben bestehen und werden wieder
                privat — `household_id` steht auf ON DELETE SET NULL. */}
            <DropdownMenuItem
              variant="destructive"
              className="gap-2"
              onSelect={() => leave.mutate(household.id)}
            >
              <LogOut className="size-4" />
              Austreten
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
              Zu „{household?.name}" einladen
            </DialogTitle>
            <DialogDescription>
              Wer beitritt, sieht ab dann alle Posten, die in diesen Haushalt
              eingebracht wurden — und darf sie ändern.
            </DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-2">
            <Label htmlFor="invite-email">E-Mail</Label>
            <Input
              id="invite-email"
              type="email"
              placeholder="jasmin@beispiel.de"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              required
            />
            <p className="text-muted-foreground text-xs">
              Private Posten bleiben privat. Nur was ausdrücklich dem Haushalt
              zugeordnet ist, wird geteilt.
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
              Abbrechen
            </Button>
            <Button type="submit" disabled={invite.isPending}>
              {invite.isPending ? 'Wird gesendet…' : 'Einladung senden'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}


/**
 * Der Posteingang für Einladungen.
 *
 * Es gibt keinen E-Mail-Versand und keinen Link, den jemand weiterreichen
 * müsste: wer sich mit der eingeladenen Adresse anmeldet, findet die Einladung
 * hier. Zeigt nichts an, solange keine offen ist.
 */
function PendingInvitations() {
  const invitations = useMyInvitations()
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
                {invitation.invitedBy} lädt dich in „{invitation.householdName}"
                ein
              </span>
              <span className="text-muted-foreground text-xs">
                Gültig bis{' '}
                {new Date(invitation.expiresAt).toLocaleDateString('de-DE')}
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
              Ablehnen
            </Button>
            <Button
              size="sm"
              onClick={() => accept.mutate(invitation.token)}
              disabled={accept.isPending || decline.isPending}
            >
              Beitreten
            </Button>
          </span>
        </li>
      ))}

      {(accept.isError || decline.isError) && (
        <li className="text-destructive text-sm">
          {errorText(accept.error ?? decline.error)}
        </li>
      )}
    </ul>
  )
}

function CreateHouseholdButton() {
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const create = useCreateHousehold()

  return (
    <>
      <Button onClick={() => setOpen(true)}>
        <Plus className="size-4" />
        Haushalt anlegen
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
                Haushalt anlegen
              </DialogTitle>
              <DialogDescription>
                Wer anlegt, wird Besitzer. Danach kannst du jemanden einladen.
              </DialogDescription>
            </DialogHeader>

            <div className="flex flex-col gap-2">
              <Label htmlFor="household-name">Name</Label>
              <Input
                id="household-name"
                value={name}
                onChange={(event) => setName(event.target.value)}
                placeholder="Tom & Jasmin"
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
                Abbrechen
              </Button>
              <Button type="submit" disabled={create.isPending}>
                {create.isPending ? 'Wird angelegt\u2026' : 'Anlegen'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </>
  )
}
