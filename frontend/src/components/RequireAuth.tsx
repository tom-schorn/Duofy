import { Navigate, Outlet, useLocation } from 'react-router'

import { getToken } from '@/lib/api'

/**
 * Guard for everything behind the sign-in.
 *
 * Only checks whether a token exists at all — whether it is valid is the backend
 * decision. When it expires, the API client throws it away and the next navigation
 * ends up here.
 *
 * The original path travels along, so that after signing in you continue where you
 * meant to go.
 */
export function RequireAuth() {
  const location = useLocation()

  if (getToken() === null) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return <Outlet />
}
