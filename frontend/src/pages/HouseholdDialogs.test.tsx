import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, test, vi } from 'vitest'

import { CreatePlanDialog } from '@/components/CreatePlanDialog'
import { CreateHouseholdButton } from '@/pages/HouseholdPage'

afterEach(() => vi.unstubAllGlobals())

describe('household dialog', () => {
  test('Verwerfen really discards: reopening shows an empty field', async () => {
    const user = userEvent.setup()
    render(
      <QueryClientProvider client={new QueryClient()}>
        <CreateHouseholdButton />
      </QueryClientProvider>
    )
    await user.click(screen.getByRole('button', { name: /Haushalt anlegen/ }))
    await user.type(screen.getByRole('textbox'), 'WG')
    await user.keyboard('{Escape}')
    await user.click(screen.getByRole('button', { name: 'Verwerfen' }))
    await user.click(screen.getByRole('button', { name: /Haushalt anlegen/ }))
    expect(screen.getByRole('textbox')).toHaveValue('')
  })

  test('a failed attempt leaves no error behind after Abbrechen and reopen', async () => {
    const user = userEvent.setup()
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response(JSON.stringify({ detail: 'not_allowed' }), { status: 403 }))
    )
    render(
      <QueryClientProvider client={new QueryClient()}>
        <CreateHouseholdButton />
      </QueryClientProvider>
    )
    await user.click(screen.getByRole('button', { name: /Haushalt anlegen/ }))
    await user.type(screen.getByRole('textbox'), 'WG')
    await user.click(screen.getByRole('button', { name: 'Anlegen' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Dazu fehlt dir die Berechtigung.')
    await user.click(screen.getByRole('button', { name: 'Abbrechen' }))
    await user.click(screen.getByRole('button', { name: /Haushalt anlegen/ }))
    await waitFor(() => expect(screen.queryByRole('alert')).not.toBeInTheDocument())
    expect(screen.getByRole('textbox')).toHaveValue('')
  })
})

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
