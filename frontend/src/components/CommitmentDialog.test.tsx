import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, test, vi } from 'vitest'

import { ApiError } from '@/lib/api'
import { i18n } from '@/lib/i18n'
import { keys } from '@/lib/queries'
import { OWN_SCOPE, type Account } from '@/lib/domain'
import { CommitmentDialog } from '@/components/CommitmentDialog'
import type { Commitment } from '@/lib/domain'

const KIND_CARDS = [i18n.t('commitmentDialog.types.contract.label'), 'Limit', 'Kredit oder Rate', 'Sparziel', 'Einnahme']

async function chooseKind(user: ReturnType<typeof userEvent.setup>, name = i18n.t('commitmentDialog.types.contract.label')) {
  await user.click(screen.getByRole('button', { name: new RegExp(name) }))
}

// The main sentence's own <p> — not the quieter second one, which also
// happens to say "Budget" in its passthrough word (issue #215, review D-215-4).
function mainSentenceEl() {
  return screen.getByText(
    (_, element) =>
      element?.tagName === 'P' &&
      /Budget/.test(element.textContent ?? '') &&
      !element.className.includes('text-muted-foreground')
  )
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
  isPrivate: false,
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
  })

  test('a savings goal names the target account and target date in its sentence', async () => {
    const user = userEvent.setup()
    renderDialog(() => {})
    await chooseKind(user, 'Sparziel')
    expect(screen.getByRole('button', { name: 'Geht raus' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Kein Zieldatum' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Kein Zielbetrag' })).toBeInTheDocument()
  })

  test('a loan names its end and its account in the sentence', async () => {
    const user = userEvent.setup()
    renderDialog(() => {})
    await chooseKind(user, 'Kredit oder Rate')
    expect(screen.getByRole('button', { name: i18n.t('commitmentDialog.noEnd') })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Standardkonto' })).toBeInTheDocument()
  })

  test('income names the account in the sentence', async () => {
    const user = userEvent.setup()
    renderDialog(() => {})
    await chooseKind(user, 'Einnahme')
    expect(screen.getByText(/Kommt/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Standardkonto' })).toBeInTheDocument()
  })

  test('a regular expense names the account in its main sentence only when there is more than one', async () => {
    const user = userEvent.setup()
    renderDialog(() => {})
    await chooseKind(user)
    // With just the one account it still names it, but in the second, quieter
    // sentence (issue #215, review D-215-4) — not in the main one.
    const mainSentence = mainSentenceEl()
    expect(within(mainSentence).queryByRole('button', { name: 'Standardkonto' })).not.toBeInTheDocument()
    cleanup()
    renderWithAccounts(2)
    await chooseKind(user)
    const mainSentenceWithAccounts = mainSentenceEl()
    expect(
      within(mainSentenceWithAccounts).getByRole('button', { name: 'Standardkonto' })
    ).toBeInTheDocument()
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
    await user.click(screen.getByRole('button', { name: 'Anlegen' }))
    expect(saved[0]).toMatchObject({ type: 'contract', isLimit: true })
  })

  test('creating for a member puts the owner on the draft, and shows their accounts', async () => {
    const user = userEvent.setup()
    const client = new QueryClient()
    client.setQueryData(keys.accountsIn({ kind: 'member', ownerId: 'u2' }), [
      {
        id: 'a-member',
        deletable: true,
        name: 'Konto von Alex',
        type: 'checking',
        openingBalance: '0.00',
        openingDate: '2026-01-01',
        isDefault: true,
        active: true,
        externalRef: null,
      },
    ] as unknown as Account[])
    const saved: Commitment[] = []
    render(
      <QueryClientProvider client={client}>
        <CommitmentDialog
          commitment={null}
          ownerId="u2"
          open
          onOpenChange={() => {}}
          onSave={(c) => saved.push(c)}
        />
      </QueryClientProvider>
    )
    await chooseKind(user, 'Einnahme')
    // The account is the clickable word in the sentence "Kommt … auf {account}." —
    // untouched, it still reads the default account's name.
    await user.click(screen.getByRole('button', { name: i18n.t('common.defaultAccount') }))
    expect(screen.getByRole('button', { name: 'Konto von Alex' })).toBeInTheDocument()
    await user.keyboard('{Escape}')
    await user.type(screen.getByLabelText('Bezeichnung'), 'Gehalt von Alex')
    await user.type(document.getElementById('amount') as HTMLElement, '2000')
    await user.click(screen.getByRole('button', { name: 'Anlegen' }))
    expect(saved[0]).toMatchObject({ ownerId: 'u2' })
  })

  describe('the member has not shared their accounts (#217)', () => {
    afterEach(() => vi.unstubAllGlobals())

    test('the account panel says so instead of offering an empty list', async () => {
      vi.stubGlobal(
        'fetch',
        vi.fn(async () =>
          Response.json({ detail: { code: 'no_insight_granted' } }, { status: 403 })
        )
      )
      const user = userEvent.setup()
      render(
        <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
          <CommitmentDialog
            commitment={null}
            ownerId="u2"
            ownerName="Alex"
            open
            onOpenChange={() => {}}
            onSave={() => {}}
          />
        </QueryClientProvider>
      )
      await chooseKind(user, 'Einnahme')
      await user.click(screen.getByRole('button', { name: i18n.t('common.defaultAccount') }))

      expect(
        await screen.findByText(i18n.t('commitmentDialog.accountsNotShared', { name: 'Alex' }))
      ).toBeInTheDocument()
    })
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

describe('CommitmentDialog sentence words', () => {
  test('the rhythm word opens a panel of chips right below the sentence, one at a time', async () => {
    const user = userEvent.setup()
    renderDialog(() => {})
    await chooseKind(user)
    const rhythm = screen.getByRole('button', { name: 'monatlich' })
    await user.click(rhythm)
    expect(rhythm).toHaveAttribute('aria-expanded', 'true')
    const panel = screen.getByRole('group', { name: 'Wie oft?' })
    await user.click(within(panel).getByRole('button', { name: i18n.t('enums.interval.quarterly') }))
    expect(screen.getByRole('button', { name: i18n.t('enums.interval.quarterly') })).toBeInTheDocument()
    // Picking closes the panel and gives the focus back to the word.
    expect(screen.queryByRole('group', { name: 'Wie oft?' })).not.toBeInTheDocument()
    await waitFor(() =>
      expect(screen.getByRole('button', { name: i18n.t('enums.interval.quarterly') })).toHaveFocus()
    )
  })

  test('opening a second word closes the first', async () => {
    const user = userEvent.setup()
    renderWithAccounts(2)
    await chooseKind(user)
    await user.click(screen.getByRole('button', { name: 'monatlich' }))
    expect(screen.getByRole('group', { name: 'Wie oft?' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Standardkonto' }))
    expect(screen.queryByRole('group', { name: 'Wie oft?' })).not.toBeInTheDocument()
    expect(screen.getByRole('group', { name: 'Welches Konto?' })).toBeInTheDocument()
  })

  test('Esc closes the open word and gives the focus back to it', async () => {
    const user = userEvent.setup()
    renderDialog(() => {})
    await chooseKind(user)
    const rhythm = screen.getByRole('button', { name: 'monatlich' })
    await user.click(rhythm)
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('group', { name: 'Wie oft?' })).not.toBeInTheDocument()
    expect(rhythm).toHaveFocus()
    // Esc did not also close the dialog.
    expect(screen.getByRole('dialog')).toBeInTheDocument()
  })

  test('a second click on the same word closes it', async () => {
    const user = userEvent.setup()
    renderDialog(() => {})
    await chooseKind(user)
    const rhythm = screen.getByRole('button', { name: 'monatlich' })
    await user.click(rhythm)
    await user.click(rhythm)
    expect(screen.queryByRole('group', { name: 'Wie oft?' })).not.toBeInTheDocument()
  })

  test('an anderer Abstand keeps the panel open for the number field', async () => {
    const user = userEvent.setup()
    renderDialog(() => {})
    await chooseKind(user)
    await user.click(screen.getByRole('button', { name: 'monatlich' }))
    await user.click(screen.getByRole('button', { name: 'Anderer Abstand' }))
    const field = await screen.findByLabelText('Abstand in Monaten')
    expect(screen.getByRole('group', { name: 'Wie oft?' })).toBeInTheDocument()
    await user.clear(field)
    await user.type(field, '5')
    expect(screen.getByRole('button', { name: 'alle 5 Monate' })).toBeInTheDocument()
  })
})

describe('CommitmentDialog invalid interval', () => {
  test('blocks saving with interval 0 and says why', async () => {
    const user = userEvent.setup()
    renderDialog(() => {})
    await chooseKind(user)
    await user.click(screen.getByRole('button', { name: 'monatlich' }))
    await user.click(screen.getByRole('button', { name: 'Anderer Abstand' }))
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

describe('CommitmentDialog rare facts', () => {
  test('a new commitment names category, account, payment and passthrough in a second sentence, all unset', async () => {
    const user = userEvent.setup()
    renderDialog(() => {})
    await chooseKind(user)
    expect(screen.getByRole('button', { name: 'Miete' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Standardkonto' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'offen' })).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: i18n.t('common.passThroughOff') })
    ).toBeInTheDocument()
  })

  test('an existing payment method is named in the second sentence', () => {
    renderEdit({ ...existing, paymentMethod: 'transfer' })
    expect(screen.getByText(/Überweisung/)).toBeInTheDocument()
  })

  test('a category that no longer matches the default is named in the second sentence', () => {
    renderEdit({ ...existing, category: 'income.earned' })
    expect(screen.getByText(/Gehalt & Lohn/)).toBeInTheDocument()
  })

  test('the passthrough word toggles between its two states', async () => {
    const user = userEvent.setup()
    renderDialog(() => {})
    await chooseKind(user)
    await user.click(screen.getByRole('button', { name: i18n.t('common.passThroughOff') }))
    await user.click(screen.getByRole('button', { name: i18n.t('common.passThroughOn') }))
    expect(
      screen.getByRole('button', { name: i18n.t('common.passThroughOn') })
    ).toBeInTheDocument()
  })
})

describe('CommitmentDialog subtitle', () => {
  test('the create hint shows only when creating', async () => {
    const user = userEvent.setup()
    renderDialog(() => {})
    await chooseKind(user)
    expect(screen.getByText(/Posten für jeden Monat entstehen daraus von selbst/)).toBeInTheDocument()
  })

  test('editing drops the create hint', () => {
    renderEdit(existing)
    expect(
      screen.queryByText(/Posten für jeden Monat entstehen daraus von selbst/)
    ).not.toBeInTheDocument()
  })
})

describe('CommitmentDialog server field errors', () => {
  test('a rejected save opens the word it names, marks it invalid, shows the message in its panel and moves the focus there (#203, review D-215-5)', async () => {
    render(
      <QueryClientProvider client={new QueryClient()}>
        <CommitmentDialog
          commitment={existing}
          open
          onOpenChange={() => {}}
          onSave={() => {}}
          error={new ApiError('ends_on_before_start', 422)}
        />
      </QueryClientProvider>
    )
    const word = screen.getByRole('button', { name: i18n.t('commitmentDialog.noEnd') })
    expect(word).toHaveAccessibleDescription(
      /Das Ende darf nicht vor dem Monat der ersten Fälligkeit liegen/
    )
    expect(
      within(screen.getByRole('group', { name: i18n.t('commitmentDialog.endsOnLabel') })).getByRole(
        'alert'
      )
    ).toHaveTextContent(i18n.t('errors.ends_on_before_start'))
    await waitFor(() => expect(word).toHaveFocus())
  })
})

describe('CommitmentDialog date words on a running contract (#237)', () => {
  const running: Commitment = {
    ...existing,
    type: 'contract',
    name: 'Streaming',
    category: 'leisure.entertainment',
    budget: 'wants',
    firstDueDate: '2026-08-05',
    endsOn: null,
  }

  test('setting the last payment saves it as the end and leaves the first due date alone', async () => {
    const user = userEvent.setup()
    const onSave = vi.fn()
    render(
      <QueryClientProvider client={new QueryClient()}>
        <CommitmentDialog commitment={running} open onOpenChange={() => {}} onSave={onSave} />
      </QueryClientProvider>
    )
    await user.click(screen.getByRole('button', { name: i18n.t('commitmentDialog.noEnd') }))
    const panel = screen.getByRole('group', { name: i18n.t('commitmentDialog.endsOnLabel') })
    await user.click(within(panel).getByRole('button', { name: 'Dezember' }))
    await user.click(screen.getByRole('button', { name: 'Speichern' }))
    expect(onSave).toHaveBeenCalledTimes(1)
    // Month and year only: the end is stored as the first day of that month.
    expect(onSave.mock.calls[0][0]).toMatchObject({ endsOn: '2026-12-01', firstDueDate: '2026-08-05' })
  })

  test('the end panel pages through the years and refuses months before the start', async () => {
    const user = userEvent.setup()
    const onSave = vi.fn()
    render(
      <QueryClientProvider client={new QueryClient()}>
        <CommitmentDialog commitment={running} open onOpenChange={() => {}} onSave={onSave} />
      </QueryClientProvider>
    )
    await user.click(screen.getByRole('button', { name: i18n.t('commitmentDialog.noEnd') }))
    const panel = screen.getByRole('group', { name: i18n.t('commitmentDialog.endsOnLabel') })
    // Refused months stay focusable (aria-disabled) so keyboard and screen reader
    // users reach them and the hint says why; a click does nothing.
    const july = within(panel).getByRole('button', { name: 'Juli' })
    expect(july).toHaveAttribute('aria-disabled', 'true')
    expect(july).not.toBeDisabled()
    expect(within(panel).getByRole('button', { name: 'August' })).not.toHaveAttribute('aria-disabled', 'true')
    expect(panel).toHaveTextContent(/Monate vor dem Start sind nicht w/)
    await user.click(july)
    expect(screen.queryByRole('button', { name: 'im Juli 2026' })).not.toBeInTheDocument()
    expect(within(panel).getByRole('button', { name: i18n.t('commitmentDialog.previousYear') })).toBeDisabled()
    await user.click(within(panel).getByRole('button', { name: i18n.t('commitmentDialog.nextYear') }))
    expect(within(panel).getByRole('button', { name: 'Juli' })).not.toHaveAttribute('aria-disabled', 'true')
    await user.click(within(panel).getByRole('button', { name: 'Februar' }))
    expect(screen.getByRole('button', { name: 'im Februar 2027' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Speichern' }))
    expect(onSave.mock.calls[0][0]).toMatchObject({ endsOn: '2027-02-01' })
  })

  test('an end already set can be cleared again', async () => {
    const user = userEvent.setup()
    const onSave = vi.fn()
    render(
      <QueryClientProvider client={new QueryClient()}>
        <CommitmentDialog commitment={{ ...running, endsOn: '2026-11-01' }} open onOpenChange={() => {}} onSave={onSave} />
      </QueryClientProvider>
    )
    await user.click(screen.getByRole('button', { name: 'im November 2026' }))
    await user.click(screen.getByRole('button', { name: i18n.t('commitmentDialog.clearEnd') }))
    await user.click(screen.getByRole('button', { name: 'Speichern' }))
    expect(onSave.mock.calls[0][0]).toMatchObject({ endsOn: null, firstDueDate: '2026-08-05' })
  })

  async function saveAfterPicking(
    commitment: Commitment,
    word: RegExp | string,
    day: RegExp | string,
    nextMonth = false
  ) {
    const user = userEvent.setup()
    const onSave = vi.fn()
    render(
      <QueryClientProvider client={new QueryClient()}>
        <CommitmentDialog commitment={commitment} open onOpenChange={() => {}} onSave={onSave} />
      </QueryClientProvider>
    )
    await user.click(screen.getByRole('button', { name: word }))
    if (nextMonth) {
      await user.click(within(screen.getByRole('group')).getByRole('button', { name: /nächsten monat|next month/i }))
    }
    await user.click(within(screen.getByRole('group')).getByRole('button', { name: day }))
    await user.click(screen.getByRole('button', { name: 'Speichern' }))
    return onSave.mock.calls[0][0] as Commitment
  }

  test('the first due date word changes the start and leaves the end alone', async () => {
    const saved = await saveAfterPicking({ ...running, endsOn: '2026-12-01' }, /05\. August 2026/, /12\. August 2026/)
    expect(saved).toMatchObject({ firstDueDate: '2026-08-12', endsOn: '2026-12-01' })
  })

  test('moving the first due date past the end clears the end instead of leaving it for a 422', async () => {
    const saved = await saveAfterPicking({ ...running, endsOn: '2026-08-01' }, /05\. August 2026/, /12\. September 2026/, true)
    expect(saved).toMatchObject({ firstDueDate: '2026-09-12', endsOn: null })
  })

  test('the end year of a closed panel does not survive to the next opening', async () => {
    const user = userEvent.setup()
    render(
      <QueryClientProvider client={new QueryClient()}>
        <CommitmentDialog commitment={running} open onOpenChange={() => {}} onSave={() => {}} />
      </QueryClientProvider>
    )
    const word = () => screen.getByRole('button', { name: i18n.t('commitmentDialog.noEnd') })
    await user.click(word())
    await user.click(screen.getByRole('button', { name: i18n.t('commitmentDialog.nextYear') }))
    expect(screen.getByText('2027')).toBeInTheDocument()
    await user.click(word())
    await user.click(word())
    expect(screen.getByText('2026')).toBeInTheDocument()
  })

  test('the target date word of a savings goal changes only the target date', async () => {
    const goal: Commitment = { ...running, type: 'savings_goal', category: 'finance.savings', budget: 'savings', targetDate: '2026-12-10' }
    const saved = await saveAfterPicking(goal, /10\. Dezember 2026/, /20\. Dezember 2026/)
    expect(saved).toMatchObject({ targetDate: '2026-12-20', firstDueDate: '2026-08-05', endsOn: null })
  })

  test('the end word of a loan, which sits in the main sentence, changes only the end', async () => {
    const saved = await saveAfterPicking({ ...existing, firstDueDate: '2026-08-05' }, i18n.t('commitmentDialog.noEnd'), 'Oktober')
    expect(saved).toMatchObject({ endsOn: '2026-10-01', firstDueDate: '2026-08-05' })
  })
})
