import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { HelpButton, HelpColumn } from '@/components/HelpPanel'
import { useHelpPinned } from '@/lib/help-state'
import de from '@/locales/de.json'

const CLOSED_KEY = 'duofy.help.closed'

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

/** A person who closed the column before: the help is a button and a sheet only. */
function renderClosed(path = '/book', wide = true) {
  localStorage.setItem(CLOSED_KEY, 'true')
  return renderAt(path, wide)
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
    renderClosed()
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

  test('the first visit on a wide screen shows the column', () => {
    renderAt()
    expect(screen.getByRole('complementary')).toBeInTheDocument()
  })

  test('an old stored "not pinned" does not hide the column', () => {
    localStorage.setItem('duofy.help.pinned', 'false')
    renderAt()
    expect(screen.getByRole('complementary')).toBeInTheDocument()
  })

  test('keeping it open again is remembered and shows the column instead of the sheet', async () => {
    renderClosed()
    fireEvent.click(screen.getByRole('button', { name: de.helpPanel.button }))
    fireEvent.click(await screen.findByRole('button', { name: de.helpPanel.pin }))

    expect(localStorage.getItem(CLOSED_KEY)).toBe('false')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getByRole('complementary')).toBeInTheDocument()
  })

  test('closing the column is remembered after a reload', () => {
    const first = renderAt()
    fireEvent.click(screen.getByRole('button', { name: de.helpPanel.hide }))
    expect(localStorage.getItem(CLOSED_KEY)).toBe('true')
    expect(screen.queryByRole('complementary')).not.toBeInTheDocument()

    first.unmount()
    renderAt()
    expect(screen.queryByRole('complementary')).not.toBeInTheDocument()
  })

  test('has no button and no column on a page without help', () => {
    renderAt('/nowhere')
    expect(screen.queryByRole('button', { name: de.helpPanel.button })).not.toBeInTheDocument()
  })

  test('pinning moves the focus to the same header button, unpinning keeps it there', async () => {
    renderClosed()
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
    renderAt()
    fireEvent.click(screen.getByRole('button', { name: de.helpPanel.hide }))
    expect(screen.getByRole('button', { name: de.helpPanel.button })).toHaveFocus()
  })

  test('narrow: the open default still opens the sheet, and offers no pin', async () => {
    renderAt('/book', false)
    fireEvent.click(screen.getByRole('button', { name: de.helpPanel.button }))
    expect(await screen.findByRole('dialog')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: de.helpPanel.pin })).not.toBeInTheDocument()
  })

  test('works when the browser refuses storage: the close holds for the session and is logged', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    vi.stubGlobal('localStorage', {
      getItem: () => null,
      setItem: () => {
        throw new Error('blocked')
      },
    })
    renderAt()
    fireEvent.click(screen.getByRole('button', { name: de.helpPanel.hide }))

    expect(screen.queryByRole('complementary')).not.toBeInTheDocument()
    expect(warn).toHaveBeenCalled()
    warn.mockRestore()
  })
})
