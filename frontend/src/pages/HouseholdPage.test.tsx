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
let me = 'u2'
let deleteStatus = 204

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter([{ path: '/', element: <HouseholdPage /> }])
  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  )
}

const leaveButton = () =>
  screen.findByRole('button', { name: i18n.t('household.leaveFrom', { name: household.name }) })

const leaveCalled = () =>
  fetchMock.mock.calls.some(([, init]) => init?.method === 'DELETE')

describe('HouseholdPage', () => {
  beforeEach(() => {
    left = false
    me = 'u2'
    deleteStatus = 204
    fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
      const path = String(url)
      if (init?.method === 'DELETE') {
        if (deleteStatus !== 204) {
          return new Response(JSON.stringify({ detail: { code: 'last_owner_cannot_leave' } }), {
            status: deleteStatus,
          })
        }
        left = true
        return new Response(null, { status: 204 })
      }
      if (path.endsWith('/users/me')) {
        return new Response(JSON.stringify({ id: me, firstName: 'Max' }), { status: 200 })
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
    expect(await leaveButton()).toBeInTheDocument()
    expect(screen.getByRole('button', { name: i18n.t('household.invite') })).toBeInTheDocument()
    expect(document.querySelector('[aria-haspopup]')).toBeNull()
    expect(screen.queryByLabelText(/weitere Aktionen/)).toBeNull()
  })

  test('the confirmation says the positions leave in every month and do not come back on re-joining', async () => {
    const user = userEvent.setup()
    renderPage()
    await user.click(await leaveButton())
    const dialog = screen.getByRole('alertdialog')
    expect(dialog).toHaveTextContent(/in keinem Monat, auch nicht in vergangenen/)
    expect(dialog).toHaveTextContent(/Wiedereintritt kommen sie nicht von selbst zurück/)
  })

  test('leaving asks first, starts on Abbrechen, leaves on confirm and focuses the page heading', async () => {
    const user = userEvent.setup()
    renderPage()
    await user.click(await leaveButton())
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
    await user.click(await leaveButton())
    await user.click(
      within(screen.getByRole('alertdialog')).getByRole('button', { name: i18n.t('common.cancel') })
    )
    expect(leaveCalled()).toBe(false)
  })

  test('the only owner is told so up front and is not offered the confirm button', async () => {
    me = 'u1'
    const user = userEvent.setup()
    renderPage()
    await user.click(await leaveButton())
    const dialog = screen.getByRole('alertdialog')
    expect(within(dialog).getByText(i18n.t('errors.last_owner_cannot_leave'))).toBeInTheDocument()
    expect(within(dialog).queryByRole('button', { name: i18n.t('household.leave') })).not.toBeInTheDocument()
    expect(within(dialog).getByRole('button', { name: i18n.t('common.cancel') })).toBeInTheDocument()
  })

  test('a refusal from the server stays in the dialog, once, and the dialog stays open', async () => {
    deleteStatus = 409
    const user = userEvent.setup()
    renderPage()
    await user.click(await leaveButton())
    const dialog = screen.getByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: i18n.t('household.leave') }))
    await waitFor(() =>
      expect(within(dialog).getByRole('alert')).toHaveTextContent(
        i18n.t('errors.last_owner_cannot_leave')
      )
    )
    expect(screen.getByRole('alertdialog')).toBeInTheDocument()
    expect(screen.getAllByText(i18n.t('errors.last_owner_cannot_leave'))).toHaveLength(1)
  })
})
