import { Navigate, Outlet, useLocation } from 'react-router'

import { getToken } from '@/lib/api'

/**
 * Schutz vor allem, was hinter der Anmeldung liegt.
 *
 * Prüft nur, ob überhaupt ein Token da ist — ob es gültig ist, entscheidet
 * das Backend. Läuft es ab, wirft der API-Client es weg und der nächste
 * Seitenwechsel landet hier.
 *
 * Der ursprüngliche Pfad wandert mit, damit man nach der Anmeldung dort
 * weitermacht, wo man hin wollte.
 */
export function RequireAuth() {
  const location = useLocation()

  if (getToken() === null) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return <Outlet />
}
