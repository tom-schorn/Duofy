import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { i18n } from '@/lib/i18n'
import { HouseholdPage } from '@/pages/HouseholdPage'

const NONE = { plan: 'none', book: 'none', accounts: 'none', commitments: 'none', import: 'none' }

const member = (userId: string, firstName: string, myGrants = NONE, role = 'member') => ({
  userId,
  firstName,
  lastName: 'Test',
  email: `${userId}@example.org`,
  role,
  grantsToMe: NONE,
  myGrants,
})

let members: ReturnType<typeof member>[]
let fetchMock: ReturnType<typeof vi.fn>
let patchStatus: number

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
  patchStatus = 200
  members = [member('u1', 'Ida', NONE, 'admin'), member('u2', 'Max'), member('u3', 'Kim')]
  fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
    const path = String(url)
    if (init?.method === 'PATCH') {
      const body =
        patchStatus === 200 ? { ...members[0], role: 'member' } : { detail: { code: 'last_admin_required' } }
      return new Response(JSON.stringify(body), { status: patchStatus })
    }
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

describe('stepping down as admin', () => {
  const stepDown = () => screen.findByRole('button', { name: i18n.t('household.stepDown') })
  const patches = () => fetchMock.mock.calls.filter(([, init]) => init?.method === 'PATCH')

  test('an admin beside another admin gives the role up after one question', async () => {
    members[1] = member('u2', 'Max', NONE, 'admin')
    const user = userEvent.setup()
    renderPage()
    await user.click(await stepDown())

    const dialog = await screen.findByRole('alertdialog')
    // Focus starts on the safe button (rule 13).
    expect(within(dialog).getByRole('button', { name: i18n.t('common.cancel') })).toHaveFocus()
    await user.click(within(dialog).getByRole('button', { name: i18n.t('household.stepDown') }))

    await waitFor(() => expect(patches()).toHaveLength(1))
    const [url, init] = patches()[0]
    expect(String(url)).toMatch(/\/households\/h1\/members\/u1$/)
    expect(JSON.parse(init.body as string)).toEqual({ role: 'member' })
    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument())
  })

  test('the last admin is offered no way to step down', async () => {
    renderPage()
    await screen.findByRole('link', { name: i18n.t('household.grants') })
    expect(screen.queryByRole('button', { name: i18n.t('household.stepDown') })).not.toBeInTheDocument()
  })

  test('a refusal stays in the dialog', async () => {
    members[1] = member('u2', 'Max', NONE, 'admin')
    patchStatus = 409
    const user = userEvent.setup()
    renderPage()
    await user.click(await stepDown())
    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: i18n.t('household.stepDown') }))

    expect(await within(dialog).findByText(i18n.t('errors.last_admin_required'))).toBeInTheDocument()
  })
})
