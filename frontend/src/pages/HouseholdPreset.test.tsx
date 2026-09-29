import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { i18n } from '@/lib/i18n'
import { HouseholdPage } from '@/pages/HouseholdPage'

const household = {
  id: 'h1',
  name: 'Zuhause',
  targetNeeds: '50.00',
  targetWants: '30.00',
  targetSavings: '20.00',
  members: [
    {
      userId: 'u1',
      firstName: 'Ida',
      lastName: 'Test',
      email: 'ida@example.org',
      role: 'member',
      grantsToMe: { plan: 'none', book: 'none', accounts: 'none', commitments: 'none', import: 'none' },
      myGrants: { plan: 'none', book: 'none', accounts: 'none', commitments: 'none', import: 'none' },
    },
    {
      userId: 'u2',
      firstName: 'Max',
      lastName: 'Test',
      email: 'max@example.org',
      role: 'admin',
      grantsToMe: { plan: 'none', book: 'none', accounts: 'none', commitments: 'none', import: 'none' },
      myGrants: { plan: 'none', book: 'none', accounts: 'none', commitments: 'none', import: 'none' },
    },
  ],
}

let fetchMock: ReturnType<typeof vi.fn>

const grantCalls = () =>
  fetchMock.mock.calls.filter(([, init]) => init?.method === 'PUT')

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter([{ path: '/', element: <HouseholdPage /> }])
  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  )
}

describe('sharing preset after joining', () => {
  beforeEach(() => {
    fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
      const path = String(url)
      if (init?.method === 'POST' && path.endsWith('/accept')) {
        return new Response(JSON.stringify(household), { status: 200 })
      }
      if (init?.method === 'PUT') return new Response(JSON.stringify({}), { status: 200 })
      if (path.endsWith('/users/me')) {
        return new Response(JSON.stringify({ id: 'u1', firstName: 'Ida' }), { status: 200 })
      }
      if (path.endsWith('/households/invitations')) {
        return new Response(
          JSON.stringify([
            {
              token: 't1',
              householdName: 'Zuhause',
              invitedBy: 'Max',
              expiresAt: '2026-12-01T00:00:00Z',
            },
          ]),
          { status: 200 }
        )
      }
      if (path.endsWith('/households')) {
        return new Response(JSON.stringify([household]), { status: 200 })
      }
      return new Response('[]', { status: 200 })
    })
    vi.stubGlobal('fetch', fetchMock)
  })
  afterEach(() => vi.unstubAllGlobals())

  test('accepting offers the couple preset, which sets only my own levels', async () => {
    const user = userEvent.setup()
    renderPage()
    await user.click(await screen.findByRole('button', { name: i18n.t('household.join') }))
    await user.click(await screen.findByRole('button', { name: i18n.t('household.presetApply') }))

    // One call per other member, granting from me — never for somebody else.
    await waitFor(() => expect(grantCalls()).toHaveLength(1))
    const [url, init] = grantCalls()[0]
    expect(String(url)).toMatch(/\/households\/h1\/grants\/u2$/)
    expect(JSON.parse(init.body as string)).toEqual({
      plan: 'edit',
      book: 'edit',
      accounts: 'edit',
      commitments: 'edit',
      import: 'edit',
    })
  })

  test('declining the preset keeps the default and changes nothing', async () => {
    const user = userEvent.setup()
    renderPage()
    await user.click(await screen.findByRole('button', { name: i18n.t('household.join') }))
    await screen.findByRole('button', { name: i18n.t('household.presetApply') })
    await user.click(screen.getByRole('button', { name: i18n.t('common.cancel') }))

    expect(grantCalls()).toHaveLength(0)
  })
})
