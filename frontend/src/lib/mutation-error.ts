import { errorText } from '@/lib/api'
import { i18n } from '@/lib/i18n'
import { announce } from '@/lib/undo-delete'

/**
 * The shared net for a change that failed and has no error path of its own.
 *
 * The message stays until it is dismissed and offers to run the action again — a
 * toast that flies away would let the failure pass unnoticed.
 */
export function reportMutationError(error: unknown, retry: () => void) {
  announce('error', errorText(error), {
    duration: Infinity,
    // Sonner's own close button has a fixed English label the Toaster does not let
    // us translate, so the dismissal is a labelled button from the catalog —
    // reachable by keyboard, which the swipe-away toast is not.
    cancel: { label: i18n.t('ui.closeNotification'), onClick: () => {} },
    action: { label: i18n.t('errors.retry'), onClick: retry },
  })
}

/**
 * Whether the caller shows this failure itself, so the net stays quiet.
 *
 * Either the hook is marked (`meta.inlineError`) or a single call is — for an
 * action that has an inline error in one place (a dialog) and none in another
 * (a tick in the list).
 */
export function showsErrorInline(
  meta: Record<string, unknown> | undefined,
  input: unknown
): boolean {
  if (meta?.inlineError === true) return true
  return (
    typeof input === 'object' &&
    input !== null &&
    (input as { inlineError?: unknown }).inlineError === true
  )
}
