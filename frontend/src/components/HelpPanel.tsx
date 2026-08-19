import { useState } from 'react'
import { useLocation, useSearchParams } from 'react-router'
import { ChevronRight, PanelRightClose, PanelRightOpen } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { HELP, type HelpKey } from '@/lib/help'
import { cn } from '@/lib/utils'

/**
 * The explanation column on the right.
 *
 * It lists the terms of the page you are on — all of them, folded, one open at a
 * time. Not a search box and not a question mark to hunt for: what a page means
 * should be readable next to the page, without leaving it.
 *
 * The set of entries changes with the route. A column that says the same thing
 * everywhere stops being read after two days.
 */

/** Remembered across pages and reloads — a column you closed should stay closed. */
const STORAGE_KEY = 'duofy.help.open'

/** Which set of entries belongs to the current route. */
function helpKeyFor(pathname: string, tab: string | null): HelpKey | null {
  if (pathname.startsWith('/plan/')) return tab === 'book' ? 'book' : 'plan'
  if (pathname === '/import') return 'import'
  if (pathname === '/plan') return 'plans'
  if (pathname === '/contracts') return 'commitments'
  if (pathname === '/accounts') return 'accounts'
  if (pathname === '/household') return 'household'
  return null
}

export function HelpPanel() {
  const location = useLocation()
  const [params] = useSearchParams()
  const [shown, setShown] = useState(
    () => localStorage.getItem(STORAGE_KEY) !== 'false'
  )
  const [openEntry, setOpenEntry] = useState<string | null>(null)

  const key = helpKeyFor(location.pathname, params.get('tab'))
  // A page without entries gets no column at all — an empty panel would take the
  // width and give nothing back.
  if (key === null) return null

  const help = HELP[key]

  function toggle(next: boolean) {
    setShown(next)
    localStorage.setItem(STORAGE_KEY, String(next))
  }

  if (!shown) {
    return (
      <div className="sticky top-14 hidden shrink-0 border-l p-2 xl:block print:hidden">
        <Button
          variant="ghost"
          size="icon"
          onClick={() => toggle(true)}
          aria-label="Erklärungen einblenden"
        >
          <PanelRightOpen className="size-4" />
        </Button>
      </div>
    )
  }

  return (
    <aside className="bg-sidebar sticky top-14 hidden max-h-[calc(100vh-3.5rem)] w-80 shrink-0 flex-col gap-3 overflow-auto border-l p-4 xl:flex print:hidden">
      <div className="flex items-start justify-between gap-2">
        <div className="flex flex-col">
          <span className="text-muted-foreground font-mono text-[10px] tracking-widest uppercase">
            Erklärung
          </span>
          <strong className="text-sm font-semibold">{help.title}</strong>
        </div>
        <Button
          variant="ghost"
          size="icon"
          className="-mt-1"
          onClick={() => toggle(false)}
          aria-label="Erklärungen ausblenden"
        >
          <PanelRightClose className="size-4" />
        </Button>
      </div>

      <div className="flex flex-col gap-1.5">
        {help.entries.map((entry) => {
          const open = openEntry === entry.id
          return (
            <div
              key={entry.id}
              className={cn(
                'bg-background rounded-lg border',
                open && 'border-foreground/20'
              )}
            >
              <button
                type="button"
                // Exactly one open: a second one would push the first out of sight
                // and the column would need scrolling to read one paragraph.
                onClick={() => setOpenEntry(open ? null : entry.id)}
                aria-expanded={open}
                className="flex w-full items-center justify-between gap-2 px-3 py-2 text-left text-[13px] font-medium hover:text-primary"
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

      <p className="text-muted-foreground mt-auto border-t pt-3 text-[11px]">
        {/* TODO: Auszug aus dem Wiki laden statt aus `help.tsx`, sobald es die
            Artikel gibt — und von hier dorthin verlinken. */}
        Kurzfassung. Die ausführlichen Artikel entstehen im Wiki.
      </p>
    </aside>
  )
}
