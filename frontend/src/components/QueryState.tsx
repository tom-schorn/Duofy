import type { ReactNode } from 'react'

import { Skeleton } from '@/components/ui/skeleton'
import { errorText } from '@/lib/api'

/**
 * Laden und Fehler an einer Stelle, statt in jeder Seite neu.
 *
 * Fehler zeigen den übersetzten Text zum Code des Backends — die API bleibt
 * sprachfrei, das UI übersetzt.
 */
export function QueryState({
  isPending,
  error,
  children,
  rows = 3,
}: {
  isPending: boolean
  error: unknown
  children: ReactNode
  rows?: number
}) {
  if (isPending) {
    return (
      <div className="flex flex-col gap-3">
        {Array.from({ length: rows }, (_, index) => (
          <Skeleton key={index} className="h-20 w-full" />
        ))}
      </div>
    )
  }

  if (error) {
    return (
      <p className="border-destructive bg-destructive/10 text-destructive rounded-lg border px-4 py-3 text-sm">
        {errorText(error)}
      </p>
    )
  }

  return <>{children}</>
}
