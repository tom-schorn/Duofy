import { useRef, useState } from 'react'
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
  /** The user changed something: outside clicks stop closing, Esc asks first. */
  dirty?: boolean
  /** The server has not answered: nothing closes, both buttons are locked. */
  pending?: boolean
  /** The server said no; shown above the buttons, the input stays. */
  error?: unknown
  /**
   * Temporary: a button that still sits left of the footer (delete). It moves into
   * the ⋯ menu with #141 and this slot goes with it.
   */
  start?: React.ReactNode
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
  dirty = false,
  pending = false,
  error = null,
  start,
  className,
  children,
}: Props) {
  const { t } = useTranslation()
  const [asking, setAsking] = useState(false)
  // Who had the focus when the dialog opened; Radix only remembers a Trigger.
  const opener = useRef<HTMLElement | null>(null)

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
            'input:not([type=hidden]), textarea, select, [role=combobox]'
          )
          if (field) {
            event.preventDefault()
            field.focus()
          }
        }}
        onCloseAutoFocus={(event) => {
          if (opener.current?.isConnected) {
            event.preventDefault()
            opener.current.focus()
          }
        }}
        onEscapeKeyDown={(event) => {
          if (dirty && !asking) {
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
                <Button type="submit" disabled={pending}>
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
