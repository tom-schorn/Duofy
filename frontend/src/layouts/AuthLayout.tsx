import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'

import { ThemeToggle } from '@/components/ThemeToggle'
import { blockLabel, type Block } from '@/lib/domain'

/**
 * Split screen for sign-in and registration.
 *
 * The brand surface on the left, the form on the right. Below `lg` the left half
 * disappears — on a phone only the form matters.
 *
 * The left half carries the class `dark` because it is a dark surface: that makes
 * `bg-chart-*` and `text-foreground` resolve to the dark values of the palette
 * automatically.
 */

/** The three budgets — 50 · 30 · 20. The backend enum is called `Block`. */
const BLOCKS: { block: Block; color: string }[] = [
  { block: 'needs', color: 'bg-chart-1' },
  { block: 'wants', color: 'bg-chart-2' },
  { block: 'savings', color: 'bg-chart-4' },
]

export function AuthLayout({ children }: { children: ReactNode }) {
  const { t } = useTranslation()

  return (
    <div className="grid min-h-svh lg:grid-cols-2">
      <aside className="dark hidden flex-col justify-between bg-[#1E3A5F] p-12 lg:flex">
        <span className="text-foreground font-heading text-xl font-semibold">
          Duofy
        </span>

        <div className="flex flex-col gap-10">
          <blockquote className="text-foreground font-heading max-w-md text-3xl leading-tight font-semibold text-balance">
            {t('auth.claim')}
          </blockquote>

          <ul className="flex flex-col gap-3">
            {BLOCKS.map((block) => (
              <li
                key={block.block}
                className="text-foreground/80 flex items-center gap-3 text-sm"
              >
                <span className={`size-2.5 rounded-sm ${block.color}`} />
                {blockLabel(block.block)}
              </li>
            ))}
          </ul>
        </div>

        <p className="text-foreground/70 max-w-xs text-sm">
          {t('auth.tagline')}
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
