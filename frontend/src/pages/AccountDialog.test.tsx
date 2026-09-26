import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { afterEach, describe, expect, test, vi } from 'vitest'

import { AccountDialog } from '@/pages/AccountsPage'
import type { Account } from '@/lib/domain'
import { i18n } from '@/lib/i18n'

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
    expect(screen.getByRole('button', { name: /schen$/ })).toBeInTheDocument()
    unmount()
    render(ui({ account: { ...account, deletable: false }, open: true }))
    expect(screen.queryByRole('button', { name: /schen$/ })).not.toBeInTheDocument()
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

describe('AccountDialog delete', () => {
  test('asks once, deletes on confirm and puts the focus on the given target', async () => {
    const user = userEvent.setup()
    const fetchMock = vi.fn(async () => new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetchMock)
    const deleted = vi.fn()
    function Page() {
      const [open, setOpen] = useState(true)
      return (
        <QueryClientProvider client={new QueryClient()}>
          <h1 id="page-heading" tabIndex={-1}>
            Konten
          </h1>
          <AccountDialog
            account={account}
            mayDelete
            open={open}
            onOpenChange={setOpen}
            onDeleted={deleted}
            returnFocus={() => document.getElementById('page-heading')}
          />
        </QueryClientProvider>
      )
    }
    render(<Page />)
    await user.click(screen.getByRole('button', { name: i18n.t('common.delete') }))
    expect(fetchMock).not.toHaveBeenCalled()
    await user.click(
      within(screen.getByRole('alertdialog')).getByRole('button', { name: i18n.t('common.delete') })
    )
    await waitFor(() => expect(fetchMock).toHaveBeenCalled())
    await waitFor(() => expect(screen.getByRole('heading', { name: 'Konten' })).toHaveFocus())
    expect(deleted).toHaveBeenCalledTimes(1)
  })

  test('has no delete button once the account is in use; the Aktiv switch is what is left', () => {
    render(ui({ account: { ...account, deletable: false }, open: true }))
    expect(screen.queryByRole('button', { name: i18n.t('common.delete') })).not.toBeInTheDocument()
    expect(screen.getByRole('switch', { name: i18n.t('accounts.active') })).toBeInTheDocument()
  })
})
