import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { HelpButton, HelpColumn } from '@/components/HelpPanel'
import { useHelpPinned } from '@/lib/help-state'
import de from '@/locales/de.json'

const PINNED_KEY = 'duofy.help.pinned'

/** The two halves as the layout wires them: the button in the header, the column beside the page. */
function Wired({ wide = true }: { wide?: boolean }) {
  const help = useHelpPinned()

  return (
    <>
      <HelpButton pinned={help.pinned} onPin={help.setPinned} wide={wide} />
      <HelpColumn pinned={help.pinned} onPin={help.setPinned} />
    </>
  )
}

function renderAt(path = '/book', wide = true) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Wired wide={wide} />
    </MemoryRouter>
  )
}

describe('the help button', () => {
  beforeEach(() => localStorage.clear())
  afterEach(() => vi.unstubAllGlobals())

  test('opens the help as a sheet and Esc closes it, focus back on the button', async () => {
    renderAt()
    const button = screen.getByRole('button', { name: de.helpPanel.button })
    button.focus()
    fireEvent.click(button)

    const sheet = await screen.findByRole('dialog')
    expect(sheet).toBeInTheDocument()
    expect(sheet.contains(document.activeElement)).toBe(true)

    fireEvent.keyDown(sheet, { key: 'Escape' })
    await vi.waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    await vi.waitFor(() => expect(button).toHaveFocus())
  })

  test('keeping it open is remembered and shows the column instead of the sheet', async () => {
    renderAt()
    fireEvent.click(screen.getByRole('button', { name: de.helpPanel.button }))
    fireEvent.click(await screen.findByRole('button', { name: de.helpPanel.pin }))

    expect(localStorage.getItem(PINNED_KEY)).toBe('true')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getByRole('complementary')).toBeInTheDocument()
  })

  test('a remembered choice is there after a reload, and can be taken back', () => {
    localStorage.setItem(PINNED_KEY, 'true')
    renderAt()
    expect(screen.getByRole('complementary')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: de.helpPanel.hide }))
    expect(localStorage.getItem(PINNED_KEY)).toBe('false')
    expect(screen.queryByRole('complementary')).not.toBeInTheDocument()
  })

  test('has no button and no column on a page without help', () => {
    renderAt('/nowhere')
    expect(screen.queryByRole('button', { name: de.helpPanel.button })).not.toBeInTheDocument()
  })

  test('pinning moves the focus to the same header button, unpinning keeps it there', async () => {
    renderAt()
    const button = screen.getByRole('button', { name: de.helpPanel.button })
    fireEvent.click(button)
    fireEvent.click(await screen.findByRole('button', { name: de.helpPanel.pin }))
    await vi.waitFor(() => expect(screen.getByRole('button', { name: de.helpPanel.button })).toBe(button))
    await vi.waitFor(() => expect(button).toHaveFocus())

    // The header button takes the column away again, and stays the same element.
    fireEvent.click(button)
    expect(screen.queryByRole('complementary')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: de.helpPanel.button })).toBe(button)
    expect(button).toHaveFocus()
  })

  test('the column own button hands the focus to the header button', () => {
    localStorage.setItem(PINNED_KEY, 'true')
    renderAt()
    fireEvent.click(screen.getByRole('button', { name: de.helpPanel.hide }))
    expect(screen.getByRole('button', { name: de.helpPanel.button })).toHaveFocus()
  })

  test('narrow: a pinned choice still opens the sheet, and offers no pin', async () => {
    localStorage.setItem(PINNED_KEY, 'true')
    renderAt('/book', false)
    fireEvent.click(screen.getByRole('button', { name: de.helpPanel.button }))
    expect(await screen.findByRole('dialog')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: de.helpPanel.pin })).not.toBeInTheDocument()
  })

  test('works when the browser refuses storage: the pin holds for the session and is logged', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    vi.stubGlobal('localStorage', {
      getItem: () => null,
      setItem: () => {
        throw new Error('blocked')
      },
    })
    renderAt()
    fireEvent.click(screen.getByRole('button', { name: de.helpPanel.button }))
    fireEvent.click(await screen.findByRole('button', { name: de.helpPanel.pin }))

    expect(screen.getByRole('complementary')).toBeInTheDocument()
    expect(warn).toHaveBeenCalled()
    warn.mockRestore()
  })
})
