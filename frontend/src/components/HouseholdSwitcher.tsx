import { useState } from 'react'
import { Link } from 'react-router'
import { Check, ChevronsUpDown, Plus, Users, User } from 'lucide-react'

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  useSidebar,
} from '@/components/ui/sidebar'
import { useHouseholds } from '@/lib/queries'

/**
 * Wechselt die Brille, nicht die Welt.
 *
 * Ein Plan gehört immer einer Person — ein Haushaltsplan ist keine eigene
 * Tabelle, sondern die Zusammenstellung aller Posten aller Mitglieder mit
 * gesetzter `household_id`. Dieselbe Miete ist gleichzeitig ein eigener und
 * ein Haushaltsposten.
 *
 * Ein Nutzer kann in mehreren Haushalten sein (WG und Partnerin gleichzeitig)
 * — die Liste ist deshalb offen.
 */

type Scope = {
  id: string
  name: string
  /** null = „Mein Plan", sonst die household_id. */
  householdId: string | null
  hint: string
}

const OWN: Scope = {
  id: 'own',
  name: 'Mein Plan',
  householdId: null,
  hint: 'Nur meine Posten',
}

export function HouseholdSwitcher() {
  const { isMobile } = useSidebar()
  const households = useHouseholds()

  // TODO: Auswahl global halten (Zustand-Store) und in der URL spiegeln,
  // damit ein Link auf einen Haushaltsplan teilbar bleibt. Erst dann kann
  // die Planungsseite auf /plans/household/... umschalten.
  const [activeId, setActiveId] = useState('own')

  const scopes: Scope[] = [
    OWN,
    ...(households.data ?? []).map((household) => ({
      id: household.id,
      name: household.name,
      householdId: household.id,
      hint: `${household.members.length} Mitglieder`,
    })),
  ]
  const active = scopes.find((scope) => scope.id === activeId) ?? OWN

  const Icon = active.householdId === null ? User : Users

  return (
    <SidebarMenu>
      <SidebarMenuItem>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <SidebarMenuButton
              size="lg"
              className="data-[state=open]:bg-sidebar-accent"
            >
              <div className="bg-sidebar-primary text-sidebar-primary-foreground flex aspect-square size-8 items-center justify-center rounded-md">
                <Icon className="size-4" />
              </div>
              <div className="grid flex-1 text-left leading-tight">
                <span className="truncate font-medium">{active.name}</span>
                <span className="text-muted-foreground truncate text-xs">
                  {active.hint}
                </span>
              </div>
              <ChevronsUpDown className="ml-auto size-4" />
            </SidebarMenuButton>
          </DropdownMenuTrigger>

          <DropdownMenuContent
            className="w-(--radix-dropdown-menu-trigger-width) min-w-56"
            align="start"
            side={isMobile ? 'bottom' : 'right'}
          >
            <DropdownMenuLabel className="text-muted-foreground text-xs">
              Ansicht
            </DropdownMenuLabel>

            {scopes.map((scope) => (
              <DropdownMenuItem
                key={scope.id}
                onSelect={() => setActiveId(scope.id)}
                className="gap-2"
              >
                {scope.householdId === null ? (
                  <User className="size-4 shrink-0" />
                ) : (
                  <Users className="size-4 shrink-0" />
                )}
                {scope.name}
                {scope.id === active.id && <Check className="ml-auto size-4" />}
              </DropdownMenuItem>
            ))}

            <DropdownMenuSeparator />

            {/* Angelegt wird auf der Haushaltsseite — hier nur der Weg dahin. */}
            <DropdownMenuItem asChild className="gap-2">
              <Link to="/household">
                <Plus className="size-4 shrink-0" />
                Haushalt verwalten
              </Link>
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </SidebarMenuItem>
    </SidebarMenu>
  )
}
