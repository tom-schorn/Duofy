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
  className?: string
  children: React.ReactNode
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
  pendingLabel,
  onSubmit,
  submitDisabled = false,
  dirty = false,
  pending = false,
  error = null,
  start,
  returnFocus,
  className,
  children,
}: Props) {
  const { t } = useTranslation()
  const [asking, setAsking] = useState(false)
  // Who had the focus when the dialog opened; Radix only remembers a Trigger.
  const opener = useRef<HTMLElement | null>(null)

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
          'max-h-[90svh] overflow-y-auto',
          width === 'narrow' ? 'sm:max-w-sm' : 'sm:max-w-md',
          className
        )}
        onOpenAutoFocus={(event) => {
          opener.current = document.activeElement as HTMLElement | null
          // The first field, not the ✕ or whatever Radix finds first.
          const field = (event.currentTarget as HTMLElement).querySelector<HTMLElement>(
            'input:not([type=hidden]):not(:disabled), textarea:not(:disabled), select:not(:disabled), [role=combobox]:not(:disabled)'
          )
          if (field) {
            event.preventDefault()
            field.focus()
          }
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
        <form onSubmit={onSubmit} className="flex flex-col gap-5">
          <DialogHeader>
            <DialogTitle className="font-heading text-xl">{title}</DialogTitle>
            {description ? (
              <DialogDescription>{description}</DialogDescription>
            ) : (
              <DialogDescription className="sr-only">{title}</DialogDescription>
            )}
          </DialogHeader>

          {children}

          <FormError error={error} />

          {asking ? (
            <DialogFooter className="items-center sm:justify-between">
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
            <DialogFooter className={start ? 'sm:justify-between' : undefined}>
              {start}
              <div className="flex flex-col-reverse gap-2 sm:flex-row">
                <Button type="button" variant="outline" disabled={pending} onClick={close}>
                  {t('common.cancel')}
                </Button>
                <Button type="submit" disabled={pending || submitDisabled}>
                  {pending ? (pendingLabel ?? t('common.saving')) : submitLabel}
                </Button>
              </div>
            </DialogFooter>
          )}
        </form>
      </DialogContent>
    </Dialog>
  )
}
