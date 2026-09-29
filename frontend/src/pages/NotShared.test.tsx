import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { afterEach, describe, expect, test, vi } from 'vitest'

import { i18n } from '@/lib/i18n'
import { AccountsPage } from '@/pages/AccountsPage'
import { CommitmentsPage } from '@/pages/CommitmentsPage'
import { ImportPage } from '@/pages/ImportPage'
import { PlansPage } from '@/pages/PlansPage'

/**
 * Nothing shared at all (level `plan`) used to look exactly like an empty list,
 * or — once #201 added error boxes — like any other failure, complete with an
 * "Erneut versuchen" that could never do anything: the grant is not coming back
 * from a retry. Below `view`, every one of these pages now says whose it is and
 * that it is not shared, in the shape of the page's normal empty state (#217).
 */

const household = {
  id: 'h1',
  name: 'Zuhause',
  members: [
    {
      userId: 'u2',
      firstName: 'Alex',
      lastName: 'Test',
      email: 'alex@example.org',
      role: 'member',
      grantsToMe: {
        plan: 'none',
        book: 'none',
        accounts: 'none',
        commitments: 'none',
        import: 'none',
      },
      myGrants: { plan: 'none', book: 'none', accounts: 'none', commitments: 'none', import: 'none' },
    },
  ],
}

function renderAt(path: string, element: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter([{ path: '/', element }], { initialEntries: [path] })
  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  )
}

function stubForbidden(matches: (url: string) => boolean) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) => {
      const address = String(url)
      if (address.endsWith('/households')) {
        return new Response(JSON.stringify([household]), { status: 200 })
      }
      if (matches(address)) {
        return new Response(JSON.stringify({ detail: { code: 'no_insight_granted' } }), {
          status: 403,
        })
      }
      return new Response('[]', { status: 200 })
    })
  )
}

afterEach(() => vi.unstubAllGlobals())

describe('nothing shared at all', () => {
  test('contracts: the honest sentence replaces the list, with no retry button', async () => {
    stubForbidden((url) => url.includes('/commitments'))
    renderAt('/?member=u2', <CommitmentsPage />)
    expect(
      await screen.findByText(i18n.t('commitments.notShared', { name: 'Alex' }))
    ).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: i18n.t('common.retry') })).not.toBeInTheDocument()
    // Below `view` the "nur ansehen" banner would contradict the honest
    // sentence above — it belongs only at exactly `view` (#217 follow-up).
    expect(
      screen.queryByText(i18n.t('commitments.leadMemberView', { name: 'Alex' }))
    ).not.toBeInTheDocument()
  })

  test('accounts: the honest sentence replaces the list, with no retry button', async () => {
    stubForbidden((url) => url.includes('/accounts'))
    renderAt('/?member=u2', <AccountsPage />)
    expect(
      await screen.findByText(i18n.t('accounts.notShared', { name: 'Alex' }))
    ).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: i18n.t('common.retry') })).not.toBeInTheDocument()
    expect(
      screen.queryByText(i18n.t('accounts.leadMemberView', { name: 'Alex' }))
    ).not.toBeInTheDocument()
  })

  test('import: the honest sentence replaces the table, with no retry button', async () => {
    stubForbidden((url) => url.includes('/imports'))
    renderAt('/?member=u2', <ImportPage />)
    expect(
      await screen.findByText(i18n.t('import.notShared', { name: 'Alex' }))
    ).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: i18n.t('common.retry') })).not.toBeInTheDocument()
  })

  test('plans: the honest sentence replaces the list, with no retry button', async () => {
    stubForbidden((url) => url.includes('/plans'))
    renderAt('/?member=u2', <PlansPage />)
    expect(
      await screen.findByText(i18n.t('plan.notShared', { name: 'Alex' }))
    ).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: i18n.t('common.retry') })).not.toBeInTheDocument()
    expect(
      screen.queryByText(i18n.t('plans.leadMemberView', { name: 'Alex' }))
    ).not.toBeInTheDocument()
  })
})
