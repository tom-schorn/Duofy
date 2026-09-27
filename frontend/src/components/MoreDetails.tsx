import { useEffect, useState } from 'react'
import { ChevronRightIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'

import { cn } from '@/lib/utils'

type Props = {
  /** Flips on every (re)open of the dialog, so the section starts from the loaded values. */
  resetKey: unknown
  /** Something in here is set: it must not hide behind a closed section. */
  hasValues: boolean
  /** Something in here is invalid: the section opens so the message can be seen. */
  invalid?: boolean
  /** The person had it open before going back a step: it starts open again. */
  startOpen?: boolean
  /** Told on every click, so the caller can remember the choice across a step. */
  onToggle?: (open: boolean) => void
  children: React.ReactNode
}

/**
 * „Weitere Angaben“ (UI guideline rule 21): everything optional or rare, folded
 * away. Opens by itself when it already holds a value or an error.
 */
export function MoreDetails({
  resetKey,
  hasValues,
  invalid = false,
  startOpen = false,
  onToggle,
  children,
}: Props) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(hasValues || startOpen)

  // Only on (re)open: while the user types, a value they just entered must not
  // decide about the fold; they opened it themselves.
  useEffect(() => {
    setOpen(hasValues || startOpen)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [resetKey])

  useEffect(() => {
    if (invalid) setOpen(true)
  }, [invalid])

  return (
    <div className="flex flex-col gap-4">
      <button
        type="button"
        aria-expanded={open}
        aria-controls={open ? 'more-details' : undefined}
        onClick={() => {
          setOpen(!open)
          onToggle?.(!open)
        }}
        className="text-muted-foreground hover:text-foreground flex w-fit items-center gap-1 text-sm font-medium"
      >
        <ChevronRightIcon className={cn('size-4 transition-transform', open && 'rotate-90')} />
        {t('common.moreDetails')}
      </button>
      {open && (
        <div id="more-details" className="flex flex-col gap-4">
          {children}
        </div>
      )}
    </div>
  )
}
