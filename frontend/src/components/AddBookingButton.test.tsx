import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { AddBookingButton } from '@/components/AddBookingButton'
import { shiftMonth, today } from '@/lib/dates'
import { monthLabel } from '@/lib/domain'
import { i18n } from '@/lib/i18n'

const account = {
  id: 'a1',
  name: 'Giro',
  active: true,
  isDefault: true,
  type: 'checking',
  balance: '0.00',
}
const savings = { ...account, id: 'a2', name: 'Tagesgeld', isDefault: false }
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
      JSON.stringify(
        String(url).includes('/accounts') ? (props.accounts ?? [account, savings]) : []
      ),
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
  fetchMock.mock.calls
    .filter(([, init]) => init?.method === 'POST')
    .map(([, init]) => JSON.parse(String(init.body)))

async function open(user: ReturnType<typeof userEvent.setup>) {
  await user.click(await screen.findByRole('button', { name: i18n.t('monthBook.add') }))
  return screen.findByRole('dialog', { name: i18n.t('monthBook.add') })
}

const month = (offset: number) => shiftMonth(today(), offset)

describe('AddBookingButton (#241)', () => {
  beforeEach(() => vi.unstubAllGlobals())
  afterEach(() => vi.unstubAllGlobals())

  test('it is the page-level button and opens the dialog to add a booking', async () => {
    renderButton()
    expect(await open(userEvent.setup())).toBeInTheDocument()
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

  test('the dialog reads as a sentence: name and amount on top, then date, account, month and position as words', async () => {
    renderButton()
    const dialog = await open(userEvent.setup())
    expect(within(dialog).getByLabelText(i18n.t('monthBook.what'))).toBeInTheDocument()
    expect(within(dialog).getByLabelText(i18n.t('common.amount'))).toBeInTheDocument()
    // The four words, in the sentence.
    const sentence = within(dialog).getByText(/^Bezahlt am/)
    expect(sentence).toHaveTextContent(/vom\s*Giro/)
    expect(sentence).toHaveTextContent(/zählt im Plan/)
    expect(sentence).toHaveTextContent(/Ungeplant · Grundbedarf/)
  })

  test('booking counts an unplanned booking in the month of its date unless another is chosen', async () => {
    const user = userEvent.setup()
    renderButton()
    const dialog = await open(user)
    await user.type(within(dialog).getByLabelText(i18n.t('monthBook.what')), 'Friseur')
    await user.type(within(dialog).getByLabelText(i18n.t('common.amount')), '23,40')
    await user.click(within(dialog).getByRole('button', { name: i18n.t('monthBook.book') }))
    await waitFor(() => expect(posted()).toHaveLength(1))
    const [body] = posted()
    expect(body).toMatchObject({
      note: 'Friseur',
      positionId: null,
      accountId: 'a1',
      counterAccountId: null,
      budget: 'needs',
      planYear: month(0).year,
      planMonth: month(0).month,
      occurredOn: today(),
    })
  })

  test('the plan month is a word: choosing the next month sends it', async () => {
    const user = userEvent.setup()
    renderButton()
    const dialog = await open(user)
    await user.type(within(dialog).getByLabelText(i18n.t('common.amount')), '10')
    await user.click(within(dialog).getByRole('button', { name: monthLabel(month(0).month) }))
    await user.click(await screen.findByRole('button', { name: /Folgemonat/ }))
    await user.click(within(dialog).getByRole('button', { name: i18n.t('monthBook.book') }))
    await waitFor(() => expect(posted()).toHaveLength(1))
    expect(posted()[0]).toMatchObject({ planYear: month(1).year, planMonth: month(1).month })
  })

  test('the account is a word: choosing another account sends it', async () => {
    const user = userEvent.setup()
    renderButton()
    const dialog = await open(user)
    await user.type(within(dialog).getByLabelText(i18n.t('common.amount')), '10')
    await user.click(within(dialog).getByRole('button', { name: 'Giro' }))
    await user.click(await screen.findByRole('button', { name: 'Tagesgeld' }))
    await user.click(within(dialog).getByRole('button', { name: i18n.t('monthBook.book') }))
    await waitFor(() => expect(posted()).toHaveLength(1))
    expect(posted()[0].accountId).toBe('a2')
  })

  test('the position word opens a choice of positions and offers the category only while unplanned', async () => {
    const user = userEvent.setup()
    renderButton()
    const dialog = await open(user)
    // Unplanned: the category is asked for (it decides the budget).
    expect(within(dialog).getByRole('button', { name: /Lebensmittel/ })).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: /Ungeplant · Grundbedarf/ }))
    expect(
      await screen.findByRole('combobox', { name: i18n.t('monthBook.positionLabel') })
    ).toBeInTheDocument()
  })

  test('a transfer is a word too: it sends the other account and no purpose or plan month', async () => {
    const user = userEvent.setup()
    renderButton()
    const dialog = await open(user)
    await user.type(within(dialog).getByLabelText(i18n.t('common.amount')), '50')
    await user.click(within(dialog).getByRole('button', { name: i18n.t('monthBook.noTransfer') }))
    await user.click(await screen.findByRole('button', { name: 'Tagesgeld' }))
    await user.click(within(dialog).getByRole('button', { name: i18n.t('monthBook.transfer') }))
    await waitFor(() => expect(posted()).toHaveLength(1))
    const [body] = posted()
    expect(body).toMatchObject({ counterAccountId: 'a2', category: null, budget: null })
    expect(body).not.toHaveProperty('planYear')
  })

  test('the dialog closes once the booking is saved', async () => {
    const user = userEvent.setup()
    renderButton()
    const dialog = await open(user)
    await user.type(within(dialog).getByLabelText(i18n.t('common.amount')), '12,50')
    await user.click(within(dialog).getByRole('button', { name: i18n.t('monthBook.book') }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })
})
