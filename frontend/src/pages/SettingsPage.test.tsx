import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, test, vi } from 'vitest'

import { i18n } from '@/lib/i18n'
import { SettingsPage } from '@/pages/SettingsPage'

afterEach(() => vi.unstubAllGlobals())

const ME = {
  id: 'u',
  email: 'a@example.org',
  firstName: 'A',
  lastName: 'B',
  targetNeeds: '50.00',
  targetWants: '30.00',
  targetSavings: '20.00',
  flowLimitsBy: 'plan',
  isSuperuser: false,
}

function open() {
  const fetchMock = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) => new Response(JSON.stringify(ME)))
  vi.stubGlobal('fetch', fetchMock)
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <MemoryRouter initialEntries={['/einstellungen']}>
        <SettingsPage />
      </MemoryRouter>
    </QueryClientProvider>
  )
  return fetchMock
}

describe('settings page', () => {
  test('shows the stored quotas as three sliders', async () => {
    open()
    expect(await screen.findByLabelText(i18n.t('quota.needs'))).toHaveValue('50')
    expect(screen.getByLabelText(i18n.t('quota.wants'))).toHaveValue('30')
    expect(screen.getByLabelText(i18n.t('quota.savings'))).toHaveValue('20')
  })

  test('saves the coupled values, which still add up to 100', async () => {
    const user = userEvent.setup()
    const fetchMock = open()
    const savings = await screen.findByLabelText(i18n.t('quota.savings'))
    const save = screen.getByRole('button', { name: 'Speichern' })
    expect(save).toBeDisabled()

    fireEvent.change(savings, { target: { value: '30' } })
    await user.click(save)

    const patch = fetchMock.mock.calls.find(([, init]) => init?.method === 'PATCH')
    expect(JSON.parse(String(patch?.[1]?.body))).toEqual({
      targetNeeds: '43.75',
      targetWants: '26.25',
      targetSavings: '30',
    })
  })

  test('offers the setting for limits in the flow', async () => {
    open()
    expect(await screen.findByLabelText(i18n.t('monthFlow.limitsBy'))).toBeInTheDocument()
  })
})
