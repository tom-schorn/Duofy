import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { describe, expect, test } from 'vitest'

import { CreatePlanDialog } from '@/components/CreatePlanDialog'

describe('CreatePlanDialog', () => {
  test('names its month and year selects and offers Anlegen', () => {
    render(
      <QueryClientProvider client={new QueryClient()}>
        <CreatePlanDialog ownerId={null} ownerName={null} open onOpenChange={() => {}} year={2026} month={10} />
      </QueryClientProvider>
    )
    expect(screen.getByRole('combobox', { name: 'Monat' })).toHaveTextContent('Oktober')
    expect(screen.getByRole('combobox', { name: 'Jahr' })).toHaveTextContent('2026')
    expect(screen.getByRole('button', { name: 'Anlegen' })).toBeInTheDocument()
  })
})
