import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, test, vi } from 'vitest'

import { CreatePlanDialog } from '@/components/CreatePlanDialog'

afterEach(() => vi.unstubAllGlobals())

describe('create-month dialog', () => {
  test('cancelling a changed month returns to the preselection without a question', async () => {
    const user = userEvent.setup()
    const tree = (open: boolean) => (
      <QueryClientProvider client={new QueryClient()}>
        <CreatePlanDialog ownerId={null} ownerName={null} open={open} onOpenChange={() => {}} year={2026} month={10} />
      </QueryClientProvider>
    )
    const { rerender } = render(tree(true))
    await user.click(screen.getByRole('combobox', { name: 'Monat' }))
    await user.click(await screen.findByRole('option', { name: 'Dezember' }))
    expect(screen.getByRole('combobox', { name: 'Monat' })).toHaveTextContent('Dezember')
    await user.keyboard('{Escape}')
    expect(screen.queryByText(/verwerfen[?]/)).not.toBeInTheDocument()
    rerender(tree(false))
    rerender(tree(true))
    await waitFor(() =>
      expect(screen.getByRole('combobox', { name: 'Monat' })).toHaveTextContent('Oktober')
    )
  })
})
