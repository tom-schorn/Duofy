import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { Toaster } from '@/components/ui/sonner'
import { categoryGroupLabel, categoryLabel, type AccessLevel } from '@/lib/domain'
import { i18n } from '@/lib/i18n'
import { flushPendingDelete } from '@/lib/undo-delete'
import { ImportPage } from '@/pages/ImportPage'
import { areaLevels } from '@/test/levels'

const baseEntry = {
  id: 'e1',
  accountId: 'a1',
  occurredOn: '2026-09-03',
  counterpartyName: 'Muster Markt',
  purpose: null,
  amount: '12.50',
  incoming: false,
  category: null,
  budget: null,
  positionId: null,
  counterAccountId: null,
  suggestion: null,
  discardedAt: null,
}

/** Alex's household, granting me `level` in the import area. */
function householdGranting(level: AccessLevel) {
  return [
    {
      id: 'h1',
      name: 'Zuhause',
      members: [
        {
          userId: 'u2',
          firstName: 'Alex',
          lastName: 'Test',
          email: 'alex@example.org',
          role: 'member',
          grantsToMe: areaLevels({ accounts: 'view', import: level }),
          myGrants: areaLevels(),
        },
      ],
    },
  ]
}

let entry: Record<string, unknown> = baseEntry
/** The September plan the dialog offers positions from; none by default. */
let plan: Record<string, unknown> | null = null
let importLevel: AccessLevel = 'delete'
let fetchMock: ReturnType<typeof vi.fn>

function renderPage(path = '/') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter([{ path: '/', element: <ImportPage /> }], {
    initialEntries: [path],
  })
  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
      <Toaster />
    </QueryClientProvider>
  )
}

describe('ImportPage', () => {
  beforeEach(() => {
    fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
      if (init?.method === 'DELETE') return new Response(null, { status: 204 })
      if (init?.method === 'PATCH' || init?.method === 'POST') {
        return new Response(JSON.stringify(entry), { status: 200 })
      }
      if (String(url).endsWith('/households')) {
        return new Response(JSON.stringify(householdGranting(importLevel)), { status: 200 })
      }
      if (plan && String(url).includes('/plans/2026/9')) {
        return new Response(JSON.stringify(plan), { status: 200 })
      }
      if (String(url).includes('/imports')) return new Response(JSON.stringify([entry]), { status: 200 })
      return new Response('[]', { status: 200 })
    })
    vi.stubGlobal('fetch', fetchMock)
  })
  afterEach(() => {
    entry = baseEntry
    plan = null
    act(() => flushPendingDelete())
    vi.unstubAllGlobals()
  })

  test('discarding removes the row at once, says verworfen, and undo brings it back without sending anything', async () => {
    const user = userEvent.setup()
    renderPage()
    await user.click(await screen.findByRole('button', { name: i18n.t('import.discard') }))
    expect(screen.queryByText('Muster Markt')).not.toBeInTheDocument()
    expect(await screen.findByText(i18n.t('toast.discardedNamed', { name: 'Muster Markt' }))).toBeInTheDocument()

    await user.click(await screen.findByRole('button', { name: i18n.t('ui.undo') }))
    expect(await screen.findByText('Muster Markt')).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'DELETE')).toBe(false)
  })

  test("import edit books another person's line but only import delete may discard it", async () => {
    importLevel = 'edit'
    renderPage('/?member=u2')
    expect(await screen.findByRole('button', { name: i18n.t('monthBook.book') })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: i18n.t('import.discard') })).not.toBeInTheDocument()
  })

  test("import delete offers discarding another person's line", async () => {
    importLevel = 'delete'
    renderPage('/?member=u2')
    expect(await screen.findByRole('button', { name: i18n.t('import.discard') })).toBeInTheDocument()
  })

  test('Buchen opens the booking dialog even without a category; picking one assigns and then books', async () => {
    const user = userEvent.setup()
    renderPage()
    const book = await screen.findByRole('button', { name: i18n.t('monthBook.book') })
    expect(book).toBeEnabled()
    await user.click(book)
    const dialog = await screen.findByRole('dialog', { name: i18n.t('import.bookTitle') })
    await user.click(within(dialog).getByRole('button', { name: i18n.t('monthBook.chooseCategory') }))
    await user.click(within(dialog).getByRole('button', { name: i18n.t('categoryPicker.placeholder') }))
    await user.click(await screen.findByRole('button', { name: new RegExp(categoryGroupLabel('household')) }))
    await user.click(await screen.findByRole('button', { name: categoryLabel('household.groceries') }))
    await user.click(within(dialog).getByRole('button', { name: i18n.t('monthBook.book') }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    const writes = fetchMock.mock.calls.filter(([, init]) => init?.method === 'PATCH' || init?.method === 'POST')
    expect(writes.map(([url, init]) => `${init?.method} ${String(url).replace(/^.*\/imports/, '/imports')}`)).toEqual([
      'PATCH /imports/e1',
      'POST /imports/e1/book',
    ])
    expect(JSON.parse(String(writes[0][1]?.body))).toEqual({ positionId: null, category: 'household.groceries' })
  })

  test('a line parked on a position, switched to unplanned with a category, books without the position', async () => {
    entry = { ...baseEntry, positionId: 'p1', category: 'housing.rent', budget: 'needs' }
    const rent = { id: 'p1', label: 'Miete', amountPlanned: '500.00', category: 'housing.rent', budget: 'needs' }
    plan = { year: 2026, month: 9, positions: [rent] }
    const user = userEvent.setup()
    renderPage()
    await user.click(await screen.findByRole('button', { name: i18n.t('monthBook.book') }))
    const dialog = await screen.findByRole('dialog', { name: i18n.t('import.bookTitle') })
    await user.click(await within(dialog).findByRole('button', { name: 'Miete' }))
    await user.click(within(dialog).getByRole('combobox'))
    await user.click(await screen.findByRole('option', { name: i18n.t('monthBook.unplanned') }))
    await user.click(within(dialog).getByRole('button', { name: categoryLabel('housing.rent') }))
    const categoryPanel = within(dialog).getByRole('group', { name: i18n.t('common.category') })
    await user.click(within(categoryPanel).getByRole('button', { name: categoryLabel('housing.rent') }))
    await user.click(await screen.findByRole('button', { name: new RegExp(categoryGroupLabel('household')) }))
    await user.click(await screen.findByRole('button', { name: categoryLabel('household.groceries') }))
    await user.click(within(dialog).getByRole('button', { name: i18n.t('monthBook.book') }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    const patch = fetchMock.mock.calls.find(([, init]) => init?.method === 'PATCH')
    expect(JSON.parse(String(patch?.[1]?.body))).toEqual({ positionId: null, category: 'household.groceries' })
  })

  test('the table names the assignment as text and offers no pickers', async () => {
    entry = { ...baseEntry, category: 'household.groceries', budget: 'needs' }
    renderPage()
    const category = await screen.findByText(categoryLabel('household.groceries'))
    expect(category.closest('button')).toBeNull()
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument()
  })
})
