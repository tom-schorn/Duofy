import { useNavigate } from 'react-router'
import { ChevronsUpDown, LogOut, Settings } from 'lucide-react'

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
import { useQueryClient } from '@tanstack/react-query'

import { clearToken } from '@/lib/api'
import { useMe } from '@/lib/queries'

export function UserMenu() {
  const { isMobile } = useSidebar()
  const navigate = useNavigate()
  const client = useQueryClient()
  const me = useMe()

  const firstName = me.data?.firstName ?? ''
  const lastName = me.data?.lastName ?? ''
  const initials = `${firstName[0] ?? ''}${lastName[0] ?? ''}` || '··'

  function handleLogout() {
    // Token first, cache second — otherwise somebody else data flashes up when the
    // next person signs in right away.
    clearToken()
    client.clear()
    navigate('/login', { replace: true })
  }

  return (
    <SidebarMenu>
      <SidebarMenuItem>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <SidebarMenuButton
              size="lg"
              className="data-[state=open]:bg-sidebar-accent"
            >
              <div className="bg-sidebar-accent text-sidebar-accent-foreground flex aspect-square size-8 items-center justify-center rounded-full text-xs font-semibold">
                {initials}
              </div>
              <div className="grid flex-1 text-left leading-tight">
                <span className="truncate font-medium">
                  {firstName} {lastName}
                </span>
                <span className="text-muted-foreground truncate text-xs">
                  {me.data?.email ?? ''}
                </span>
              </div>
              <ChevronsUpDown className="ml-auto size-4" />
            </SidebarMenuButton>
          </DropdownMenuTrigger>

          <DropdownMenuContent
            className="w-(--radix-dropdown-menu-trigger-width) min-w-56"
            align="end"
            side={isMobile ? 'bottom' : 'right'}
          >
            <DropdownMenuLabel className="text-muted-foreground text-xs">
              Konto
            </DropdownMenuLabel>

            {/* TODO: Route /einstellungen bauen — Name, E-Mail, Passwort ändern. */}
            <DropdownMenuItem className="gap-2">
              <Settings className="size-4 shrink-0" />
              Einstellungen
            </DropdownMenuItem>

            <DropdownMenuSeparator />

            <DropdownMenuItem onSelect={handleLogout} className="gap-2">
              <LogOut className="size-4 shrink-0" />
              Abmelden
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </SidebarMenuItem>
    </SidebarMenu>
  )
}
