import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { afterEach, describe, expect, test, vi } from 'vitest'

import { i18n } from '@/lib/i18n'
import { CommitmentsPage } from '@/pages/CommitmentsPage'

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
      grantsPlan: 'edit',
      grantsCommitments: 'view',
      grantsAccounts: 'plan',
    },
  ],
}

afterEach(() => vi.unstubAllGlobals())

describe('missing grant', () => {
  test('a member without edit rights on contracts is told whose grant is missing', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) =>
        String(url).endsWith('/households')
          ? new Response(JSON.stringify([household]), { status: 200 })
          : new Response('[]', { status: 200 })
      )
    )
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const router = createMemoryRouter([{ path: '/', element: <CommitmentsPage /> }], {
      initialEntries: ['/?member=u2'],
    })
    render(
      <QueryClientProvider client={client}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    )

    expect(await screen.findByText(i18n.t('commitments.leadMemberView', { name: 'Alex' }))
    ).toBeInTheDocument()
  })
})
