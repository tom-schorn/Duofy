import '@/lib/i18n'
import { QueryClient, QueryObserver } from '@tanstack/react-query'
import { act, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'

import { Toaster } from '@/components/ui/sonner'
import { ApiError } from '@/lib/api'
import { i18n } from '@/lib/i18n'
import { announce, deleteWithUndo, flushPendingDelete } from '@/lib/undo-delete'

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

const settle = (ms = 50) => act(() => vi.advanceTimersByTimeAsync(ms))
const undoButton = () => screen.getByRole('button', { name: i18n.t('ui.undo') })
const closeButton = () => screen.getByRole('button', { name: i18n.t('ui.closeNotification') })
const visibleToasts = () =>
  document.querySelectorAll('[data-sonner-toast][data-visible="true"]').length

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

  test('the message names the thing and offers Undo and closing', async () => {
    remove('a', 'Miete')
    await settle()
    expect(screen.getByText(i18n.t('toast.deleted', { name: 'Miete' }))).toBeInTheDocument()
    expect(undoButton()).toBeInTheDocument()
    expect(closeButton()).toBeInTheDocument()
  })

  test('while the message is still there nothing is sent, however long it takes', async () => {
    remove('a', 'Miete')
    await settle(60_000)
    expect(screen.getByText(i18n.t('toast.deleted', { name: 'Miete' }))).toBeInTheDocument()
    expect(request).not.toHaveBeenCalled()
  })

  test('closing the message sends exactly one DELETE', async () => {
    remove('a', 'Miete')
    await settle()
    fireEvent.click(closeButton())
    await settle(1000)
    expect(request).toHaveBeenCalledTimes(1)
    expect(screen.queryByText(i18n.t('toast.deleted', { name: 'Miete' }))).not.toBeInTheDocument()
  })

  test('Undo sends no request, not even later', async () => {
    remove('a', 'Miete')
    await settle()
    fireEvent.click(undoButton())
    await settle(60_000)
    expect(request).not.toHaveBeenCalled()
    expect(client.getQueryData(KEY)).toEqual(rows())
  })

  test('a second delete replaces the first message and sends the first delete at once', async () => {
    const second = vi.fn(async () => undefined)
    remove('a', 'Miete')
    remove('b', 'Strom', second)
    expect(request).toHaveBeenCalledTimes(1)
    expect(second).not.toHaveBeenCalled()
    await settle(60_000)
    expect(second).not.toHaveBeenCalled()
  })

  test('any other message flushes the waiting delete', () => {
    remove('a', 'Miete')
    act(() => {
      announce('success', 'Saved')
    })
    expect(request).toHaveBeenCalledTimes(1)
  })

  test('the page going away flushes the waiting delete, once, and the message goes with it', async () => {
    remove('a', 'Miete')
    await settle()
    act(() => {
      window.dispatchEvent(new Event('pagehide'))
    })
    await settle(1000)
    expect(request).toHaveBeenCalledTimes(1)
    expect(request).toHaveBeenCalledWith(true)
    // Dismissed: on its way out, no longer a choice the user can still make.
    expect(document.querySelector('[data-sonner-toast]')?.getAttribute('data-removed')).toBe('true')
  })

  test('the tab going to the background flushes the waiting delete', () => {
    remove('a', 'Miete')
    vi.spyOn(document, 'visibilityState', 'get').mockReturnValue('hidden')
    act(() => {
      document.dispatchEvent(new Event('visibilitychange'))
    })
    expect(request).toHaveBeenCalledTimes(1)
    vi.restoreAllMocks()
  })

  test('beforeunload does not flush: the page stays for a mailto link or a download', () => {
    remove('a', 'Miete')
    act(() => {
      window.dispatchEvent(new Event('beforeunload'))
    })
    expect(request).not.toHaveBeenCalled()
  })

  test('at most one message is visible', async () => {
    remove('a', 'Miete')
    remove('b', 'Strom')
    await settle(500)
    expect(visibleToasts()).toBe(1)
  })

  test('a reload while the message is up does not bring the row back', () => {
    remove('a', 'Miete')
    act(() => {
      client.setQueryData(KEY, rows())
    })
    expect(client.getQueryData(KEY)).toEqual([{ id: 'b', label: 'Strom' }])
  })
})

describe('deleteWithUndo without a network', () => {
  let unsubscribe: () => void

  beforeEach(() => {
    vi.useFakeTimers()
    client = new QueryClient()
    client.setQueryData(KEY, rows())
    // A list on screen whose reload fails, as it does offline.
    const observer = new QueryObserver(client, {
      queryKey: KEY,
      queryFn: () => Promise.reject(new ApiError('unreachable', 0)),
      staleTime: Infinity,
      retry: false,
    })
    unsubscribe = observer.subscribe(() => {})
    render(<Toaster />)
  })
  afterEach(() => {
    unsubscribe()
    act(() => flushPendingDelete())
    vi.useRealTimers()
  })

  test('Undo puts the row back even if the reload fails', async () => {
    request = vi.fn(async () => undefined)
    remove('a', 'Miete')
    await settle()
    fireEvent.click(undoButton())
    await settle(1000)
    expect(client.getQueryData(KEY)).toEqual(rows())
    expect(request).not.toHaveBeenCalled()
  })

  test('a failed DELETE puts the row back and leaves an error that stays and can retry', async () => {
    request = vi.fn(async () => {
      throw new ApiError('unreachable', 0)
    })
    remove('a', 'Miete')
    await settle()
    fireEvent.click(closeButton())
    await settle(1000)
    expect(request).toHaveBeenCalledTimes(1)
    expect(client.getQueryData(KEY)).toEqual(rows())
    await settle(60_000)
    const retry = screen.getByRole('button', { name: i18n.t('errors.retry') })
    expect(retry).toBeInTheDocument()
    fireEvent.click(retry)
    await settle(1000)
    expect(request).toHaveBeenCalledTimes(2)
  })
})
