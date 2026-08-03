import { CalendarDays } from 'lucide-react'
import { useState } from 'react'

import { Button } from '@/components/ui/button'
import { Calendar } from '@/components/ui/calendar'
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover'
import { fromIsoDay, langesDatum, toIsoDay } from '@/lib/dates'

/**
 * Ein Datumsfeld aus dem Theme — Knopf plus Kalender im Popover.
 *
 * Vorher stand hier überall `<input type="date">`. Das sieht in jedem Browser
 * anders aus, ignoriert die Schrift der Oberfläche und zeigt unter Linux ein
 * anderes Format als unter macOS. Der Kalender des Themes sieht überall gleich
 * aus und schreibt Monatsnamen aus.
 *
 * Nach außen bleibt es ein ISO-Tag (`2026-08-03`), wie im ganzen Projekt — die
 * Umrechnung läuft über `lib/dates`, nie über `toISOString`.
 */

type Props = {
  id?: string
  /** ISO-Tag oder leer. */
  value: string
  onChange: (iso: string) => void
  /** Wird im Knopf gezeigt, solange nichts gewählt ist. */
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
            langesDatum(value)
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
            // Nach der Wahl zu — ein offener Kalender über dem Formular
            // verdeckt genau die Felder, zu denen man weitergehen will.
            setOpen(false)
          }}
          autoFocus
        />
      </PopoverContent>
    </Popover>
  )
}
