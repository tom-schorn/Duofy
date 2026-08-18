import { NavLink, Outlet, useSearchParams } from 'react-router'

import { MemberSwitcher } from '@/components/MemberSwitcher'
import { useHouseholds } from '@/lib/queries'

import { CalendarRange, FileText, Users, Wallet } from 'lucide-react'

import { ThemeToggle } from '@/components/ThemeToggle'
import { UserMenu } from '@/components/UserMenu'
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarHeader,
  SidebarInset,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarMenuSub,
  SidebarMenuSubButton,
  SidebarMenuSubItem,
  SidebarProvider,
  SidebarRail,
  SidebarTrigger,
} from '@/components/ui/sidebar'
import { Separator } from '@/components/ui/separator'

/**
 * The shell around everything behind the sign-in.
 *
 * Sidebar and top bar stay put, `<Outlet />` swaps the middle. Accounts sit between
 * commitments and household: private like the commitments, but not something one
 * plans with.
 */

/**
 * One page for every commitment — savings plans and loans are commitments too.
 *
 * This used to be three entries split by `Commitment.type`. That was the wrong
 * axis: a commitment can sit in **any** block (rent → needs, streaming → wants,
 * a savings plan → savings). `type` only says whether the thing has an end — a
 * property, not a navigation point.
 */
const NAV = [
  { to: '/plan', label: 'Planung', icon: CalendarRange },
  { to: '/contracts', label: 'Verträge', icon: FileText },
  { to: '/accounts', label: 'Konten', icon: Wallet },
  { to: '/household', label: 'Haushalt', icon: Users },
]

export function AppLayout() {
  const households = useHouseholds().data ?? []
  // The sub-entry points at the current month — there is no "current" household
  // plan otherwise, it is composed from positions.
  const now = new Date()
  const [params] = useSearchParams()
  const active = params.get('household')
  const activeMember = params.get('member')
  const navSearch = activeMember === null ? '' : `?member=${activeMember}`

  return (
    <SidebarProvider>
      <Sidebar collapsible="icon">
        <SidebarHeader className="gap-2">
          <span className="font-heading px-2 pt-1 text-lg font-semibold group-data-[collapsible=icon]:hidden">
            Duofy
          </span>
          <MemberSwitcher />
        </SidebarHeader>

        <SidebarContent>
          <SidebarGroup>
            <SidebarGroupContent>
              <SidebarMenu>
                {NAV.map((item) => (
                  <SidebarMenuItem key={item.to}>
                    {/* Die gewählte Person reist mit. Ohne das fiele man beim
                        ersten Klick auf „Verträge" wieder auf sich selbst
                        zurück, ohne dass es jemand ansagt. */}
                    <NavLink to={{ pathname: item.to, search: navSearch }} end>
                      {({ isActive }) => (
                        <SidebarMenuButton
                          isActive={isActive}
                          tooltip={item.label}
                        >
                          <item.icon className="size-4" />
                          <span>{item.label}</span>
                        </SidebarMenuButton>
                      )}
                    </NavLink>

                    {/* Die gemeinsamen Pläne hängen unter „Haushalt", statt
                        hinter einem Umschalter zu verschwinden. Ein Menüpunkt
                        ist ein Ort, den man ansteuert und verlinken kann —
                        ein Umschalter ändert unsichtbar, was alle Seiten
                        zeigen. */}
                    {item.to === '/household' && households.length > 0 && (
                      <SidebarMenuSub>
                        {households.map((household) => (
                          <SidebarMenuSubItem key={household.id}>
                            <NavLink
                              to={`/plan/${now.getFullYear()}/${now.getMonth() + 1}?household=${household.id}`}
                            >
                              <SidebarMenuSubButton
                                isActive={active === household.id}
                              >
                                <span>{household.name}</span>
                              </SidebarMenuSubButton>
                            </NavLink>
                          </SidebarMenuSubItem>
                        ))}
                      </SidebarMenuSub>
                    )}
                  </SidebarMenuItem>
                ))}
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
        </SidebarContent>

        <SidebarFooter>
          <UserMenu />
        </SidebarFooter>

        <SidebarRail />
      </Sidebar>

      <SidebarInset>
        <header className="bg-background sticky top-0 z-10 flex h-14 shrink-0 items-center gap-3 border-b px-4">
          <SidebarTrigger className="-ml-1" />
          {/* data-vertical:self-center hebt das self-stretch der Komponente auf,
              sonst klebt der Trenner am oberen Rand. */}
          <Separator
            orientation="vertical"
            className="mr-1 data-vertical:h-5 data-vertical:self-center"
          />

          {/* Periode und Status stehen auf der Detailseite — hier wären sie
              doppelt und auf der Übersicht schlicht falsch.

              TODO: Brotkrumen einsetzen, die dem aktiven Bereich folgen
              („Planung / Juli 2026"). */}

          {/* TODO: Mitglieder des aktiven Haushalts anzeigen, mit Hinweis wer
              den Monat schon bestätigt hat. */}
          <span className="text-muted-foreground ml-auto text-sm">T · J</span>
          <ThemeToggle />
        </header>

        <div className="flex-1 p-6">
          <Outlet />
        </div>
      </SidebarInset>
    </SidebarProvider>
  )
}
