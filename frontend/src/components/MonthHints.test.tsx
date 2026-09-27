import { render, screen } from '@testing-library/react'
import { describe, expect, test } from 'vitest'

import { MonthHints } from '@/components/MonthHints'
import type { PlanHint } from '@/lib/domain'

const nothingFree = (free: string): PlanHint => ({
  code: 'plan_nothing_free',
  severity: 'info',
  positionId: null,
  params: { free },
})

describe('month hints', () => {
  test('says nothing is free when the income is exactly used up', () => {
    render(<MonthHints hints={[nothingFree('0.00')]} />)
    expect(screen.getByText(/nichts mehr frei/)).toBeInTheDocument()
  })

  test('names the overshoot when more is planned than earned', () => {
    render(<MonthHints hints={[nothingFree('-90.00')]} />)
    expect(screen.getByText(/90,00.*mehr verplant/)).toBeInTheDocument()
  })

  test('ignores position hints and unknown codes', () => {
    const { container } = render(
      <MonthHints
        hints={[
          { code: 'position_overdue', severity: 'warning', positionId: 'p', params: {} },
          { code: 'from_a_newer_version', severity: 'info', positionId: null, params: {} },
        ]}
      />
    )
    expect(container).toBeEmptyDOMElement()
  })
})
