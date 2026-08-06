import type { ReactNode } from 'react'

import { Skeleton } from '@/components/ui/skeleton'
import { errorText } from '@/lib/api'

/**
 * Loading and errors in one place instead of in every page.
 *
 * Errors show the wording that belongs to the backend code — the API stays free of
 * any language, the UI supplies the sentence.
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
