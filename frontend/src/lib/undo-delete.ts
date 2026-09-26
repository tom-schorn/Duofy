import type { QueryClient, QueryKey } from '@tanstack/react-query'
import { toast, type ExternalToast } from 'sonner'

import { errorText } from '@/lib/api'
import { i18n } from '@/lib/i18n'

/**
 * One message at a time, and delete-with-undo (UI guideline rules 7 and 15).
 *
 * A small thing (a position, a booking) leaves the list at once, but the request
 * only goes out when its message goes away: the next message or action replaces it,
 * the user closes it, or the page is left. There is no timer — keyboard and screen
 * reader users must be able to reach „Rückgängig“ without time pressure. Undo
 * therefore needs no request at all; the backend stays as it is.
 */

/** How long a message without a choice stays; errors and undo stay until closed. */
export const NOTICE_MS = 5_000

/** The delete that waits behind the message on screen, if any. */
let waiting: { flush: (leaving: boolean) => void } | null = null

/** Sends the waiting delete now — a newer message has replaced its message. */
export function flushPendingDelete(leaving = false) {
  waiting?.flush(leaving)
}

/**
 * The one place a message is shown. A new message replaces the old one, and if the
 * old one held a delete back, that delete runs first.
 */
export function announce(kind: 'success' | 'error', text: string, options: ExternalToast = {}) {
  flushPendingDelete()
  toast.dismiss()
  return toast[kind](text, {
    duration: kind === 'success' ? NOTICE_MS : Infinity,
    ...options,
  })
}

// A delete that has not gone out yet must not be lost with the page. `pagehide`
// covers closing and navigating; `visibilitychange` covers the tab going to the
// background on mobile, where `pagehide` may never come. Not `beforeunload`: that
// also fires for a mailto link or a download, and the page stays.
//
// Accepted for V1: if the access token has expired by then, this last request is
// refused and nobody is left to refresh it — the DELETE is lost silently and the
// row comes back on the next load.
if (typeof window !== 'undefined') {
  window.addEventListener('pagehide', () => flushPendingDelete(true))
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'hidden') flushPendingDelete(true)
  })
}

/** The row without `id` — in a list, or in a plan with its positions. Same object if it is not there. */
function without(data: unknown, id: string): unknown {
  const isRow = (row: unknown) =>
    Boolean(row) && typeof row === 'object' && (row as { id?: unknown }).id === id
  if (Array.isArray(data)) return data.some(isRow) ? data.filter((row) => !isRow(row)) : data
  if (data && typeof data === 'object' && Array.isArray((data as { positions?: unknown }).positions)) {
    const plan = data as { positions: unknown[] }
    const positions = without(plan.positions, id)
    return positions === plan.positions ? data : { ...plan, positions }
  }
  return data
}

export function deleteWithUndo({
  client,
  id,
  name,
  hideIn,
  invalidate,
  request,
}: {
  client: QueryClient
  id: string
  /** What the message calls the thing: a label or an amount. */
  name: string
  /** Cached lists the row disappears from at once. */
  hideIn: readonly QueryKey[]
  /** Reloaded when the delete is done or was taken back. */
  invalidate: readonly QueryKey[]
  /** The DELETE. `keepalive` when the page is going away. */
  request: (keepalive: boolean) => Promise<unknown>
}) {
  flushPendingDelete()

  // What each list looked like before the row went, so undo and a failed DELETE
  // can put it back without a network round trip.
  const snapshots = new Map<string, { key: QueryKey; data: unknown }>()
  const hide = () => {
    for (const queryKey of hideIn) {
      for (const [key, data] of client.getQueriesData({ queryKey })) {
        const next = without(data, id)
        // Only when the row is there: writing the cache fires the subscription below.
        if (next === data) continue
        const hash = JSON.stringify(key)
        if (!snapshots.has(hash)) snapshots.set(hash, { key, data })
        client.setQueryData(key, next)
      }
    }
  }
  const reload = () => {
    for (const queryKey of invalidate) client.invalidateQueries({ queryKey })
  }
  const restore = () => {
    for (const { key, data } of snapshots.values()) client.setQueryData(key, data)
    // The refetch only makes sure; the row is already back if it fails.
    reload()
  }

  hide()
  // A reload while the message is up (window focus, another save) would bring the
  // row back from the server; hide it again as long as the message is on screen.
  const unsubscribe = client.getQueryCache().subscribe((event) => {
    if (event.type === 'updated' && event.action.type === 'success') hide()
  })

  let settled = false
  const me = { flush: (leaving: boolean) => commit(leaving) }

  const settle = () => {
    settled = true
    unsubscribe()
    if (waiting === me) waiting = null
  }

  const send = (keepalive: boolean): Promise<void> =>
    request(keepalive)
      .then(reload)
      .catch((error: unknown) => {
        restore()
        announce('error', errorText(error), {
          duration: Infinity,
          cancel: { label: i18n.t('ui.closeNotification'), onClick: () => {} },
          action: { label: i18n.t('errors.retry'), onClick: () => void send(false) },
        })
      })

  // The one way a delete goes out. The message is the only trigger; closing it here
  // (if the page or a newer message got there first) keeps message and request from
  // ever disagreeing.
  function commit(leaving = false) {
    if (settled) return
    settle()
    // Only one message lives at a time, and it is this one.
    toast.dismiss()
    void send(leaving)
  }

  function undo() {
    if (settled) return
    settle()
    restore()
  }

  // The message goes up first: showing it flushes whatever was waiting before, and
  // that must not be this delete.
  announce('success', i18n.t('toast.deleted', { name }), {
    // Stays until the next message, the user closing it, or the page being left.
    duration: Infinity,
    action: { label: i18n.t('ui.undo'), onClick: undo },
    // Sonner calls `onDismiss` for a swipe or Esc but not for its cancel button.
    cancel: { label: i18n.t('ui.closeNotification'), onClick: () => commit() },
    onDismiss: () => commit(),
  })
  waiting = me
}
