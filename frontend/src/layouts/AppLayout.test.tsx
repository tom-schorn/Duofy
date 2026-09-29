import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterAll, beforeAll, describe, expect, test, vi } from 'vitest'

import { TooltipProvider } from '@/components/ui/tooltip'
import de from '@/locales/de.json'
import { AppLayout } from '@/layouts/AppLayout'

function renderAt(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <TooltipProvider>
        <MemoryRouter initialEntries={[path]}>
          <Routes>
            <Route element={<AppLayout />}>
              <Route path="*" element={<p>Seite</p>} />
            </Route>
          </Routes>
        </MemoryRouter>
      </TooltipProvider>
    </QueryClientProvider>
  )
}

const household = {
  id: 'h1',
  name: 'Demo-Haushalt',
  targetNeeds: '50',
  targetWants: '30',
  targetSavings: '20',
  bufferPercent: '10',
  members: [],
}

const me = {
  id: 'me',
  email: 'me@example.com',
  firstName: 'Mia',
  lastName: 'Muster',
  targetNeeds: '50',
  targetWants: '30',
  targetSavings: '20',
  bufferPercent: '10',
  flowLimitsBy: 'planned',
  isSuperuser: false,
}

/** As `renderAt`, but `/households` answers with one household and `/users/me`
 * with a full profile — the sidebar then has something to hang under
 * "Haushalt", and the user menu's quota dialog has a shape to read. */
function renderWithHousehold(path: string) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input)
      if (url.includes('/households')) return new Response(JSON.stringify([household]), { status: 200 })
      if (url.includes('/users/me')) return new Response(JSON.stringify(me), { status: 200 })
      return new Response('[]', { status: 200 })
    })
  )
  return renderAt(path)
}

describe('AppLayout', () => {
  // jsdom has no matchMedia; the sidebar asks it whether the screen is narrow.
  // Everything the shell loads is an empty list.
  beforeAll(() => {
    vi.stubGlobal('matchMedia', (query: string) => ({
      matches: false,
      media: query,
      addEventListener: () => {},
      removeEventListener: () => {},
    }))
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response('[]', { status: 200 }))
    )
  })
  afterAll(() => vi.unstubAllGlobals())

  test('each sidebar entry is one link, with no button inside it', () => {
    renderAt('/book')
    const link = screen.getByRole('link', { name: de.nav.commitments })
    expect(link.querySelector('button')).toBeNull()
    expect(link.closest('button')).toBeNull()
  })

  test('the header shows the title of the page, not a placeholder', () => {
    renderAt('/contracts')
    expect(
      screen.getByText(de.nav.commitments, { selector: 'header span' })
    ).toBeInTheDocument()
  })

  test('"Unser Haushalt" opens the month list of the household, like "Meine Planung" opens the own one (#242)', async () => {
    renderWithHousehold('/household')
    const link = await screen.findByRole('link', { name: de.nav.ourHousehold })
    expect(link).toHaveAttribute('href', `/plan?household=${household.id}`)
  })

  test('"Unser Haushalt" is active on its list and on one of its months, "Meine Planung" is not', async () => {
    renderWithHousehold(`/plan?household=${household.id}`)
    expect(
      await screen.findByRole('link', { name: de.nav.ourHousehold })
    ).toHaveAttribute('data-active', 'true')
    expect(screen.getByRole('link', { name: de.nav.plan })).not.toHaveAttribute(
      'data-active',
      'true'
    )
  })
})
