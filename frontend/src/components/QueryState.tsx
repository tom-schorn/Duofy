import type { ReactNode } from 'react'

import { ErrorBox } from '@/components/ErrorBox'
import { Skeleton } from '@/components/ui/skeleton'
import { errorText } from '@/lib/api'

/**
 * Loading and errors in one place instead of in every page.
 *
 * Errors show the wording that belongs to the backend code, in a box that can be run
 * again — the API stays free of any language, the UI supplies the sentence.
 */
export function QueryState({
  isPending,
  error,
  onRetry,
  children,
  rows = 3,
}: {
  isPending: boolean
  error: unknown
  /** Runs the query again; without it the box has no retry button. */
  onRetry?: () => void
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
      <ErrorBox onRetry={onRetry}>{errorText(error)}</ErrorBox>
    )
  }

  return <>{children}</>
}
