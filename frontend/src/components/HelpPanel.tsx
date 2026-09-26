import { useState } from 'react'
import { ChevronRight, CircleHelp, PanelRightClose, PanelRightOpen } from 'lucide-react'
import { useTranslation } from 'react-i18next'

import { Button } from '@/components/ui/button'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/sheet'
import { helpFor, type HelpKey } from '@/lib/help'
import { useHelpKey } from '@/lib/help-state'
import { cn } from '@/lib/utils'

/**
 * The explanation of the page you are on.
 *
 * It lists the terms of the page — all of them, folded, one open at a time. A fixed
 * "?" button in the header opens it as a side sheet on every width. From 1280 px up
 * the sheet can be kept open beside the page ("pinned"); that choice is remembered.
 * Not a search box: what a page means should be readable next to the page.
 *
 * The set of entries changes with the route. A page without entries has neither
 * button nor column — an empty panel would take room and give nothing back.
 */

type Pinning = {
  pinned: boolean
  onPin: (next: boolean) => void
}

/** The "?" in the header. Opens the sheet; with the column pinned it closes the column. */
export function HelpButton({ pinned, onPin, wide }: Pinning & { wide: boolean }) {
  const { t } = useTranslation()
  const key = useHelpKey()
  const [open, setOpen] = useState(false)
  if (key === null) return null

  // Pinned and wide enough: the help is already beside the page, so the button
  // takes it away again. Narrower, the pin is not visible, the sheet still works.
  if (pinned && wide) {
    return (
      <Button
        variant="ghost"
        size="icon"
        aria-label={t('helpPanel.button')}
        aria-pressed
        onClick={() => onPin(false)}
        className="print:hidden"
      >
        <CircleHelp className="size-4" />
      </Button>
    )
  }

  return (
    <Sheet open={open} onOpenChange={setOpen}>
      <SheetTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          aria-label={t('helpPanel.button')}
          className="print:hidden"
        >
          <CircleHelp className="size-4" />
        </Button>
      </SheetTrigger>
      <SheetContent className="w-full overflow-auto sm:max-w-sm print:hidden">
        <SheetHeader>
          <SheetDescription className="font-mono text-[10px] tracking-widest uppercase">
            {t('helpPanel.heading')}
          </SheetDescription>
          <SheetTitle>{helpFor(key).title}</SheetTitle>
        </SheetHeader>
        <div className="flex flex-col gap-3 px-4 pb-4">
          <HelpEntries helpKey={key} />
          {wide && (
            <Button
              variant="outline"
              onClick={() => {
                onPin(true)
                setOpen(false)
              }}
            >
              <PanelRightOpen className="size-4" />
              {t('helpPanel.show')}
            </Button>
          )}
        </div>
      </SheetContent>
    </Sheet>
  )
}

/** The pinned column beside the page. Only from 1280 px up. */
export function HelpColumn({ pinned, onPin }: Pinning) {
  const { t } = useTranslation()
  const key = useHelpKey()
  if (key === null || !pinned) return null

  return (
    <aside className="bg-sidebar sticky top-14 hidden max-h-[calc(100vh-3.5rem)] w-80 shrink-0 flex-col gap-3 overflow-auto border-l p-4 xl:flex print:hidden">
      <div className="flex items-start justify-between gap-2">
        <div className="flex flex-col">
          <span className="text-muted-foreground font-mono text-[10px] tracking-widest uppercase">
            {t('helpPanel.heading')}
          </span>
          <strong className="text-sm font-semibold">{helpFor(key).title}</strong>
        </div>
        <Button
          variant="ghost"
          size="icon"
          className="-mt-1"
          onClick={() => onPin(false)}
          aria-label={t('helpPanel.hide')}
        >
          <PanelRightClose className="size-4" />
        </Button>
      </div>

      <HelpEntries helpKey={key} />

      <p className="text-muted-foreground mt-auto border-t pt-3 text-[11px]">
        {/* TODO: Auszug aus dem Wiki laden statt aus `help.tsx`, sobald es die
            Artikel gibt — und von hier dorthin verlinken. */}
        {t('helpPanel.footer')}
      </p>
    </aside>
  )
}

function HelpEntries({ helpKey }: { helpKey: HelpKey }) {
  const [openEntry, setOpenEntry] = useState<string | null>(null)
  const { entries } = helpFor(helpKey)

  return (
    <div className="flex flex-col gap-1.5">
      {entries.map((entry) => {
        const open = openEntry === entry.id
        return (
          <div
            key={entry.id}
            className={cn('bg-background rounded-lg border', open && 'border-foreground/20')}
          >
            <button
              type="button"
              // Exactly one open: a second one would push the first out of sight
              // and the column would need scrolling to read one paragraph.
              onClick={() => setOpenEntry(open ? null : entry.id)}
              aria-expanded={open}
              className="hover:text-primary flex w-full items-center justify-between gap-2 px-3 py-2 text-left text-[13px] font-medium"
            >
              {entry.title}
              <ChevronRight
                className={cn(
                  'text-muted-foreground size-3.5 shrink-0 transition-transform',
                  open && 'rotate-90'
                )}
              />
            </button>

            {open && (
              <div className="text-muted-foreground flex flex-col gap-2 px-3 pb-3 text-xs leading-relaxed">
                {entry.body}
              </div>
            )}
          </div>
        )
      })}
    </div>
  )
}
