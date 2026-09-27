import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, test } from 'vitest'

import { i18n } from '@/lib/i18n'
import { keys } from '@/lib/queries'
import { OWN_SCOPE, type Account } from '@/lib/domain'
import { CommitmentDialog } from '@/components/CommitmentDialog'
import type { Commitment } from '@/lib/domain'

const KIND_CARDS = [i18n.t('commitmentDialog.types.contract.label'), 'Limit', 'Kredit oder Rate', 'Sparziel', 'Einnahme']

async function chooseKind(user: ReturnType<typeof userEvent.setup>, name = i18n.t('commitmentDialog.types.contract.label')) {
  await user.click(screen.getByRole('button', { name: new RegExp(name) }))
}

const existing: Commitment = {
  id: 'c1',
  deletable: true,
  type: 'debt',
  name: 'Auto-Kredit',
  amount: '250.00',
  category: 'finance.debt',
  budget: 'savings',
  isLimit: false,
  householdId: null,
  intervalMonths: 1,
  firstDueDate: '2026-01-05',
  endsOn: null,
  passThrough: false,
  counterAccountId: null,
  targetAmount: null,
  targetDate: null,
  paymentMethod: null,
  accountId: null,
}

function renderEdit(commitment: Commitment) {
  render(
    <QueryClientProvider client={new QueryClient()}>
      <CommitmentDialog commitment={commitment} open onOpenChange={() => {}} onSave={() => {}} />
    </QueryClientProvider>
  )
}

function renderWithAccounts(count: number) {
  const client = new QueryClient()
  const accounts = Array.from({ length: count }, (_, index) => ({
    id: `a${index}`,
    deletable: true,
    name: `Konto ${index}`,
    type: 'checking',
    openingBalance: '0.00',
    openingDate: '2026-01-01',
    isDefault: index === 0,
    active: true,
    externalRef: null,
  })) as unknown as Account[]
  client.setQueryData(keys.accountsIn(OWN_SCOPE), accounts)
  render(
    <QueryClientProvider client={client}>
      <CommitmentDialog commitment={null} open onOpenChange={() => {}} onSave={() => {}} />
    </QueryClientProvider>
  )
}

function renderDialog(onOpenChange: (open: boolean) => void) {
  render(
    <QueryClientProvider client={new QueryClient()}>
      <CommitmentDialog commitment={null} open onOpenChange={onOpenChange} onSave={() => {}} />
    </QueryClientProvider>
  )
}

