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
  members: [
    {
      userId: 'u1',
      firstName: 'Ida',
      lastName: 'Test',
      email: 'ida@example.org',
      role: 'owner',
      grantsPlan: 'plan',
      grantsCommitments: 'plan',
      grantsAccounts: 'plan',
    },
  ],
}

let fetchMock: ReturnType<typeof vi.fn>

const patchCalls = () =>
  fetchMock.mock.calls.filter(([, init]) => init?.method === 'PATCH')

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
      if (init?.method === 'PATCH') return new Response(JSON.stringify({}), { status: 200 })
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

    await waitFor(() => expect(patchCalls()).toHaveLength(1))
    const [url, init] = patchCalls()[0]
    expect(String(url)).toMatch(/\/households\/h1\/members\/me$/)
    expect(JSON.parse(init.body as string)).toEqual({
      grantsPlan: 'edit',
      grantsCommitments: 'edit',
      grantsAccounts: 'edit',
    })
  })

  test('declining the preset keeps the default and changes nothing', async () => {
    const user = userEvent.setup()
    renderPage()
    await user.click(await screen.findByRole('button', { name: i18n.t('household.join') }))
    await screen.findByRole('button', { name: i18n.t('household.presetApply') })
    await user.click(screen.getByRole('button', { name: i18n.t('common.cancel') }))

    expect(patchCalls()).toHaveLength(0)
  })
})
