import { createContext, useContext, useEffect, useState } from 'react'
import type { ReactNode } from 'react'

/**
 * Light, dark, or whatever the system says.
 *
 * The colour scheme is built for both modes — `index.css` carries two complete sets
 * under `:root` and `.dark`. This provider only sets the class that switches
 * between them.
 */

export type Theme = 'light' | 'dark' | 'system'

const STORAGE_KEY = 'duofy-theme'

const ThemeContext = createContext<{
  theme: Theme
  setTheme: (theme: Theme) => void
}>({ theme: 'system', setTheme: () => {} })

function readStored(): Theme {
  const stored = localStorage.getItem(STORAGE_KEY)
  return stored === 'light' || stored === 'dark' ? stored : 'system'
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<Theme>(readStored)

  useEffect(() => {
    const root = document.documentElement
    const media = window.matchMedia('(prefers-color-scheme: dark)')

    const apply = () => {
      const dark = theme === 'dark' || (theme === 'system' && media.matches)
      root.classList.toggle('dark', dark)
    }

    apply()

    // On "system" the page has to follow when the operating system switches —
    // otherwise it stays stuck in the old mode.
    if (theme !== 'system') return
    media.addEventListener('change', apply)
    return () => media.removeEventListener('change', apply)
  }, [theme])

  function setTheme(next: Theme) {
    if (next === 'system') {
      localStorage.removeItem(STORAGE_KEY)
    } else {
      localStorage.setItem(STORAGE_KEY, next)
    }
    setThemeState(next)
  }

  return (
    <ThemeContext value={{ theme, setTheme }}>{children}</ThemeContext>
  )
}

export function useTheme() {
  return useContext(ThemeContext)
}
