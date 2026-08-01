import { createContext, useContext, useMemo, useState } from 'react'

/**
 * Welche Brille gerade aufgesetzt ist — der eigene Plan oder ein Haushalt.
 *
 * Ein Plan gehört immer einer Person. Der Haushaltsplan ist keine eigene
 * Tabelle, sondern die Zusammenstellung aller Posten aller Mitglieder mit
 * gesetzter `household_id`. Der Umschalter wechselt also die Sicht, nicht die
 * Daten — dieselbe Miete ist gleichzeitig eigener und gemeinsamer Posten.
 *
 * Bewusst Context statt Zustand-Store: es ist genau ein Wert, und Zustand ist
 * im Projekt nicht installiert.
 *
 * TODO: Auswahl in der URL spiegeln, damit ein Link auf einen Haushaltsplan
 * teilbar bleibt. Bis dahin startet jeder Seitenaufruf beim eigenen Plan.
 */

type ScopeValue = {
  /** null = eigener Plan, sonst die household_id. */
  householdId: string | null
  setHouseholdId: (id: string | null) => void
}

const ScopeContext = createContext<ScopeValue | null>(null)

export function ScopeProvider({ children }: { children: React.ReactNode }) {
  const [householdId, setHouseholdId] = useState<string | null>(null)
  const value = useMemo(() => ({ householdId, setHouseholdId }), [householdId])

  return <ScopeContext value={value}>{children}</ScopeContext>
}

export function useScope() {
  const value = useContext(ScopeContext)
  if (value === null) {
    throw new Error('useScope braucht einen ScopeProvider darüber')
  }
  return value
}
