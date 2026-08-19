import { useEffect, useState } from 'react'
import { ChevronLeft, ChevronDown, Check } from 'lucide-react'

import { Button } from '@/components/ui/button'
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover'
import {
  BLOCK_DOT,
  BLOCK_SUGGESTION,
  CATEGORY_GROUPS,
  CATEGORY_LABEL,
  type Category,
} from '@/lib/domain'
import { cn } from '@/lib/utils'

/**
 * Choosing a category in two steps: first the group, then the entry.
 *
 * Forty-five entries in one list are a lot to scroll, and the import screen asks
 * for one on every row. Eight groups of three to eight are quicker to aim at.
 *
 * ## Why the second step keeps saying which group it is in
 *
 * Some names exist more than once. "Versicherung & Steuern" sits under Wohnen
 * **and** under Mobilität, and there is a plain "Versicherung" under Persönlich.
 * Whoever picks the group first has to guess right, and the other two are then
 * invisible. The heading and the way back are what make that mistake cheap
 * instead of confusing.
 *
 * Its own component because four forms ask the same question — commitment,
 * position, book and import — and the answer has to look identical in all of
 * them.
 */
export function CategoryPicker({
  value,
  onChange,
  disabled = false,
  placeholder = 'Kategorie wählen …',
  className,
}: {
  value: Category | null
  onChange: (category: Category) => void
  disabled?: boolean
  placeholder?: string
  className?: string
}) {
  const [open, setOpen] = useState(false)
  const [group, setGroup] = useState<string | null>(null)

  // Reopening starts at the groups again. Keeping the last one would help
  // whoever books ten groceries in a row and mislead everybody else.
  useEffect(() => {
    if (!open) setGroup(null)
  }, [open])

  const chosen = CATEGORY_GROUPS.find((entry) => entry.label === group)

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="outline"
          disabled={disabled}
          className={cn(
            'w-full justify-between font-normal',
            value === null && 'text-muted-foreground',
            className
          )}
        >
          <span className="flex min-w-0 items-center gap-2">
            {value !== null && (
              <span
                className={cn('size-2 shrink-0 rounded-full', BLOCK_DOT[BLOCK_SUGGESTION[value]])}
              />
            )}
            <span className="truncate">
              {value === null ? placeholder : CATEGORY_LABEL[value]}
            </span>
          </span>
          <ChevronDown className="size-4 shrink-0 opacity-50" />
        </Button>
      </PopoverTrigger>

      <PopoverContent align="start" className="w-64 p-0">
        {chosen === undefined ? (
          <ul className="max-h-80 overflow-y-auto py-1">
            {CATEGORY_GROUPS.map((entry) => (
              <li key={entry.label ?? 'ungrouped'}>
                <button
                  type="button"
                  onClick={() => setGroup(entry.label)}
                  className="hover:bg-accent flex w-full items-center justify-between px-3 py-2 text-sm"
                >
                  {entry.label ?? 'Ohne Gruppe'}
                  <span className="text-muted-foreground text-xs tabular-nums">
                    {entry.categories.length}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        ) : (
          <>
            <button
              type="button"
              onClick={() => setGroup(null)}
              className="hover:bg-accent border-border flex w-full items-center gap-1.5 border-b px-3 py-2 text-sm font-medium"
            >
              <ChevronLeft className="size-4" />
              {chosen.label}
            </button>
            <ul className="max-h-72 overflow-y-auto py-1">
              {chosen.categories.map((category) => (
                <li key={category}>
                  <button
                    type="button"
                    onClick={() => {
                      onChange(category)
                      setOpen(false)
                    }}
                    className="hover:bg-accent flex w-full items-center gap-2 px-3 py-2 text-sm"
                  >
                    <span
                      className={cn(
                        'size-2 shrink-0 rounded-full',
                        BLOCK_DOT[BLOCK_SUGGESTION[category]]
                      )}
                    />
                    <span className="truncate">{CATEGORY_LABEL[category]}</span>
                    {category === value && <Check className="ml-auto size-4 shrink-0" />}
                  </button>
                </li>
              ))}
            </ul>
          </>
        )}
      </PopoverContent>
    </Popover>
  )
}
