import { createContext, useContext, useEffect, useState } from 'react'
import type { ReactNode } from 'react'

/**
 * Hell, dunkel oder was das System sagt.
 *
 * Die Farbrichtung „Tinte" ist für beide Modi gebaut — in `index.css` stehen
 * unter `:root` und `.dark` zwei vollständige Sätze. Dieser Provider setzt nur
 * die Klasse, die zwischen ihnen umschaltet.
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

    // Bei „System" muss die Seite mitziehen, wenn das Betriebssystem
    // umschaltet — sonst bleibt sie im alten Modus hängen.
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