describe('CommitmentDialog', () => {
  test('opens with the question and five kind cards, and nothing to send yet', () => {
    renderDialog(() => {})
    expect(screen.getByRole('dialog', { name: 'Was ist das?' })).toBeInTheDocument()
    for (const card of KIND_CARDS) {
      expect(screen.getByRole('button', { name: new RegExp(card) })).toBeInTheDocument()
    }
    expect(screen.queryByRole('button', { name: 'Anlegen' })).not.toBeInTheDocument()
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument()
  })

  test('a card leads to the fields of that kind, named in the title', async () => {
    const user = userEvent.setup()
    renderDialog(() => {})
    await chooseKind(user, 'Kredit oder Rate')
    expect(screen.getByRole('dialog', { name: 'Kredit anlegen' })).toBeInTheDocument()
    expect(screen.getByLabelText('Rate')).toBeInTheDocument()
    expect(screen.queryByLabelText('Zielbetrag')).not.toBeInTheDocument()
  })

  test('a savings goal shows target amount, target date and the target account up front', async () => {
    const user = userEvent.setup()
    renderDialog(() => {})
    await chooseKind(user, 'Sparziel')
    expect(screen.getByLabelText(/Zielbetrag/)).toBeInTheDocument()
    expect(screen.getByLabelText(/Zieldatum/)).toBeInTheDocument()
    expect(screen.getByText('Zielkonto')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Weitere Angaben' })).toHaveAttribute(
      'aria-expanded',
      'false'
    )
  })

  test('a loan shows its end and the account up front', async () => {
    const user = userEvent.setup()
    renderDialog(() => {})
    await chooseKind(user, 'Kredit oder Rate')
    expect(screen.getByLabelText(/Läuft bis/)).toBeInTheDocument()
    expect(screen.getByText('Konto')).toBeInTheDocument()
  })

  test('income shows the account up front', async () => {
    const user = userEvent.setup()
    renderDialog(() => {})
    await chooseKind(user, 'Einnahme')
    expect(screen.getByText('Konto')).toBeInTheDocument()
  })

  test('a regular expense asks for the account only when there is more than one', async () => {
    const user = userEvent.setup()
    renderDialog(() => {})
    await chooseKind(user)
    expect(screen.queryByText('Konto')).not.toBeInTheDocument()
    cleanup()
    renderWithAccounts(2)
    await chooseKind(user)
    expect(screen.getByText('Konto')).toBeInTheDocument()
  })

  test('a limit is its own card, sets the flag and is named in the title', async () => {
    const user = userEvent.setup()
    const saved: Commitment[] = []
    render(
      <QueryClientProvider client={new QueryClient()}>
        <CommitmentDialog
          commitment={null}
          open
          onOpenChange={() => {}}
          onSave={(c) => saved.push(c)}
        />
      </QueryClientProvider>
    )
    await chooseKind(user, 'Limit')
    expect(screen.getByRole('dialog', { name: 'Limit anlegen' })).toBeInTheDocument()
    await user.type(screen.getByLabelText('Bezeichnung'), 'Lebensmittel')
    await user.type(document.getElementById('amount') as HTMLElement, '400')
    await user.click(screen.getByRole('button', { name: 'Weitere Angaben' }))
    expect(screen.queryByText(/kein fester Betrag zum Abhaken/)).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Anlegen' }))
    expect(saved[0]).toMatchObject({ type: 'contract', isLimit: true })
  })

  test('an existing limit is edited as „Limit bearbeiten“', () => {
    renderEdit({
      ...existing,
      type: 'contract',
      isLimit: true,
      category: 'housing.rent',
      budget: 'needs',
    })
    expect(screen.getByRole('dialog', { name: 'Limit bearbeiten' })).toBeInTheDocument()
  })

  test('the way back leads to the cards again, and the typed name stays', async () => {
    const user = userEvent.setup()
    renderDialog(() => {})
    await chooseKind(user)
    await user.type(screen.getByLabelText('Bezeichnung'), 'Miete')
    await user.click(screen.getByRole('button', { name: i18n.t('commitmentDialog.back') }))
    expect(screen.getByRole('dialog', { name: 'Was ist das?' })).toBeInTheDocument()
    await chooseKind(user, 'Einnahme')
    expect(screen.getByLabelText('Bezeichnung')).toHaveValue('Miete')
  })

  test('the way back puts the focus on the first card, a card on the first field', async () => {
    const user = userEvent.setup()
    renderDialog(() => {})
    expect(screen.getByRole('button', { name: new RegExp(i18n.t('commitmentDialog.types.contract.label')) })).toHaveFocus()
    await chooseKind(user)
    expect(screen.getByLabelText('Bezeichnung')).toHaveFocus()
    await user.click(screen.getByRole('button', { name: i18n.t('commitmentDialog.back') }))
    expect(screen.getByRole('button', { name: new RegExp(i18n.t('commitmentDialog.types.contract.label')) })).toHaveFocus()
  })

  test('Weitere Angaben stays open after going back and choosing again', async () => {
    const user = userEvent.setup()
    renderDialog(() => {})
    await chooseKind(user)
    await user.click(screen.getByRole('button', { name: 'Weitere Angaben' }))
    await user.click(screen.getByRole('button', { name: i18n.t('commitmentDialog.back') }))
    await chooseKind(user, 'Einnahme')
    expect(screen.getByRole('button', { name: 'Weitere Angaben' })).toHaveAttribute(
      'aria-expanded',
      'true'
    )
  })

  test('offers Anlegen and puts the focus into the first field', async () => {
    const user = userEvent.setup()
    renderDialog(() => {})
    await chooseKind(user)
    expect(screen.getByRole('button', { name: 'Anlegen' })).toBeInTheDocument()
    expect(screen.getAllByRole('textbox')[0]).toHaveFocus()
  })

  test('asks before discarding after a change', async () => {
    const user = userEvent.setup()
    let closed = false
    renderDialog(() => {
      closed = true
    })
    await chooseKind(user)
    await user.type(screen.getAllByRole('textbox')[0], 'Miete')
    await user.keyboard('{Escape}')
    expect(screen.getByText(/verwerfen[?]/)).toBeInTheDocument()
    expect(closed).toBe(false)
  })
})

describe('CommitmentDialog interval', () => {
  test('choosing a card alone does not make the dialog dirty', async () => {
    const user = userEvent.setup()
    let closed = false
    renderDialog(() => {
      closed = true
    })
    await chooseKind(user)
    await user.keyboard('{Escape}')
    expect(screen.queryByText(/verwerfen[?]/)).not.toBeInTheDocument()
    expect(closed).toBe(true)
  })
})

