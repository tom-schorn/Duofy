import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { AddBookingButton } from '@/components/AddBookingButton'
import { i18n } from '@/lib/i18n'

const account = {
  id: 'a1',
  name: 'Giro',
  active: true,
  isDefault: true,
  type: 'checking',
  balance: '0.00',
}
const rent = { id: 'p1', label: 'Miete', category: 'housing.rent', budget: 'needs' }

let fetchMock: ReturnType<typeof vi.fn>

function renderButton(props: { readOnly?: boolean; accounts?: unknown[] } = {}) {
  fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
    if (init?.method === 'POST') {
      return new Response(JSON.stringify({ id: 't1', amount: '12.50', note: null }), {
        status: 201,
      })
    }
    return new Response(
      JSON.stringify(String(url).includes('/accounts') ? (props.accounts ?? [account]) : []),
      { status: 200 }
    )
  })
  vi.stubGlobal('fetch', fetchMock)
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <AddBookingButton
          year={2026}
          month={9}
          positions={[rent] as never}
          readOnly={props.readOnly ?? false}
        />
      </MemoryRouter>
    </QueryClientProvider>
  )
}

const posted = () =>
  fetchMock.mock.calls.filter(([, init]) => init?.method === 'POST').map(([, init]) =>
    JSON.parse(String(init.body))
  )

describe('AddBookingButton (#241)', () => {
  beforeEach(() => vi.unstubAllGlobals())
  afterEach(() => vi.unstubAllGlobals())

  test('it is the page-level button and opens the dialog to add a booking', async () => {
    const user = userEvent.setup()
    renderButton()
    await user.click(await screen.findByRole('button', { name: i18n.t('monthBook.add') }))
    expect(
      await screen.findByRole('dialog', { name: i18n.t('monthBook.add') })
    ).toBeInTheDocument()
  })

  test('without the right to book there is no button', async () => {
    renderButton({ readOnly: true })
    await waitFor(() => expect(fetchMock).toHaveBeenCalled())
    expect(screen.queryByRole('button', { name: i18n.t('monthBook.add') })).not.toBeInTheDocument()
  })

  test('without an account there is nothing to book on, so no button', async () => {
    renderButton({ accounts: [] })
    await waitFor(() => expect(fetchMock).toHaveBeenCalled())
    expect(screen.queryByRole('button', { name: i18n.t('monthBook.add') })).not.toBeInTheDocument()
  })

  test('booking counts an unplanned booking in the month of today unless another is chosen', async () => {
    const user = userEvent.setup()
    renderButton()
    await user.click(await screen.findByRole('button', { name: i18n.t('monthBook.add') }))
    await user.type(await screen.findByLabelText(i18n.t('common.amount')), '12,50')
    await user.click(screen.getByRole('button', { name: i18n.t('monthBook.book') }))
    await waitFor(() => expect(posted()).toHaveLength(1))
    const now = new Date()
    expect(posted()[0].planYear).toBe(now.getFullYear())
    expect(posted()[0].planMonth).toBe(now.getMonth() + 1)
    expect(posted()[0].positionId).toBeNull()
  })

  test('the dialog closes once the booking is saved', async () => {
    const user = userEvent.setup()
    renderButton()
    await user.click(await screen.findByRole('button', { name: i18n.t('monthBook.add') }))
    await user.type(await screen.findByLabelText(i18n.t('common.amount')), '12,50')
    await user.click(screen.getByRole('button', { name: i18n.t('monthBook.book') }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })
})
