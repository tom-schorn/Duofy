import { NavLink, Outlet, useSearchParams } from 'react-router'

import { useHouseholds, useMe } from '@/lib/queries'

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
 * Die Hülle für alles hinter der Anmeldung.
 *
 * Sidebar und Topbar bleiben stehen, `<Outlet />` tauscht den Mittelteil.
 * Konten stehen zwischen Verträgen und Haushalt: sie sind privat wie die
 * Verträge, aber kein Planungsgegenstand.
 */

/**
 * Eine Seite für alle Verträge — Sparpläne und Kredite sind auch nur Verträge.
 *
 * Früher waren das drei Einträge, aufgeteilt nach `Commitment.type`. Das war
 * die falsche Achse: ein Vertrag kann in **jedem** Budget liegen (Miete →
 * Fixkosten, Streaming → Wünsche, Sparplan → Sparen). `type` sagt nur, ob das
 * Ding ein Ende hat — eine Eigenschaft, kein Navigationspunkt.
 */
const NAV = [
  { to: '/plan', label: 'Planung', icon: CalendarRange },
  { to: '/contracts', label: 'Verträge', icon: FileText },
  { to: '/accounts', label: 'Konten', icon: Wallet },
  { to: '/household', label: 'Haushalt', icon: Users },
]

export function AppLayout() {
  const households = useHouseholds().data ?? []
  const me = useMe().data
  // Der Untereintrag zeigt auf den laufenden Monat — einen „aktuellen"
  // Haushaltsplan gibt es sonst nicht, er wird ja aus Posten zusammengesetzt.
  const now = new Date()
  const [params] = useSearchParams()
  const active = params.get('household')
  const activeMember = params.get('member')

  return (
    <SidebarProvider>
      <Sidebar collapsible="icon">
        <SidebarHeader>
          <span className="font-heading px-2 pt-1 text-lg font-semibold group-data-[collapsible=icon]:hidden">
            Duofy
          </span>
        </SidebarHeader>

        <SidebarContent>
          <SidebarGroup>
            <SidebarGroupContent>
              <SidebarMenu>
                {NAV.map((item) => (
                  <SidebarMenuItem key={item.to}>
                    <NavLink to={item.to} end>
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

                        {/* Personen, die Einblick gegeben haben. Kein
                            Umschalter, sondern ein Ort — dieselbe Begründung
                            wie beim gemeinsamen Plan. Wer nur die gemeinsamen
                            Posten teilt, erscheint hier nicht. */}
                        {households
                          .flatMap((household) => household.members)
                          .filter(
                            (member) =>
                              member.userId !== me?.id &&
                              member.grantsAccess !== 'plan'
                          )
                          .map((member) => (
                            <SidebarMenuSubItem key={member.userId}>
                              <NavLink
                                to={`/plan/${now.getFullYear()}/${now.getMonth() + 1}?member=${member.userId}`}
                              >
                                <SidebarMenuSubButton
                                  isActive={activeMember === member.userId}
                                >
                                  <span>{member.firstName}</span>
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
