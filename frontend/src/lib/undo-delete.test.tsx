import '@/lib/i18n'
import { QueryClient } from '@tanstack/react-query'
import { act, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { Toaster } from '@/components/ui/sonner'
import { i18n } from '@/lib/i18n'
import { announce, deleteWithUndo, flushPendingDelete, UNDO_MS } from '@/lib/undo-delete'

const KEY = ['plans', 2026, 9] as const
const rows = () => [
  { id: 'a', label: 'Miete' },
  { id: 'b', label: 'Strom' },
]

let client: QueryClient
let request: ReturnType<typeof vi.fn>

function remove(id: string, name: string, send = request) {
  act(() => {
    deleteWithUndo({
      client,
      id,
      name,
      hideIn: [['plans']],
      invalidate: [['plans']],
      request: send as unknown as (keepalive: boolean) => Promise<unknown>,
    })
  })
}

const undoButton = () => screen.getByRole('button', { name: i18n.t('ui.undo') })

describe('deleteWithUndo', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    client = new QueryClient()
    client.setQueryData(KEY, rows())
    request = vi.fn(async () => undefined)
    render(<Toaster />)
  })
  afterEach(() => {
    act(() => flushPendingDelete())
    vi.useRealTimers()
  })

  test('the row leaves the list at once, before any request', () => {
    remove('a', 'Miete')
    expect(client.getQueryData(KEY)).toEqual([{ id: 'b', label: 'Strom' }])
    expect(request).not.toHaveBeenCalled()
  })

  test('the message names the thing and offers Undo', async () => {
    remove('a', 'Miete')
    await act(() => vi.advanceTimersByTimeAsync(50))
    expect(screen.getByText(i18n.t('toast.deleted', { name: 'Miete' }))).toBeInTheDocument()
    expect(undoButton()).toBeInTheDocument()
  })

  test('Undo within the window sends no request, not even later', async () => {
    remove('a', 'Miete')
    await act(() => vi.advanceTimersByTimeAsync(50))
    fireEvent.click(undoButton())
    await act(() => vi.advanceTimersByTimeAsync(UNDO_MS * 2))
    expect(request).not.toHaveBeenCalled()
  })

  test('after the window exactly one DELETE goes out', async () => {
    remove('a', 'Miete')
    await act(() => vi.advanceTimersByTimeAsync(UNDO_MS - 100))
    expect(request).not.toHaveBeenCalled()
    await act(() => vi.advanceTimersByTimeAsync(200))
    expect(request).toHaveBeenCalledTimes(1)
    await act(() => vi.advanceTimersByTimeAsync(UNDO_MS * 2))
    expect(request).toHaveBeenCalledTimes(1)
  })

  test('a second delete replaces the first message and sends the first delete at once', async () => {
    const second = vi.fn(async () => undefined)
    remove('a', 'Miete')
    remove('b', 'Strom', second)
    expect(request).toHaveBeenCalledTimes(1)
    expect(second).not.toHaveBeenCalled()
    await act(() => vi.advanceTimersByTimeAsync(UNDO_MS + 100))
    expect(second).toHaveBeenCalledTimes(1)
    expect(request).toHaveBeenCalledTimes(1)
  })

  test('any other message flushes the waiting delete', () => {
    remove('a', 'Miete')
    act(() => {
      announce('success', 'Gespeichert')
    })
    expect(request).toHaveBeenCalledTimes(1)
  })

  test('the page going away flushes the waiting delete', () => {
    remove('a', 'Miete')
    act(() => {
      window.dispatchEvent(new Event('pagehide'))
    })
    expect(request).toHaveBeenCalledTimes(1)
    expect(request).toHaveBeenCalledWith(true)
  })

  test('at most one message is visible', async () => {
    remove('a', 'Miete')
    remove('b', 'Strom')
    await act(() => vi.advanceTimersByTimeAsync(500))
    expect(document.querySelectorAll('[data-sonner-toast][data-visible="true"]')).toHaveLength(1)
  })

  test('a reload during the window does not bring the row back', () => {
    remove('a', 'Miete')
    act(() => {
      client.setQueryData(KEY, rows())
    })
    expect(client.getQueryData(KEY)).toEqual([{ id: 'b', label: 'Strom' }])
  })
})
