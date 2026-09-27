import type { ReactNode } from 'react'

import { EmptyState } from '@/components/EmptyState'
import { ErrorBox } from '@/components/ErrorBox'
import { Skeleton } from '@/components/ui/skeleton'
import { ApiError, errorText } from '@/lib/api'

/**
 * Loading and errors in one place instead of in every page.
 *
 * Errors show the wording that belongs to the backend code, in a box that can be run
 * again — the API stays free of any language, the UI supplies the sentence.
 *
 * Missing insight is not a failure: `notShared` swaps the box for the page's usual
 * empty state, without a retry button — trying again changes nothing about a grant
 * someone else has not given (#217).
 */
export function QueryState({
  isPending,
  error,
  onRetry,
  children,
  rows = 3,
  notShared,
}: {
  isPending: boolean
  error: unknown
  /** Runs the query again; without it the box has no retry button. */
  onRetry?: () => void
  children: ReactNode
  rows?: number
  /** Shown instead of the error box when the code is `no_insight_granted`. */
  notShared?: ReactNode
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
    if (notShared !== undefined && error instanceof ApiError && error.code === 'no_insight_granted') {
      return <EmptyState>{notShared}</EmptyState>
    }
    return (
      <ErrorBox onRetry={onRetry}>{errorText(error)}</ErrorBox>
    )
  }

  return <>{children}</>
}
