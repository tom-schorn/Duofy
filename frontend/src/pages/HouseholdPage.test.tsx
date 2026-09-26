import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { i18n } from '@/lib/i18n'
import { HouseholdPage } from '@/pages/HouseholdPage'

const household = {
  id: 'h1',
  name: 'Zuhause',
  members: [
    {
      userId: 'u1',
      firstName: 'Ida',
      lastName: 'Test',
      email: 'ida@example.org',
      role: 'owner',
      grantsPlan: 'view',
      grantsCommitments: 'none',
      grantsAccounts: 'none',
    },
    {
      userId: 'u2',
      firstName: 'Max',
      lastName: 'Test',
      email: 'max@example.org',
      role: 'member',
      grantsPlan: 'view',
      grantsCommitments: 'none',
      grantsAccounts: 'none',
    },
  ],
}

let fetchMock: ReturnType<typeof vi.fn>
let left = false

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter([{ path: '/', element: <HouseholdPage /> }])
  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  )
}

const leaveCalled = () =>
  fetchMock.mock.calls.some(([, init]) => init?.method === 'DELETE')

describe('HouseholdPage', () => {
  beforeEach(() => {
    left = false
    fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
      const path = String(url)
      if (init?.method === 'DELETE') {
        left = true
        return new Response(null, { status: 204 })
      }
      if (path.endsWith('/users/me')) {
        return new Response(JSON.stringify({ id: 'u2', firstName: 'Max' }), { status: 200 })
      }
      if (path.endsWith('/households')) {
        return new Response(JSON.stringify(left ? [] : [household]), { status: 200 })
      }
      return new Response('[]', { status: 200 })
    })
    vi.stubGlobal('fetch', fetchMock)
  })
  afterEach(() => vi.unstubAllGlobals())

  test('there is no ⋯ menu; leaving is a visible button next to inviting', async () => {
    renderPage()
    expect(
      await screen.findByRole('button', { name: i18n.t('household.leave') })
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: i18n.t('household.invite') })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /verwalten|weitere Aktionen/ })).not.toBeInTheDocument()
  })

  test('leaving asks first, starts on Abbrechen, leaves on confirm and focuses the page heading', async () => {
    const user = userEvent.setup()
    renderPage()
    await user.click(await screen.findByRole('button', { name: i18n.t('household.leave') }))
    const dialog = screen.getByRole('alertdialog')
    expect(leaveCalled()).toBe(false)
    expect(within(dialog).getByRole('button', { name: i18n.t('common.cancel') })).toHaveFocus()
    await user.click(within(dialog).getByRole('button', { name: i18n.t('household.leave') }))
    await waitFor(() => expect(leaveCalled()).toBe(true))
    await waitFor(() =>
      expect(screen.getByRole('heading', { level: 1, name: i18n.t('household.title') })).toHaveFocus()
    )
  })

  test('Abbrechen leaves nothing', async () => {
    const user = userEvent.setup()
    renderPage()
    await user.click(await screen.findByRole('button', { name: i18n.t('household.leave') }))
    await user.click(
      within(screen.getByRole('alertdialog')).getByRole('button', { name: i18n.t('common.cancel') })
    )
    expect(leaveCalled()).toBe(false)
  })
})
