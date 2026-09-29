import { Fragment } from 'react'
import { Home } from 'lucide-react'
import { NavLink, Outlet, useLocation, useSearchParams } from 'react-router'
import { useTranslation } from 'react-i18next'

import { HelpButton, HelpColumn } from '@/components/HelpPanel'
import { MemberSwitcher } from '@/components/MemberSwitcher'
import { useHelpPinned, useIsWide } from '@/lib/help-state'
import { LegalLinks } from '@/components/LegalLinks'
import { NAV, titleKeyFor } from '@/lib/nav'
import { useHouseholds } from '@/lib/queries'

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
 * The shell around everything behind the sign-in.
 *
 * Sidebar and top bar stay put, `<Outlet />` swaps the middle. Accounts sit between
 * commitments and household: private like the commitments, but not something one
 * plans with.
 */

export function AppLayout() {
  const { t } = useTranslation()
  const { pathname } = useLocation()
  const titleKey = titleKeyFor(pathname)
  const help = useHelpPinned()
  const wide = useIsWide()
  const household = (useHouseholds().data ?? [])[0]
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
                  <Fragment key={item.to}>
                  <SidebarMenuItem>
                    {/* Die gewählte Person reist mit. Ohne das fiele man beim
                        ersten Klick auf „Verträge" wieder auf sich selbst
                        zurück, ohne dass es jemand ansagt. */}
                    <SidebarMenuButton
                      asChild
                      isActive={pathname === item.to && (item.to !== '/plan' || active === null)}
                      tooltip={t(item.label)}
                    >
                      <NavLink to={{ pathname: item.to, search: navSearch }} end>
                        <item.icon className="size-4" />
                        <span>{t(item.label)}</span>
                      </NavLink>
                    </SidebarMenuButton>
                  </SidebarMenuItem>

                  {/* Jede Person ist in genau einem Haushalt, also gibt es genau
                      einen Menüpunkt dafür. Ein Menüpunkt ist ein Ort, den man
                      ansteuert und verlinken kann — ein Umschalter änderte
                      unsichtbar, was alle Seiten zeigen. Er öffnet die
                      Monatsliste des Haushalts, wie „Meine Planung" die eigene
                      öffnet. */}
                  {item.to === '/plan' && household !== undefined && (
                    <SidebarMenuItem>
                      <SidebarMenuButton
                        asChild
                        isActive={active === household.id}
                        tooltip={t('nav.ourHousehold')}
                      >
                        <NavLink to={`/plan?household=${household.id}`}>
                          <Home className="size-4" />
                          <span>{t('nav.ourHousehold')}</span>
                        </NavLink>
                      </SidebarMenuButton>
                    </SidebarMenuItem>
                  )}
                  </Fragment>
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

          {/* Der Seitentitel, aus dem Katalog — kein fester Platzhalter (Regel 20). */}
          <span className="font-heading text-lg font-semibold">
            {titleKey === null ? '' : t(titleKey)}
          </span>
          <span className="ml-auto" />
          <ThemeToggle />
          <HelpButton pinned={help.pinned} onPin={help.setPinned} wide={wide} />
        </header>

        {/* Die Erklärspalte steht neben dem Inhalt, nicht darüber, und nur, wenn
            sie angeheftet ist und der Platz ab 1280 px reicht. Sonst öffnet der
            Knopf „?“ in der Kopfzeile sie als Seitenblatt. */}
        <div className="flex flex-1 items-start">
          <div className="min-w-0 flex-1 p-6">
            <Outlet />
            <LegalLinks className="mt-10" />
          </div>
          <HelpColumn pinned={help.pinned} onPin={help.setPinned} />
        </div>
      </SidebarInset>
    </SidebarProvider>
  )
}
