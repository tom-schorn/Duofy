import { toast } from 'sonner'

import { errorText } from '@/lib/api'
import { i18n } from '@/lib/i18n'

/**
 * The shared net for a change that failed and has no error path of its own.
 *
 * The message stays until it is dismissed and offers to run the action again — a
 * toast that flies away would let the failure pass unnoticed.
 */
export function reportMutationError(error: unknown, retry: () => void) {
  toast.error(errorText(error), {
    duration: Infinity,
    action: { label: i18n.t('errors.retry'), onClick: retry },
  })
}
