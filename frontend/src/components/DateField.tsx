import { CalendarDays } from 'lucide-react'
import { useState } from 'react'

import { Button } from '@/components/ui/button'
import { Calendar } from '@/components/ui/calendar'
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover'
import { fromIsoDay, longDate, toIsoDay } from '@/lib/dates'

/**
 * A date field from the theme — a button plus a calendar in a popover.
 *
 * What stood here before was `<input type="date">` everywhere. That looks different
 * in every browser, ignores the interface font and shows a different format on
 * Linux than on macOS. The theme calendar looks the same everywhere and writes
 * month names out.
 *
 * On the outside it stays an ISO day (`2026-08-03`), as everywhere in the project —
 * the conversion runs through `lib/dates`, never through `toISOString`.
 */

type Props = {
  id?: string
  /** An ISO day, or empty. */
  value: string
  onChange: (iso: string) => void
  /** Shown in the button while nothing is selected. */
  placeholder?: string
  disabled?: boolean
}

export function DateField({
  id,
  value,
  onChange,
  placeholder = 'Datum wählen',
  disabled,
}: Props) {
  const [open, setOpen] = useState(false)
  const gewaehlt = value ? fromIsoDay(value) : undefined

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          id={id}
          type="button"
          variant="outline"
          disabled={disabled}
          className="w-full justify-start font-normal tabular-nums"
        >
          <CalendarDays className="size-4 opacity-70" />
          {value ? (
            longDate(value)
          ) : (
            <span className="text-muted-foreground">{placeholder}</span>
          )}
        </Button>
      </PopoverTrigger>
      <PopoverContent align="start" className="w-auto p-0">
        <Calendar
          mode="single"
          selected={gewaehlt}
          defaultMonth={gewaehlt}
          onSelect={(date) => {
            if (!date) return
            onChange(toIsoDay(date))
            // Close after picking — an open calendar covers exactly the fields
            // one wants to move on to.
            setOpen(false)
          }}
          autoFocus
        />
      </PopoverContent>
    </Popover>
  )
}
