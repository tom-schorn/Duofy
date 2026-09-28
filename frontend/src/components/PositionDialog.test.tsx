import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, test, vi } from 'vitest'

import { PositionDialog } from '@/components/PositionDialog'
import { budgetHeadingId, budgetLabel, type PlanPosition } from '@/lib/domain'
import { ApiError } from '@/lib/api'
import { i18n } from '@/lib/i18n'

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

// A parent the way the pages are: it closes on success only, and hands the error
// back to the dialog when the server says no.
function Page() {
  const [open, setOpen] = useState(true)
  const [error, setError] = useState<unknown>(null)
  return (
    <QueryClientProvider client={new QueryClient()}>
      <PositionDialog
        position={null}
        budget="needs"
        planId="p1"
        open={open}
        onOpenChange={setOpen}
        onSave={() => setError(new ApiError('not_allowed', 403))}
        error={error}
      />
    </QueryClientProvider>
  )
}

describe('PositionDialog', () => {
  test('stays open, shows the error box and keeps the input when saving fails', async () => {
    const user = userEvent.setup()
    render(<Page />)
    await user.click(screen.getByRole('button', { name: /Verpflichtung/ }))
    await user.type(screen.getByLabelText('Bezeichnung'), 'Miete')
    await user.type(screen.getByLabelText('Betrag'), '500')
    await user.click(screen.getByRole('button', { name: 'Anlegen' }))
    expect(screen.getByRole('dialog')).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('Dazu fehlt dir die Berechtigung.')
    expect(screen.getByLabelText('Bezeichnung')).toHaveValue('Miete')
  })
})

const existing = {
  id: 'p1',
  label: 'Miete',
  amountPlanned: '500.00',
  amountActual: null,
  category: 'housing.rent',
  budget: 'needs',
  dueDay: 1,
  accountId: null,
  isLimit: false,
  counterAccountId: null,
  passThrough: false,
  paymentMethod: null,
  householdId: null,
  commitmentId: null,
  paidAt: null,
} as PlanPosition

function EditPage({
  onDelete,
  readOnly,
  ownerName,
}: {
  onDelete?: (position: PlanPosition) => void
  readOnly?: boolean
  ownerName?: string | null
}) {
  const [open, setOpen] = useState(true)
  return (
    <QueryClientProvider client={new QueryClient()}>
      <h2 id={budgetHeadingId('needs')} tabIndex={-1}>
        {budgetLabel('needs')}
      </h2>
      <h2 id={budgetHeadingId('wants')} tabIndex={-1}>
        {budgetLabel('wants')}
      </h2>
      <PositionDialog
        position={existing}
        budget="needs"
        planId="p1"
        open={open}
        onOpenChange={setOpen}
        onSave={() => {}}
        onDelete={onDelete}
        readOnly={readOnly}
        ownerName={ownerName}
      />
    </QueryClientProvider>
  )
}

describe('PositionDialog delete', () => {
  test('offers Delete in the footer when allowed, and calls it with the position', async () => {
    const user = userEvent.setup()
    const onDelete = vi.fn()
    render(<EditPage onDelete={onDelete} />)
    await user.click(screen.getByRole('button', { name: i18n.t('common.delete') }))
    expect(onDelete).toHaveBeenCalledWith(expect.objectContaining({ id: 'p1' }))
  })

  test('has no Delete without the right to delete', () => {
    render(<EditPage />)
    expect(screen.queryByRole('button', { name: i18n.t('common.delete') })).not.toBeInTheDocument()
  })

  test('has no Delete when creating', () => {
    render(
      <QueryClientProvider client={new QueryClient()}>
        <PositionDialog
          position={null}
          budget="needs"
          planId="p1"
          open
          onOpenChange={() => {}}
          onSave={() => {}}
          onDelete={() => {}}
        />
      </QueryClientProvider>
    )
    expect(screen.queryByRole('button', { name: i18n.t('common.delete') })).not.toBeInTheDocument()
  })

  test('after a delete the focus lands on the section heading', async () => {
    const user = userEvent.setup()
    render(<EditPage onDelete={() => {}} />)
    await user.click(screen.getByRole('button', { name: i18n.t('common.delete') }))
    await waitFor(() =>
      expect(screen.getByRole('heading', { name: budgetLabel('needs') })).toHaveFocus()
    )
  })

  test('after a delete the focus lands on the section the row was in, even if its budget was changed', async () => {
    const user = userEvent.setup()
    render(<EditPage onDelete={() => {}} />)
    await user.click(screen.getByRole('button', { name: budgetLabel('needs') }))
    await user.click(screen.getByRole('button', { name: budgetLabel('wants') }))
    await user.click(screen.getByRole('button', { name: i18n.t('common.delete') }))
    await waitFor(() =>
      expect(screen.getByRole('heading', { name: budgetLabel('needs') })).toHaveFocus()
    )
  })
})

