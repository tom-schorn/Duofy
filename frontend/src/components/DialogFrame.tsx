import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { FormError } from '@/components/FormError'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { cn } from '@/lib/utils'

type Props = {
  open: boolean
  onOpenChange: (open: boolean) => void
  title: string
  description?: string
  /** narrow for questions, normal for forms. */
  width?: 'narrow' | 'normal'
  /** Repeats the verb of the trigger: „Anlegen“ or „Speichern“. */
  submitLabel: string
  /** Step 1 of a two-step create dialog has nothing to send yet: only „Abbrechen“. */
  hideSubmit?: boolean
  /** Replaces „Speichert…“ while pending, when the verb is another one. */
  pendingLabel?: string
  onSubmit: (event: React.FormEvent<HTMLFormElement>) => void
  /** The form cannot be sent yet (a field is invalid). */
  submitDisabled?: boolean
  /** The user changed something: outside clicks stop closing, Esc asks first. */
  dirty?: boolean
  /** The server has not answered: nothing closes, both buttons are locked. */
  pending?: boolean
  /** The server said no; shown above the buttons, the input stays. */
  error?: unknown
  /** Delete (rule 6): red, left in the footer, only when the person may delete. */
  start?: React.ReactNode
  /**
   * Where the focus goes on closing instead of back to the opener — after a delete
   * the opener is gone. Returns null to fall back to the opener.
   */
  returnFocus?: () => HTMLElement | null
  /**
   * Changes when the dialog shows something else (the step): the focus then moves
   * to the first field, or to the first card when there is no field yet.
   */
  focusKey?: unknown
  className?: string
  children: React.ReactNode
}

const FIELD =
  'input:not([type=hidden]):not(:disabled), textarea:not(:disabled), select:not(:disabled), [role=combobox]:not(:disabled)'
/** Cards mark themselves with this attribute so the frame can find the first one. */
const CARD_ATTRIBUTE = 'data-dialog-card'

function focusFirst(root: HTMLElement): boolean {
  const target =
    root.querySelector<HTMLElement>(FIELD) ?? root.querySelector<HTMLElement>(`[${CARD_ATTRIBUTE}]`)
  target?.focus()
  return target !== null
}

/**
 * The one frame around every form dialog (UI guideline rules 12–14): title, focus
 * into the first field and back to where the user came from, a guard against
 * losing changes, a lock while saving, and the same footer everywhere.
 */
export function DialogFrame({
  open,
  onOpenChange,
  title,
  description,
  width = 'normal',
  submitLabel,
  hideSubmit = false,
  pendingLabel,
  onSubmit,
  submitDisabled = false,
  dirty = false,
  pending = false,
  error = null,
  start,
  returnFocus,
  focusKey,
  className,
  children,
}: Props) {
  const { t } = useTranslation()
  const [asking, setAsking] = useState(false)
  // Who had the focus when the dialog opened; Radix only remembers a Trigger.
  const opener = useRef<HTMLElement | null>(null)
  const body = useRef<HTMLDivElement>(null)
  const lastFocusKey = useRef(focusKey)

  // Going to another step: the focus would otherwise stay on the button that is gone.
  useEffect(() => {
    if (lastFocusKey.current === focusKey) return
    lastFocusKey.current = focusKey
    if (open && body.current) focusFirst(body.current)
  }, [focusKey, open])

  // A dialog that closes from outside (after a save) must not reopen with the
  // question still standing.
  useEffect(() => {
    if (!open) setAsking(false)
  }, [open])

  function close() {
    setAsking(false)
    onOpenChange(false)
  }

  function handleOpenChange(next: boolean) {
    if (pending) return
    if (!next) close()
    else onOpenChange(next)
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent
        className={cn(
          // Header and footer stay put, only the middle scrolls (UI guideline rule 21).
          'flex max-h-[90svh] flex-col gap-0 overflow-hidden',
          width === 'narrow' ? 'sm:max-w-sm' : 'sm:max-w-md',
          className
        )}
        onOpenAutoFocus={(event) => {
          opener.current = document.activeElement as HTMLElement | null
          // The first field (or card), not the ✕ or whatever Radix finds first.
          if (focusFirst(event.currentTarget as HTMLElement)) event.preventDefault()
        }}
        onCloseAutoFocus={(event) => {
          const target = returnFocus?.()
          if (target) {
            event.preventDefault()
            target.focus()
          } else if (opener.current?.isConnected) {
            event.preventDefault()
            opener.current.focus()
          }
        }}
        onEscapeKeyDown={(event) => {
          if (pending) {
            event.preventDefault()
            return
          }
          if (asking) {
            // Never discard by Esc alone: a second Esc means keep editing.
            event.preventDefault()
            setAsking(false)
          } else if (dirty) {
            event.preventDefault()
            setAsking(true)
          }
        }}
        onInteractOutside={(event) => {
          if (dirty || asking) event.preventDefault()
        }}
      >
        <form onSubmit={onSubmit} className="flex min-h-0 flex-1 flex-col gap-4">
          <DialogHeader className="shrink-0 pr-8">
            <DialogTitle className="font-heading text-xl">{title}</DialogTitle>
            {description ? (
              <DialogDescription>{description}</DialogDescription>
            ) : (
              <DialogDescription className="sr-only">{title}</DialogDescription>
            )}
          </DialogHeader>

          {/* The padding keeps focus rings from being cut off by the scroll area. */}
          <div
            ref={body}
            className="-mx-1 flex min-h-0 flex-1 flex-col gap-5 overflow-y-auto px-1 py-1"
          >
            {children}
          </div>

          <div className="shrink-0 empty:hidden">
            <FormError error={error} />
          </div>

          {asking ? (
            <DialogFooter className="shrink-0 items-center sm:justify-between">
              <p role="alert" className="text-sm font-medium">
                {t('ui.dialog.discardQuestion')}
              </p>
              <div className="flex flex-col-reverse gap-2 sm:flex-row">
                <Button type="button" variant="outline" onClick={() => setAsking(false)}>
                  {t('ui.dialog.keepEditing')}
                </Button>
                <Button type="button" variant="destructive" onClick={close}>
                  {t('ui.dialog.discard')}
                </Button>
              </div>
            </DialogFooter>
          ) : (
            <DialogFooter className={cn('shrink-0', start && 'sm:justify-between')}>
              {start}
              <div className="flex flex-col-reverse gap-2 sm:flex-row">
                <Button type="button" variant="outline" disabled={pending} onClick={close}>
                  {t('common.cancel')}
                </Button>
                {!hideSubmit && (
                  <Button type="submit" disabled={pending || submitDisabled}>
                    {pending ? (pendingLabel ?? t('common.saving')) : submitLabel}
                  </Button>
                )}
              </div>
            </DialogFooter>
          )}
        </form>
      </DialogContent>
    </Dialog>
  )
}
