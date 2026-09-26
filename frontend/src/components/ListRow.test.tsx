import '@/lib/i18n'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Trash2 } from 'lucide-react'
import { describe, expect, test, vi } from 'vitest'

import { ListRow } from '@/components/ListRow'
import { RowMenu } from '@/components/RowMenu'

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
})

describe('RowMenu', () => {
  test('renders nothing without actions', () => {
    const { container } = render(<RowMenu name="Miete" items={[]} />)
    expect(container).toBeEmptyDOMElement()
  })

  test('names the object and lists the destructive action last after a separator', async () => {
    const user = userEvent.setup()
    const onDelete = vi.fn()
    render(
      <RowMenu
        name="Miete"
        items={[
          { key: 'delete', label: 'Delete', icon: Trash2, destructive: true, onSelect: onDelete },
          { key: 'end', label: 'Beenden', onSelect: () => {} },
        ]}
      />
    )
    await user.click(screen.getByRole('button', { name: 'Miete: weitere Aktionen' }))
    const items = screen.getAllByRole('menuitem')
    expect(items.map((item) => item.textContent)).toEqual(['Beenden', 'Delete'])
    expect(screen.getByRole('separator')).toBeInTheDocument()
    await user.click(items[1])
    expect(onDelete).toHaveBeenCalled()
  })

  test('is locked while the actions must not run', () => {
    render(<RowMenu name="Miete" disabled items={[{ key: 'end', label: 'Beenden', onSelect: () => {} }]} />)
    expect(screen.getByRole('button', { name: 'Miete: weitere Aktionen' })).toBeDisabled()
  })
})
