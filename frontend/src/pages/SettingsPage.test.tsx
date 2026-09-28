import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, MemoryRouter, RouterProvider } from 'react-router'
import { afterEach, describe, expect, test, vi } from 'vitest'

import { i18n } from '@/lib/i18n'
import { SettingsPage } from '@/pages/SettingsPage'

afterEach(() => vi.unstubAllGlobals())

const ME = {
  id: 'u',
  email: 'a@example.org',
  firstName: 'A',
  lastName: 'B',
  targetNeeds: '50.00',
  targetWants: '30.00',
  targetSavings: '20.00',
  flowLimitsBy: 'plan',
  isSuperuser: false,
}

function open() {
  const fetchMock = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) => new Response(JSON.stringify(ME)))
  vi.stubGlobal('fetch', fetchMock)
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <MemoryRouter initialEntries={['/einstellungen']}>
        <SettingsPage />
      </MemoryRouter>
    </QueryClientProvider>
  )
  return fetchMock
}

describe('settings page', () => {
  test('shows the stored quotas as three sliders', async () => {
    open()
    expect(await screen.findByLabelText(i18n.t('quota.needs'))).toHaveValue('50')
    expect(screen.getByLabelText(i18n.t('quota.wants'))).toHaveValue('30')
    expect(screen.getByLabelText(i18n.t('quota.savings'))).toHaveValue('20')
  })

  test('saves the coupled values, which still add up to 100', async () => {
    const user = userEvent.setup()
    const fetchMock = open()
    const savings = await screen.findByLabelText(i18n.t('quota.savings'))
    const save = screen.getByRole('button', { name: 'Speichern' })
    expect(save).toBeDisabled()

    fireEvent.change(savings, { target: { value: '30' } })
    await user.click(save)

    const patch = fetchMock.mock.calls.find(([, init]) => init?.method === 'PATCH')
    expect(JSON.parse(String(patch?.[1]?.body))).toEqual({
      targetNeeds: '43.75',
      targetWants: '26.25',
      targetSavings: '30',
    })
  })

  test('offers the setting for limits in the flow', async () => {
    open()
    expect(await screen.findByLabelText(i18n.t('monthFlow.limitsBy'))).toBeInTheDocument()
  })
})

/**
 * `open()` above wires no `/login` route, so the deletion flow — which navigates
 * there — gets its own render with a router that has both.
 */
function openForDeletion(deleteStatus = 204) {
  const fetchMock = vi.fn(async (url: RequestInfo | URL, init?: RequestInit) => {
    const path = String(url)
    if (init?.method === 'DELETE' && path.endsWith('/users/me')) {
      if (deleteStatus !== 204) {
        return new Response(JSON.stringify({ detail: { code: 'last_admin' } }), {
          status: deleteStatus,
        })
      }
      return new Response(null, { status: 204 })
    }
    return new Response(JSON.stringify(ME), { status: 200 })
  })
  vi.stubGlobal('fetch', fetchMock)
  const router = createMemoryRouter(
    [
      { path: '/einstellungen', element: <SettingsPage /> },
      { path: '/login', element: <p>Anmeldeseite</p> },
    ],
    { initialEntries: ['/einstellungen'] }
  )
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  )
  return { fetchMock, router }
}

describe('deleting the account', () => {
  test('the confirmation names what disappears, what stays, and household handover', async () => {
    const user = userEvent.setup()
    openForDeletion()
    await user.click(await screen.findByRole('button', { name: i18n.t('settings.deleteAccount') }))
    expect(screen.getByRole('alertdialog')).toBeInTheDocument()
    expect(screen.getByText(i18n.t('settings.deleteAccountText'))).toBeInTheDocument()
  })

  test('confirming deletes the account, clears the session and lands on the login page', async () => {
    const user = userEvent.setup()
    const { fetchMock, router } = openForDeletion()
    await user.click(await screen.findByRole('button', { name: i18n.t('settings.deleteAccount') }))
    const confirm = screen.getByRole('alertdialog')
    await user.click(within(confirm).getByRole('button', { name: i18n.t('settings.deleteAccount') }))

    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(
          ([url, init]) => init?.method === 'DELETE' && String(url).endsWith('/users/me')
        )
      ).toBe(true)
    )
    await waitFor(() => expect(router.state.location.pathname).toBe('/login'))
  })

  test('the last admin sees why deletion is refused, and the dialog stays open', async () => {
    const user = userEvent.setup()
    const { router } = openForDeletion(409)
    await user.click(await screen.findByRole('button', { name: i18n.t('settings.deleteAccount') }))
    const confirm = screen.getByRole('alertdialog')
    await user.click(within(confirm).getByRole('button', { name: i18n.t('settings.deleteAccount') }))

    expect(await screen.findByText(i18n.t('errors.last_admin'))).toBeInTheDocument()
    expect(screen.getByRole('alertdialog')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/einstellungen')
  })
})
