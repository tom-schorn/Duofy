import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { beforeAll, describe, expect, test } from 'vitest'

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

describe('AppLayout', () => {
  // jsdom has no matchMedia; the sidebar asks it whether the screen is narrow.
  beforeAll(() => {
    window.matchMedia = ((query: string) => ({
      matches: false,
      media: query,
      addEventListener: () => {},
      removeEventListener: () => {},
    })) as unknown as typeof window.matchMedia
  })

  test('each sidebar entry is one link, with no button inside it', () => {
    renderAt('/book')
    const link = screen.getByRole('link', { name: de.nav.commitments })
    expect(link.querySelector('button')).toBeNull()
    expect(link.closest('button')).toBeNull()
  })

  test('the header shows the title of the page, not a placeholder', () => {
    renderAt('/contracts')
    expect(screen.getByText(de.nav.commitments, { selector: 'header span' })).toBeInTheDocument()
  })
})
