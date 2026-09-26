import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, test } from 'vitest'

import { PositionDialog } from '@/components/PositionDialog'
import { ApiError } from '@/lib/api'

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
        onDelete={null}
        error={error}
      />
    </QueryClientProvider>
  )
}

describe('PositionDialog', () => {
  test('stays open, shows the error box and keeps the input when saving fails', async () => {
    const user = userEvent.setup()
    render(<Page />)
    await user.type(screen.getByLabelText('Bezeichnung'), 'Miete')
    await user.type(screen.getByLabelText('Betrag'), '500')
    await user.click(screen.getByRole('button', { name: 'Anlegen' }))
    expect(screen.getByRole('dialog')).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('Dazu fehlt dir die Berechtigung.')
    expect(screen.getByLabelText('Bezeichnung')).toHaveValue('Miete')
  })
})
