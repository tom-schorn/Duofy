import type { QueryClient, QueryKey } from '@tanstack/react-query'
import { toast, type ExternalToast } from 'sonner'

import { errorText } from '@/lib/api'
import { i18n } from '@/lib/i18n'

/**
 * One message at a time, and delete-with-undo (UI guideline rules 7 and 15).
 *
 * A small thing (a position, a booking) leaves the list at once, but the request
 * only goes out when the undo window closes: after ten seconds, when a newer
 * message replaces this one, or when the page goes away. „Rückgängig“ therefore
 * needs no request at all — the backend stays as it is.
 */

/** How long a message with „Rückgängig“ stays. */
export const UNDO_MS = 10_000
/** How long a message without it stays; errors stay until dismissed. */
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

// A delete that has not gone out yet must not be lost with the page.
if (typeof window !== 'undefined') {
  window.addEventListener('pagehide', () => flushPendingDelete(true))
  window.addEventListener('beforeunload', () => flushPendingDelete(true))
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

  const hide = () => {
    for (const queryKey of hideIn) {
      for (const [key, data] of client.getQueriesData({ queryKey })) {
        const next = without(data, id)
        // Only when the row is there: writing the cache fires the subscription below.
        if (next !== data) client.setQueryData(key, next)
      }
    }
  }
  const reload = () => {
    for (const queryKey of invalidate) client.invalidateQueries({ queryKey })
  }

  hide()
  // A reload while the message is up (window focus, another save) would bring the
  // row back from the server; hide it again as long as the undo window is open.
  const unsubscribe = client.getQueryCache().subscribe((event) => {
    if (event.type === 'updated' && event.action.type === 'success') hide()
  })

  let settled = false
  let timer: ReturnType<typeof setTimeout>
  const me = { flush: (leaving: boolean) => commit(leaving) }

  const settle = () => {
    settled = true
    clearTimeout(timer)
    unsubscribe()
    if (waiting === me) waiting = null
  }

  const send = (keepalive: boolean) =>
    request(keepalive)
      .then(reload)
      .catch((error: unknown) => {
        reload()
        announce('error', errorText(error), {
          duration: Infinity,
          cancel: { label: i18n.t('ui.closeNotification'), onClick: () => {} },
          action: { label: i18n.t('errors.retry'), onClick: () => void send(false) },
        })
      })

  function commit(leaving = false) {
    if (settled) return
    settle()
    void send(leaving)
  }

  function undo() {
    if (settled) return
    settle()
    reload()
  }

  // Waving the message away accepts the delete. The message goes up first: showing
  // it flushes whatever was waiting before, and that must not be this delete.
  announce('success', i18n.t('toast.deleted', { name }), {
    duration: UNDO_MS,
    action: { label: i18n.t('ui.undo'), onClick: undo },
    onDismiss: () => commit(),
  })
  waiting = me
  timer = setTimeout(() => commit(), UNDO_MS)
}
