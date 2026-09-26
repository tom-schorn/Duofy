import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { afterEach, describe, expect, test, vi } from 'vitest'

import { LegalLinks } from '@/components/LegalLinks'
import { i18n } from '@/lib/i18n'
import { LegalPage } from '@/pages/LegalPage'
import { NotFoundPage } from '@/pages/NotFoundPage'

function stub(documents: string[]) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) => {
      const path = String(url)
      if (path.endsWith('/legal')) {
        return new Response(JSON.stringify({ documents }), { status: 200 })
      }
      const name = path.split('/').pop() ?? ''
      return documents.includes(name)
        ? new Response(JSON.stringify({ text: 'Beispiel GmbH' }), { status: 200 })
        : new Response(JSON.stringify({ detail: { code: 'legal_not_found' } }), { status: 404 })
    })
  )
}

function renderAt(element: React.ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={createMemoryRouter([{ path: '/', element }])} />
    </QueryClientProvider>
  )
}

afterEach(() => vi.unstubAllGlobals())

describe('legal texts of the instance', () => {
  test('the footer links exactly the configured documents', async () => {
    stub(['imprint', 'terms'])
    renderAt(<LegalLinks />)
    expect(await screen.findByRole('link', { name: i18n.t('legal.imprint') })).toHaveAttribute(
      'href',
      '/impressum'
    )
    expect(screen.getByRole('link', { name: i18n.t('legal.terms') })).toHaveAttribute('href', '/agb')
    expect(screen.queryByRole('link', { name: i18n.t('legal.privacy') })).toBeNull()
  })

  test('without configuration the footer shows nothing', async () => {
    stub([])
    renderAt(<LegalLinks />)
    await new Promise((resolve) => setTimeout(resolve, 50))
    expect(screen.queryByRole('navigation')).toBeNull()
  })

  test('the page shows the configured text', async () => {
    stub(['imprint'])
    renderAt(<LegalPage name="imprint" />)
    expect(await screen.findByText('Beispiel GmbH')).toBeInTheDocument()
    expect(
      screen.getByRole('heading', { level: 1, name: i18n.t('legal.imprint') })
    ).toBeInTheDocument()
  })

  test('the not-found page carries the legal footer too', async () => {
    stub(['imprint'])
    renderAt(<NotFoundPage />)
    expect(await screen.findByRole('link', { name: i18n.t('legal.imprint') })).toBeInTheDocument()
  })

  test('a failing server is shown as an error, not as a missing page', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 500 })))
    renderAt(<LegalPage name="imprint" />)
    expect(await screen.findByRole('alert')).toBeInTheDocument()
    expect(screen.queryByText(i18n.t('notFound.title'))).toBeNull()
  })

  test('the page does not exist without a configured text', async () => {
    stub([])
    renderAt(<LegalPage name="imprint" />)
    expect(await screen.findByText(i18n.t('notFound.title'))).toBeInTheDocument()
  })
})
