import { NavLink, Outlet } from 'react-router'

import { ScopeProvider } from '@/lib/scope'
import { CalendarRange, FileText, Users } from 'lucide-react'

import { HouseholdSwitcher } from '@/components/HouseholdSwitcher'
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
  SidebarProvider,
  SidebarRail,
  SidebarTrigger,
} from '@/components/ui/sidebar'
import { Separator } from '@/components/ui/separator'

/**
 * Die Hülle für alles hinter der Anmeldung.
 *
 * Sidebar und Topbar bleiben stehen, `<Outlet />` tauscht den Mittelteil.
 * Konten fehlen bewusst — dafür gibt es im Backend noch kein Modell.
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
  { to: '/household', label: 'Haushalt', icon: Users },
]

export function AppLayout() {
  return (
    // Umschließt Sidebar und Inhalt: der Umschalter sitzt in der Sidebar, die
    // Planungsseite im Outlet — beide brauchen dieselbe Auswahl.
    <ScopeProvider>
    <SidebarProvider>
      <Sidebar collapsible="icon">
        <SidebarHeader>
          <span className="font-heading px-2 pt-1 text-lg font-semibold group-data-[collapsible=icon]:hidden">
            Duofy
          </span>
          <HouseholdSwitcher />
        </SidebarHeader>

        <SidebarContent>
          <SidebarGroup>
            <SidebarGroupContent>
              <SidebarMenu>
                {NAV.map((item) => (
                  <SidebarMenuItem key={item.to}>
                    <NavLink to={item.to}>
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
    </ScopeProvider>
  )
}
