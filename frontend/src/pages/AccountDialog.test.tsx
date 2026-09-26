import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, test, vi } from 'vitest'

import { AccountDialog } from '@/pages/AccountsPage'
import type { Account } from '@/lib/domain'

const account = {
  id: 'a1',
  deletable: true,
  name: 'Giro',
  type: 'checking',
  openingBalance: '0',
  openingDate: '2026-01-01',
  isDefault: false,
  active: true,
  externalRef: null,
  countsAsAvailable: true,
} as Account

function ui(props: { account: Account; open: boolean }) {
  return (
    <QueryClientProvider client={new QueryClient()}>
      <AccountDialog account={props.account} mayDelete open={props.open} onOpenChange={() => {}} />
    </QueryClientProvider>
  )
}

afterEach(() => vi.unstubAllGlobals())

describe('AccountDialog', () => {
  test('offers deleting only for an account that may be deleted', () => {
    const { unmount } = render(ui({ account, open: true }))
    expect(screen.getByRole('button', { name: 'Löschen' })).toBeInTheDocument()
    unmount()
    render(ui({ account: { ...account, deletable: false }, open: true }))
    expect(screen.queryByRole('button', { name: 'Löschen' })).not.toBeInTheDocument()
  })

  test('shows the error of a failed save and forgets it when the dialog opens again', async () => {
    const user = userEvent.setup()
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response(JSON.stringify({ detail: 'not_allowed' }), { status: 403 }))
    )
    const client = new QueryClient()
    const tree = (open: boolean) => (
      <QueryClientProvider client={client}>
        <AccountDialog account={account} mayDelete open={open} onOpenChange={() => {}} />
      </QueryClientProvider>
    )
    const { rerender } = render(tree(true))
    await user.click(screen.getByRole('button', { name: 'Speichern' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Dazu fehlt dir die Berechtigung.')
    rerender(tree(false))
    rerender(tree(true))
    await waitFor(() => expect(screen.queryByRole('alert')).not.toBeInTheDocument())
  })
})
