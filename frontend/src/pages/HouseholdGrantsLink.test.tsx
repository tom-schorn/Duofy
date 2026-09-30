import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { i18n } from '@/lib/i18n'
import { HouseholdPage } from '@/pages/HouseholdPage'

const NONE = { plan: 'none', book: 'none', accounts: 'none', commitments: 'none', import: 'none' }

const member = (userId: string, firstName: string, myGrants = NONE) => ({
  userId,
  firstName,
  lastName: 'Test',
  email: `${userId}@example.org`,
  role: userId === 'u1' ? 'admin' : 'member',
  grantsToMe: NONE,
  myGrants,
})

let members: ReturnType<typeof member>[]
let fetchMock: ReturnType<typeof vi.fn>

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(
    [
      { path: '/household', element: <HouseholdPage /> },
      { path: '/household/grants', element: <h1>grants page</h1> },
    ],
    { initialEntries: ['/household'] }
  )
  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  )
}

beforeEach(() => {
  members = [member('u1', 'Ida'), member('u2', 'Max'), member('u3', 'Kim')]
  fetchMock = vi.fn(async (url: string) => {
    const path = String(url)
    if (path.endsWith('/users/me')) {
      return new Response(JSON.stringify({ id: 'u1', firstName: 'Ida' }), { status: 200 })
    }
    if (path.endsWith('/households')) {
      return new Response(
        JSON.stringify([
          {
            id: 'h1',
            name: 'Zuhause',
            targetNeeds: '50.00',
            targetWants: '30.00',
            targetSavings: '20.00',
            members,
          },
        ]),
        { status: 200 }
      )
    }
    return new Response('[]', { status: 200 })
  })
  vi.stubGlobal('fetch', fetchMock)
})
afterEach(() => vi.unstubAllGlobals())

describe('grants on the household page', () => {
  test('names everybody who has no grant from me yet', async () => {
    renderPage()
    expect(
      await screen.findByText(i18n.t('household.noGrantYetMany', { names: 'Max und Kim' }))
    ).toBeInTheDocument()
  })

  test('drops the sentence for whoever has any grant from me', async () => {
    members[1] = member('u2', 'Max', { ...NONE, plan: 'view' })
    renderPage()
    expect(
      await screen.findByText(i18n.t('household.noGrantYetOne', { names: 'Kim' }))
    ).toBeInTheDocument()
  })

  test('offers no level selects any more, only the way to the grants page', async () => {
    const user = userEvent.setup()
    renderPage()
    const link = await screen.findByRole('link', { name: i18n.t('household.grants') })
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument()
    await user.click(link)
    expect(await screen.findByRole('heading', { name: 'grants page' })).toBeInTheDocument()
  })
})
