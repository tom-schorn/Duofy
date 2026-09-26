import '@/lib/i18n'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, test, vi } from 'vitest'

import { AccountRow } from '@/pages/AccountsPage'
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

function renderRow(overrides: { account?: Partial<Account>; mayEdit?: boolean } = {}) {
  const onOpen = vi.fn()
  render(
    <ul>
      <AccountRow
        account={{ ...account, ...overrides.account }}
        mayEdit={overrides.mayEdit ?? true}
        onOpen={onOpen}
      />
    </ul>
  )
  return onOpen
}

describe('AccountRow', () => {
  test('a click on the row opens it', async () => {
    const user = userEvent.setup()
    const onOpen = renderRow()
    await user.click(screen.getByRole('button', { name: /^Giro/ }))
    expect(onOpen).toHaveBeenCalledTimes(1)
  })

  test('the row has no menu, however the account is', () => {
    renderRow({ account: { deletable: false, active: false } })
    expect(screen.queryByRole('button', { name: /weitere Aktionen/ })).not.toBeInTheDocument()
  })

  test('without the right to edit there is no row button at all', () => {
    renderRow({ mayEdit: false })
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })
})