describe('CommitmentDialog invalid interval', () => {
  test('blocks saving with interval 0 and says why', async () => {
    const user = userEvent.setup()
    renderDialog(() => {})
    await chooseKind(user)
    await user.click(screen.getByRole('combobox', { name: /Abstand/ }))
    await user.click(await screen.findByRole('option', { name: 'Anderer Abstand' }))
    const field = await screen.findByLabelText('Abstand in Monaten')
    await user.clear(field)
    await user.type(field, '0')
    expect(screen.getByRole('button', { name: 'Anlegen' })).toBeDisabled()
    expect(screen.getByText(/Ganze Monate von 1 bis 120/)).toBeInTheDocument()
    expect(field).toHaveAttribute('aria-invalid', 'true')
  })
})

describe('CommitmentDialog amount', () => {
  function renderWithSave(onSave: (c: { amount: string }) => void) {
    render(
      <QueryClientProvider client={new QueryClient()}>
        <CommitmentDialog commitment={null} open onOpenChange={() => {}} onSave={onSave} />
      </QueryClientProvider>
    )
  }

  test('hands the typed amount to the draft as a decimal string', async () => {
    const user = userEvent.setup()
    const saved: { amount: string }[] = []
    renderWithSave((c) => saved.push(c))
    await chooseKind(user)
    await user.type(screen.getAllByRole('textbox')[0], 'Miete')
    await user.type(document.getElementById('amount') as HTMLElement, '1.234,5')
    await user.click(screen.getByRole('button', { name: 'Anlegen' }))
    expect(saved[0]?.amount).toBe('1234.50')
  })

  test('saves a planned amount of 0,00', async () => {
    const user = userEvent.setup()
    const saved: { amount: string }[] = []
    renderWithSave((c) => saved.push(c))
    await chooseKind(user)
    await user.type(screen.getAllByRole('textbox')[0], 'Miete')
    await user.type(document.getElementById('amount') as HTMLElement, '0,00')
    await user.click(screen.getByRole('button', { name: 'Anlegen' }))
    expect(saved[0]?.amount).toBe('0.00')
  })

  test('refuses a sign and says why', async () => {
    const user = userEvent.setup()
    const saved: unknown[] = []
    renderWithSave((c) => saved.push(c))
    await chooseKind(user)
    await user.type(screen.getAllByRole('textbox')[0], 'Miete')
    const field = document.getElementById('amount') as HTMLElement
    await user.type(field, '-50')
    await user.tab()
    expect(screen.getByRole('alert')).toHaveTextContent(/Betrag wie 1\.234,56/)
    await user.click(screen.getByRole('button', { name: 'Anlegen' }))
    expect(saved).toHaveLength(0)
  })
})

describe('CommitmentDialog edit', () => {
  test('has no kind cards and no way back, the kind is in the title and the fields follow', () => {
    renderEdit(existing)
    expect(screen.getByRole('dialog', { name: 'Kredit bearbeiten' })).toBeInTheDocument()
    expect(screen.getByLabelText('Bezeichnung')).toHaveValue('Auto-Kredit')
    for (const card of KIND_CARDS) {
      expect(screen.queryByRole('button', { name: new RegExp(card) })).not.toBeInTheDocument()
    }
    expect(screen.queryByRole('button', { name: i18n.t('commitmentDialog.back') })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Speichern' })).toBeInTheDocument()
  })
})

describe('CommitmentDialog Weitere Angaben', () => {
  test('starts closed on a new commitment and opens on a click', async () => {
    const user = userEvent.setup()
    renderDialog(() => {})
    await chooseKind(user)
    const toggle = screen.getByRole('button', { name: 'Weitere Angaben' })
    expect(toggle).toHaveAttribute('aria-expanded', 'false')
    expect(screen.queryByText('Zahlungsart')).not.toBeInTheDocument()
    await user.click(toggle)
    expect(toggle).toHaveAttribute('aria-expanded', 'true')
    expect(screen.getByText('Zahlungsart')).toBeInTheDocument()
  })

  test('starts closed on an edit whose rare fields are all empty', () => {
    renderEdit(existing)
    expect(screen.getByRole('button', { name: 'Weitere Angaben' })).toHaveAttribute(
      'aria-expanded',
      'false'
    )
  })

  test('opens by itself on an edit that already holds a value in it', () => {
    renderEdit({ ...existing, paymentMethod: 'transfer' })
    expect(screen.getByRole('button', { name: 'Weitere Angaben' })).toHaveAttribute(
      'aria-expanded',
      'true'
    )
    expect(screen.getByText('Zahlungsart')).toBeInTheDocument()
  })
})
