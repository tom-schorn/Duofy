import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { i18n } from '@/lib/i18n'
import { CommitmentsPage } from '@/pages/CommitmentsPage'

function commitment(id: string, name: string, deletable: boolean) {
  return {
    id,
    deletable,
    type: 'contract',
    name,
    amount: '500.00',
    category: 'housing.rent',
    budget: 'needs',
    isLimit: false,
    isPrivate: false,
    intervalMonths: 1,
    firstDueDate: '2026-10-01',
    endsOn: null,
    passThrough: false,
    counterAccountId: null,
    targetAmount: null,
    targetDate: null,
    paymentMethod: null,
    accountId: null,
  }
}

const rows = [commitment('c1', 'Miete', true), commitment('c2', 'Strom', false)]
let fetchMock: ReturnType<typeof vi.fn>

function renderPage(path = '/') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter([{ path: '/', element: <CommitmentsPage /> }], {
    initialEntries: [path],
  })
  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  )
}

describe('CommitmentsPage', () => {
  beforeEach(() => {
    fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
      if (init?.method === 'DELETE') return new Response(null, { status: 204 })
      if (String(url).includes('/commitments')) {
        return new Response(JSON.stringify(rows), { status: 200 })
      }
      return new Response('[]', { status: 200 })
    })
    vi.stubGlobal('fetch', fetchMock)
  })
  afterEach(() => vi.unstubAllGlobals())

  test('the status filter comes from the address, so it survives a reload', async () => {
    renderPage('/?status=ended')
    await waitFor(() =>
      expect(fetchMock.mock.calls.some(([url]) => String(url).includes('status=ended'))).toBe(true)
    )
    expect(screen.getByRole('combobox', { name: i18n.t('commitments.filterLabel') })).toHaveTextContent(
      i18n.t('commitments.status.ended')
    )
  })

  test('a row has no menu, and a click on it opens the edit dialog', async () => {
    const user = userEvent.setup()
    renderPage()
    await user.click(await screen.findByRole('button', { name: /^Miete/ }))
    expect(screen.getByRole('dialog', { name: i18n.t('commitmentDialog.types.contract.editTitle') })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /weitere Aktionen/ })).not.toBeInTheDocument()
  })

  test('the dialog offers Delete only for a commitment that is in no plan yet', async () => {
    const user = userEvent.setup()
    renderPage()
    await user.click(await screen.findByRole('button', { name: /^Strom/ }))
    expect(screen.getByRole('dialog')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: i18n.t('common.delete') })).not.toBeInTheDocument()
  })

  test('Delete asks once, deletes on confirm and puts the focus on the page heading', async () => {
    const user = userEvent.setup()
    renderPage()
    await user.click(await screen.findByRole('button', { name: /^Miete/ }))
    await user.click(screen.getByRole('button', { name: i18n.t('common.delete') }))
    // Nothing is deleted before the question is answered.
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'DELETE')).toBe(false)
    const confirm = screen.getByRole('alertdialog')
    await user.click(within(confirm).getByRole('button', { name: i18n.t('common.delete') }))
    await waitFor(() =>
      expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'DELETE')).toBe(true)
    )
    await waitFor(() =>
      expect(screen.getByRole('heading', { level: 1, name: i18n.t('commitments.title') })).toHaveFocus()
    )
  })
})

describe('CommitmentsPage for the person chosen in the sidebar (#254)', () => {
  afterEach(() => vi.unstubAllGlobals())

  test('?member= loads the contracts of that person, not your own', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        const target = String(url)
        if (!target.includes('/commitments')) return new Response('[]', { status: 200 })
        const own = [commitment('c1', 'Miete', true)]
        const partner = [commitment('c7', 'Fitnessstudio', true)]
        return new Response(JSON.stringify(target.includes('owner=u2') ? partner : own), {
          status: 200,
        })
      })
    )
    renderPage('/?member=u2')
    expect(await screen.findByText('Fitnessstudio')).toBeInTheDocument()
    expect(screen.queryByText('Miete')).not.toBeInTheDocument()
  })
})

describe('CommitmentsPage ending a running contract (#237)', () => {
  test('the end chosen in the dialog reaches the server as endsOn, the start stays', async () => {
    const running = { ...commitment('c9', 'Streaming', true), firstDueDate: '2026-08-05' }
    const sent: { url: string; body: unknown }[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string, init?: RequestInit) => {
        if (init?.method === 'PATCH') {
          sent.push({ url: String(url), body: JSON.parse(String(init.body)) })
          return new Response(JSON.stringify(running), { status: 200 })
        }
        if (String(url).includes('/commitments')) {
          return new Response(JSON.stringify([running]), { status: 200 })
        }
        return new Response('[]', { status: 200 })
      })
    )
    const user = userEvent.setup()
    renderPage()
    await user.click(await screen.findByRole('button', { name: /^Streaming/ }))
    await user.click(screen.getByRole('button', { name: i18n.t('commitmentDialog.noEnd') }))
    const panel = screen.getByRole('group', { name: i18n.t('commitmentDialog.endsOnLabel') })
    await user.click(within(panel).getByRole('button', { name: 'Dezember' }))
    await user.click(screen.getByRole('button', { name: 'Speichern' }))
    await waitFor(() => expect(sent).toHaveLength(1))
    expect(sent[0].body).toMatchObject({ endsOn: '2026-12-01', firstDueDate: '2026-08-05' })
    vi.unstubAllGlobals()
  })
})
