import { useEffect, useState } from 'react'
import { Navigate, Outlet, useLocation } from 'react-router'

import { getToken, refreshSession } from '@/lib/api'
import { Skeleton } from '@/components/ui/skeleton'

/**
 * Guard for everything behind the sign-in.
 *
 * The access token lives in memory only, so a reload always starts without one.
 * Checking for it would send every returning visitor to the sign-in page, even
 * though their session is fine — so on the first mount this asks the backend
 * instead: the refresh cookie either yields a token or it does not.
 *
 * That is why there are three states and not two. `undefined` means the answer has
 * not arrived; showing a skeleton for that moment is the whole cost of never
 * signing in again for a month.
 *
 * The original path travels along, so that after signing in you continue where you
 * meant to go.
 */
export function RequireAuth() {
  const location = useLocation()
  const [token, setTokenState] = useState(getToken())

  useEffect(() => {
    if (token !== undefined) return
    // Not awaited on purpose: the state update below is what re-renders.
    void refreshSession().then(setTokenState)
  }, [token])

  if (token === undefined) {
    return (
      <div className="flex flex-col gap-4 p-6">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-32 w-full" />
      </div>
    )
  }

  if (token === null) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return <Outlet />
}
