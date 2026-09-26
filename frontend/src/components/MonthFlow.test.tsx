import '@/lib/i18n'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { describe, expect, test } from 'vitest'

import { FlowView } from '@/components/MonthFlow'
import type { PlanFlow } from '@/lib/domain'

const flow: PlanFlow = {
  year: 2026,
  month: 9,
  flowLimitsBy: 'plan',
  start: '0.00',
  entries: [
    {
      date: '2026-09-01',
      day: 1,
      amount: '-800.00',
      kind: 'plan',
      label: 'Miete',
      positionId: 'p1',
      balance: '-800.00',
    },
    {
      date: '2026-08-30',
      day: 1,
      amount: '-20.00',
      kind: 'booking',
      label: '',
      positionId: null,
      balance: '-820.00',
    },
    {
      date: '2026-09-20',
      day: 20,
      amount: '2000.00',
      kind: 'plan',
      label: 'Gehalt',
      positionId: 'p2',
      balance: '1180.00',
    },
  ],
  days: [{ day: 1, balance: '-820.00' }],
  hints: [],
}

const shortfall = {
  code: 'flow_shortfall',
  severity: 'warning' as const,
  positionId: null,
  params: { day: 1, amount: '820.00', account_id: null, account_name: null },
}

function renderView(props: Partial<React.ComponentProps<typeof FlowView>> = {}) {
  const client = new QueryClient()
  render(
    <QueryClientProvider client={client}>
      <FlowView flow={flow} {...props} />
    </QueryClientProvider>
  )
}

describe('FlowView', () => {
  test('the shortfall is the amount of the backend hint, not one worked out here', () => {
    renderView({ flow: { ...flow, hints: [shortfall] } })
    expect(screen.getByText(/Du brauchst am 1\. mindestens/)).toHaveTextContent('820,00')
  })

  test('without a hint the month is called self-carrying', () => {
    renderView()
    expect(screen.getByText(/^Der Monat tr.gt sich durchgehend selbst/)).toBeInTheDocument()
  })

  test('the household heading speaks for the household, not for a person', () => {
    renderView({ shared: true, flow: { ...flow, hints: [shortfall] } })
    expect(screen.getByText(/^Zusammen fehlen bis zu/)).toBeInTheDocument()
    expect(screen.queryByText(/Du brauchst/)).not.toBeInTheDocument()
  })

  test('a curve from zero is a change, never called a balance', () => {
    renderView()
    expect(screen.getAllByText(/^Ver.nderung seit Monatsanfang/).length).toBeGreaterThan(0)
    expect(screen.queryByText('Kontostand')).not.toBeInTheDocument()
  })

  test('a curve that starts at a carry-over may be called a balance', () => {
    renderView({ flow: { ...flow, start: '500.00' } })
    expect(screen.getAllByText('Kontostand').length).toBeGreaterThan(0)
  })

  test('the table tells plan entries from bookings and names a booking without a position', () => {
    renderView()
    expect(screen.getAllByText('Geplant')).toHaveLength(2)
    expect(screen.getByText('Gebucht')).toBeInTheDocument()
    expect(screen.getByText('Buchung ohne Posten')).toBeInTheDocument()
  })

  test('the switch for limits shows the saved setting and can be left out for print', () => {
    renderView({ flow: { ...flow, flowLimitsBy: 'bookings' } })
    expect(screen.getByRole('combobox', { name: /^Limits z.hlen nach/ })).toHaveTextContent(
      'Buchungen'
    )
  })

  test('no switch in the print copy', () => {
    renderView({ showSwitch: false })
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument()
  })
})
