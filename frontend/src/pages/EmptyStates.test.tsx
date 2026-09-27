import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { i18n } from '@/lib/i18n'
import { AccountsPage } from '@/pages/AccountsPage'
import { CommitmentsPage } from '@/pages/CommitmentsPage'
import { ImportPage } from '@/pages/ImportPage'
import { PlansPage } from '@/pages/PlansPage'

function renderPage(element: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter([{ path: '/', element }])
  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  )
}

describe('empty states', () => {
  beforeEach(() => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response('[]', { status: 200 }))
    )
  })
  afterEach(() => vi.unstubAllGlobals())

  test.each([
    ['plans', <PlansPage key="p" />, 'plans.empty', 'plans.create'],
    ['contracts', <CommitmentsPage key="c" />, 'commitments.empty', 'commitments.create'],
    ['accounts', <AccountsPage key="a" />, 'accounts.empty', 'accounts.create'],
    ['import', <ImportPage key="i" />, 'import.empty', 'import.upload'],
  ])('%s: the empty list carries the main button of the page', async (_name, page, text, button) => {
    renderPage(page)
    const sentence = await screen.findByText(i18n.t(text), {}, { timeout: 20000 })
    const box = sentence.closest('[data-slot=empty]') as HTMLElement
    expect(box).not.toBeNull()
    expect(within(box).getByRole('button', { name: i18n.t(button) })).toBeInTheDocument()
  })

  test('a failed load shows an alert that runs the request again', async () => {
    const user = userEvent.setup()
    let failed = false
    const fetchMock = vi.fn(async (url: string) => {
      if (String(url).includes('/accounts') && !failed) {
        failed = true
        return new Response(JSON.stringify({ code: 'not_allowed' }), { status: 403 })
      }
      return new Response('[]', { status: 200 })
    })
    vi.stubGlobal('fetch', fetchMock)
    renderPage(<AccountsPage />)
    const alert = await screen.findByRole('alert')
    await user.click(within(alert).getByRole('button', { name: i18n.t('common.retry') }))
    expect(await screen.findByText(i18n.t('accounts.empty'))).toBeInTheDocument()
  })
})
