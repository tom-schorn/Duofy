import type { ReactNode } from 'react'

import { ThemeToggle } from '@/components/ThemeToggle'

/**
 * Split-Screen für Login und Registrierung.
 *
 * Links die Markenfläche in Marineblau, rechts das Formular. Unter `lg`
 * verschwindet die linke Seite — auf dem Handy zählt nur das Formular.
 *
 * Die linke Seite trägt die Klasse `dark`, weil sie eine dunkle Fläche ist:
 * dadurch lösen `bg-chart-*` und `text-foreground` dort automatisch zu den
 * Dunkel-Werten der Palette auf.
 */

/** Die drei Budgets — 50 · 30 · 20. Im Backend heißt das Enum `Block`. */
const BLOCKS = [
  { label: 'Fixkosten', color: 'bg-chart-1' },
  { label: 'Wünsche', color: 'bg-chart-2' },
  { label: 'Sparen', color: 'bg-chart-4' },
]

export function AuthLayout({ children }: { children: ReactNode }) {
  return (
    <div className="grid min-h-svh lg:grid-cols-2">
      <aside className="dark hidden flex-col justify-between bg-[#1E3A5F] p-12 lg:flex">
        <span className="text-foreground font-heading text-xl font-semibold">
          Duofy
        </span>

        <div className="flex flex-col gap-10">
          <blockquote className="text-foreground font-heading max-w-md text-3xl leading-tight font-semibold text-balance">
            Duofy plant Geld, es zählt es nicht.
          </blockquote>

          <ul className="flex flex-col gap-3">
            {BLOCKS.map((block) => (
              <li
                key={block.label}
                className="text-foreground/80 flex items-center gap-3 text-sm"
              >
                <span className={`size-2.5 rounded-sm ${block.color}`} />
                {block.label}
              </li>
            ))}
          </ul>
        </div>

        <p className="text-foreground/70 max-w-xs text-sm">
          Gemeinsam planen, getrennt besitzen.
        </p>
      </aside>

      <main className="relative flex items-center justify-center px-6 py-12">
        {/* Auch vor der Anmeldung umschaltbar — sonst sitzt man bis zum
            Login im falschen Modus. */}
        <div className="absolute top-4 right-4">
          <ThemeToggle />
        </div>
        <div className="w-full max-w-sm">{children}</div>
      </main>
    </div>
  )
}
