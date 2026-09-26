import { render, screen } from '@testing-library/react'
import { describe, expect, test } from 'vitest'

import { QueryState } from '@/components/QueryState'
import { ApiError } from '@/lib/api'

describe('QueryState', () => {
  test('shows the children once the data is there', () => {
    render(
      <QueryState isPending={false} error={null}>
        <p>Plan</p>
      </QueryState>
    )
    expect(screen.getByText('Plan')).toBeInTheDocument()
  })

  test('shows the translated sentence for a backend error code, not the children', () => {
    render(
      <QueryState isPending={false} error={new ApiError('not_allowed', 403)}>
        <p>Plan</p>
      </QueryState>
    )
    expect(screen.getByText('Dazu fehlt dir die Berechtigung.')).toBeInTheDocument()
    expect(screen.queryByText('Plan')).not.toBeInTheDocument()
  })

  test('shows neither children nor error while loading', () => {
    render(
      <QueryState isPending error={new ApiError('not_allowed', 403)}>
        <p>Plan</p>
      </QueryState>
    )
    expect(screen.queryByText('Plan')).not.toBeInTheDocument()
    expect(screen.queryByText('Dazu fehlt dir die Berechtigung.')).not.toBeInTheDocument()
  })
})