function renderCreate() {
  render(
    <QueryClientProvider client={new QueryClient()}>
      <PositionDialog
        position={null}
        budget="needs"
        planId="p1"
        open
        onOpenChange={() => {}}
        onSave={() => {}}
      />
    </QueryClientProvider>
  )
}

describe('PositionDialog kind', () => {
  test('creating asks what it is first: Verpflichtung or Limit, nothing to send yet', () => {
    renderCreate()
    expect(screen.getByRole('dialog', { name: 'Was ist das?' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Verpflichtung/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Limit/ })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Anlegen' })).not.toBeInTheDocument()
  })

  test('a Verpflichtung names the due day in its main sentence, a Limit names it in the second sentence', async () => {
    const user = userEvent.setup()
    renderCreate()
    await user.click(screen.getByRole('button', { name: /Verpflichtung/ }))
    expect(screen.getByRole('dialog', { name: i18n.t('positionDialog.kinds.obligation.addTitle') })).toBeInTheDocument()
    const obligationMain = mainSentenceEl()
    expect(within(obligationMain).getByRole('button', { name: '1.' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: i18n.t('positionDialog.back') }))
    await user.click(screen.getByRole('button', { name: /Limit/ }))
    expect(screen.getByRole('dialog', { name: i18n.t('positionDialog.kinds.limit.addTitle') })).toBeInTheDocument()
    // Not in the main sentence anymore — the second, quieter one names it
    // instead (issue #215, review D-215-4).
    const limitMain = mainSentenceEl()
    expect(within(limitMain).queryByRole('button', { name: '1.' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: '1.' })).toBeInTheDocument()
  })

  test('the due day word and its opened field are described by the whole sentence for screen readers', async () => {
    const user = userEvent.setup()
    renderCreate()
    await user.click(screen.getByRole('button', { name: /Verpflichtung/ }))
    // A regex, not the exact string: the accessible-description algorithm adds a
    // space of its own at an element boundary (before the sentence's closing
    // full stop), which is not part of what this test cares about — hence the
    // trailing full stop is stripped before building the pattern.
    const filled = i18n
      .t('positionDialog.sentence.obligation')
      .replace('{dueDay}', '1.')
      .replace('{budget}', budgetLabel('needs'))
      .replace(/\.$/, '')
    const sentence = new RegExp(filled.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'))
    const dueDayWord = screen.getByRole('button', { name: '1.' })
    expect(dueDayWord).toHaveAccessibleDescription(sentence)
    await user.click(dueDayWord)
    // Not getByLabelText: the panel's own group and the field share one label
    // text, so a label lookup is ambiguous between them.
    expect(
      screen.getByRole('spinbutton', { name: i18n.t('positionDialog.dueDayLabel') })
    ).toHaveAccessibleDescription(sentence)
  })

  test('choosing a card alone does not make the dialog dirty', async () => {
    const user = userEvent.setup()
    const onOpenChange = vi.fn()
    render(
      <QueryClientProvider client={new QueryClient()}>
        <PositionDialog
          position={null}
          budget="needs"
          planId="p1"
          open
          onOpenChange={onOpenChange}
          onSave={() => {}}
        />
      </QueryClientProvider>
    )
    await user.click(screen.getByRole('button', { name: /Limit/ }))
    await user.keyboard('{Escape}')
    expect(screen.queryByText(/verwerfen[?]/)).not.toBeInTheDocument()
    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  test('editing has no cards; the kind is in the title and the fields follow', () => {
    render(<EditPage />)
    expect(screen.getByRole('dialog', { name: 'Verpflichtung bearbeiten' })).toBeInTheDocument()
    expect(screen.getByLabelText('Bezeichnung')).toHaveValue('Miete')
    expect(screen.queryByRole('button', { name: i18n.t('positionDialog.back') })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Verpflichtung$/ })).not.toBeInTheDocument()
  })

})

describe('PositionDialog rare facts', () => {
  test('an edit names category, account, counterAccount, payment, assignment, passthrough and the actual amount in a second sentence, all unset', () => {
    render(<EditPage />)
    expect(screen.getByRole('button', { name: 'Miete' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Standardkonto' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Geht raus' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'offen' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Nur mein Plan' })).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: i18n.t('common.passThroughOff') })
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Noch nichts verbucht.' })).toBeInTheDocument()
  })

  test('an existing payment method is named in the second sentence', () => {
    render(
      <QueryClientProvider client={new QueryClient()}>
        <PositionDialog
          position={{ ...existing, paymentMethod: 'transfer' } as PlanPosition}
          budget="needs"
          planId="p1"
          open
          onOpenChange={() => {}}
          onSave={() => {}}
        />
      </QueryClientProvider>
    )
    expect(screen.getByText(/Überweisung/)).toBeInTheDocument()
  })

  test('creating has no actual-amount word — nothing has been booked yet', async () => {
    const user = userEvent.setup()
    renderCreate()
    await user.click(screen.getByRole('button', { name: /Verpflichtung/ }))
    expect(screen.queryByText(/verbucht/)).not.toBeInTheDocument()
  })
})

describe('PositionDialog rights (#218)', () => {
  test('own position: no owner line, fields and words stay editable', () => {
    render(<EditPage />)
    expect(screen.queryByText(/^Posten von /)).not.toBeInTheDocument()
    expect(screen.getByLabelText('Bezeichnung')).toHaveValue('Miete')
    expect(screen.getByRole('button', { name: 'Standardkonto' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: i18n.t('common.save') })).toBeInTheDocument()
  })

  test('foreign position with edit right: names the owner, fields stay editable', () => {
    render(<EditPage ownerName="Alex" />)
    expect(screen.getByText('Posten von Alex')).toBeInTheDocument()
    expect(screen.getByLabelText('Bezeichnung')).toHaveValue('Miete')
    expect(screen.getByRole('button', { name: 'Standardkonto' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: i18n.t('common.save') })).toBeInTheDocument()
  })

  test('foreign position without edit right: view-only — plain words, no inputs, no delete, footer only "Schließen"', () => {
    render(<EditPage ownerName="Alex" readOnly onDelete={vi.fn()} />)
    expect(screen.getByText('Posten von Alex')).toBeInTheDocument()
    // Label and amount as plain text, no form controls left to edit them.
    expect(screen.queryByLabelText('Bezeichnung')).not.toBeInTheDocument()
    expect(screen.getByText('Miete', { selector: '.font-heading' })).toBeInTheDocument()
    // The sentence words are plain text, not clickable buttons.
    expect(screen.queryByRole('button', { name: 'Standardkonto' })).not.toBeInTheDocument()
    expect(screen.getByText('Standardkonto')).toBeInTheDocument()
    // No delete, even though a handler was passed — the caller may not know
    // the right is missing until here.
    expect(screen.queryByRole('button', { name: i18n.t('common.delete') })).not.toBeInTheDocument()
    // No save either — the footer only offers to close (next to the dialog's
    // own ✕, which is also named "Schließen" — hence two, not one).
    expect(screen.queryByRole('button', { name: i18n.t('common.save') })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: i18n.t('common.cancel') })).not.toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: i18n.t('ui.close') })).toHaveLength(2)
  })
})

describe('PositionDialog server field errors', () => {
  test('a rejected save opens the word it names, marks it invalid, shows the message in its panel and moves the focus there (#203, review D-215-5)', async () => {
    render(
      <QueryClientProvider client={new QueryClient()}>
        <PositionDialog
          position={existing}
          budget="needs"
          planId="p1"
          open
          onOpenChange={() => {}}
          onSave={() => {}}
          error={new ApiError('not_household_member', 403)}
        />
      </QueryClientProvider>
    )
    // Its panel is open (the chip inside repeats the same name), so the word
    // itself is told apart by its expanded state.
    const word = screen.getByRole('button', { name: 'Nur mein Plan', expanded: true })
    expect(word).toHaveAccessibleDescription(/Du bist kein Mitglied dieses Haushalts/)
    expect(
      within(screen.getByRole('group', { name: i18n.t('common.assignment') })).getByRole('alert')
    ).toHaveTextContent('Du bist kein Mitglied dieses Haushalts.')
    await waitFor(() => expect(word).toHaveFocus())
  })
})
