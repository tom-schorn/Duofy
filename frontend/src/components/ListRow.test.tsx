import '@/lib/i18n'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, test, vi } from 'vitest'

import { ListRow } from '@/components/ListRow'

describe('ListRow', () => {
  test('opens from the keyboard: Tab reaches the row, Enter opens it', async () => {
    const user = userEvent.setup()
    const onOpen = vi.fn()
    render(
      <ul>
        <ListRow onOpen={onOpen} trailing={<span>500,00 €</span>}>
          <span>Miete</span>
        </ListRow>
      </ul>
    )
    await user.tab()
    expect(screen.getByRole('button', { name: /Miete/ })).toHaveFocus()
    await user.keyboard('{Enter}')
    expect(onOpen).toHaveBeenCalledTimes(1)
  })

  test('a control in the leading slot does not open the row', async () => {
    const user = userEvent.setup()
    const onOpen = vi.fn()
    const onTick = vi.fn()
    render(
      <ul>
        <ListRow onOpen={onOpen} leading={<input type="checkbox" aria-label="abhaken" onChange={onTick} />}>
          <span>Miete</span>
        </ListRow>
      </ul>
    )
    await user.click(screen.getByRole('checkbox'))
    expect(onTick).toHaveBeenCalled()
    expect(onOpen).not.toHaveBeenCalled()
  })

  test('a read-only row has no button at all', () => {
    render(
      <ul>
        <ListRow>
          <span>Miete</span>
        </ListRow>
      </ul>
    )
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
    expect(screen.getByText('Miete')).toBeInTheDocument()
  })

  test('the row button is described by the amount, so a screen reader hears it', () => {
    render(
      <ul>
        <ListRow onOpen={() => {}} trailing={<span>500,00 €</span>}>
          <span>Miete</span>
        </ListRow>
      </ul>
    )
    expect(screen.getByRole('button', { name: 'Miete' })).toHaveAccessibleDescription('500,00 €')
  })
})
