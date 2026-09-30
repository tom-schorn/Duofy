import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { Toaster } from '@/components/ui/sonner'
import { i18n } from '@/lib/i18n'
import { GrantsPage } from '@/pages/GrantsPage'

const NONE = { plan: 'none', book: 'none', accounts: 'none', commitments: 'none', import: 'none' }

function member(userId: string, firstName: string, role: string, myGrants = NONE) {
  return {
    userId,
    firstName,
    lastName: 'Test',
    email: `${userId}@example.org`,
    role,
    grantsToMe: NONE,
    myGrants,
  }
}

let members: ReturnType<typeof member>[]
let putStatus: number
let fetchMock: ReturnType<typeof vi.fn>

const puts = () =>
  fetchMock.mock.calls
    .filter(([, init]) => init?.method === 'PUT')
    .map(([url, init]) => ({ url: String(url), body: JSON.parse(init.body as string) }))

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter([{ path: '/', element: <GrantsPage /> }])
  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
      <Toaster />
    </QueryClientProvider>
  )
}

const card = async (name: string) => screen.findByRole('region', { name: `${name} Test` })

beforeEach(() => {
  putStatus = 200
  members = [
    member('u1', 'Ida', 'member'),
    member('u2', 'Max', 'admin', {
      plan: 'delete',
      book: 'delete',
      accounts: 'delete',
      commitments: 'delete',
      import: 'delete',
    }),
    member('u3', 'Kim', 'member', { ...NONE, plan: 'view', book: 'create' }),
  ]
  fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
    const path = String(url)
    if (init?.method === 'PUT') {
      if (putStatus !== 200) {
        return new Response(JSON.stringify({ detail: { code: 'not_a_member' } }), {
          status: putStatus,
        })
      }
      const target = members.find((m) => path.endsWith(`/grants/${m.userId}`))!
      target.myGrants = { ...target.myGrants, ...JSON.parse(init.body as string) }
      return new Response(JSON.stringify(target), { status: 200 })
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

describe('grants page', () => {
  test('shows one card per other member with role, but none for me', async () => {
    renderPage()
    const max = await card('Max')
    expect(within(max).getByText(i18n.t('household.roles.admin'))).toBeInTheDocument()
    expect(within(await card('Kim')).getByText(i18n.t('household.roles.member'))).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Ida Test' })).not.toBeInTheDocument()
  })

  test('marks the matching preset as pressed and says own choice when none matches', async () => {
    renderPage()
    const max = await card('Max')
    const partner = within(max).getByRole('button', { name: i18n.t('grants.presets.partner') })
    expect(partner).toHaveAttribute('aria-pressed', 'true')
    expect(within(max).queryByText(i18n.t('grants.ownChoice'))).not.toBeInTheDocument()

    const kim = await card('Kim')
    expect(within(kim).getByText(i18n.t('grants.ownChoice'))).toBeInTheDocument()
    for (const button of within(kim).getAllByRole('button')) {
      expect(button).toHaveAttribute('aria-pressed', 'false')
    }
  })

  test('a preset writes all five levels for that one person and reports it', async () => {
    const user = userEvent.setup()
    renderPage()
    const max = await card('Max')
    await user.click(within(max).getByRole('button', { name: i18n.t('grants.presets.read') }))

    await waitFor(() => expect(puts()).toHaveLength(1))
    expect(puts()[0]).toEqual({
      url: expect.stringMatching(/\/households\/h1\/grants\/u2$/),
      body: { plan: 'view', book: 'view', accounts: 'view', commitments: 'view', import: 'none' },
    })
    expect(await screen.findByText(i18n.t('toast.grantUpdated', { name: 'Max' }))).toBeInTheDocument()
    expect(
      within(max).getByRole('button', { name: i18n.t('grants.presets.read') })
    ).toHaveAttribute('aria-pressed', 'true')
  })

  test('one select sends only its area, and focus stays on it', async () => {
    const user = userEvent.setup()
    renderPage()
    const max = await card('Max')
    const accounts = within(max).getByRole('combobox', { name: i18n.t('enums.area.accounts') })
    await user.click(accounts)
    await user.click(
      await screen.findByRole('option', { name: i18n.t('enums.access.accounts.view') })
    )

    await waitFor(() => expect(puts()).toHaveLength(1))
    expect(puts()[0].body).toEqual({ accounts: 'view' })
    expect(accounts).toHaveTextContent(i18n.t('enums.access.accounts.view'))
    expect(await within(max).findByText(i18n.t('grants.ownChoice'))).toBeInTheDocument()
    await waitFor(() => expect(accounts).toHaveFocus())
  })

  test('points out dependent rights without changing them', async () => {
    renderPage()
    const kim = await card('Kim')
    expect(
      within(kim).getByText(i18n.t('grants.hints.bookNeedsAccounts', { name: 'Kim' }))
    ).toBeInTheDocument()
    expect(
      within(kim).getByRole('combobox', { name: i18n.t('enums.area.accounts') })
    ).toHaveTextContent(i18n.t('enums.access.accounts.none'))
    expect(puts()).toHaveLength(0)
  })

  test('a refused save stays visible with a retry and keeps the old level', async () => {
    putStatus = 404
    const user = userEvent.setup()
    renderPage()
    const max = await card('Max')
    await user.click(within(max).getByRole('button', { name: i18n.t('grants.presets.none') }))

    expect(await screen.findByText(i18n.t('errors.not_a_member'))).toBeInTheDocument()
    expect(screen.getByRole('button', { name: i18n.t('errors.retry') })).toBeInTheDocument()
    expect(
      within(max).getByRole('button', { name: i18n.t('grants.presets.partner') })
    ).toHaveAttribute('aria-pressed', 'true')
  })

  test('says so when nobody else is in the household', async () => {
    members = [member('u1', 'Ida', 'admin')]
    renderPage()
    expect(await screen.findByText(i18n.t('grants.empty'))).toBeInTheDocument()
  })

  test('presets form a named group per person', async () => {
    renderPage()
    await card('Max')
    expect(
      screen.getByRole('group', { name: i18n.t('grants.presetsFor', { name: 'Max' }) })
    ).toBeInTheDocument()
  })
})
