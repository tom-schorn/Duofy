/**
 * Der Draht zum Backend.
 *
 * Frontend und Backend laufen auf getrennten Domains — die Basis-URL kommt
 * deshalb aus der Umgebung und wird nirgends hart hineingeschrieben.
 *
 * Das Backend liefert Fehler als **Code** (`{"detail": {"code": "..."}}`),
 * die Übersetzung passiert hier im Frontend. So bleibt die API sprachfrei.
 */

const BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:8000/api/v1'

const TOKEN_KEY = 'duofy-token'

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function setToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token)
}

export function clearToken(): void {
  localStorage.removeItem(TOKEN_KEY)
}

/** Fehler mit dem Code des Backends — das UI übersetzt ihn. */
export class ApiError extends Error {
  code: string
  status: number

  constructor(code: string, status: number) {
    super(code)
    this.name = 'ApiError'
    this.code = code
    this.status = status
  }
}

/** Deutsche Texte zu den Codes, die das Backend kennt. */
const ERROR_TEXT: Record<string, string> = {
  // Anmeldung
  LOGIN_BAD_CREDENTIALS: 'E-Mail oder Passwort stimmt nicht.',
  LOGIN_USER_NOT_VERIFIED: 'Bitte bestätige zuerst deine E-Mail-Adresse.',
  REGISTER_USER_ALREADY_EXISTS: 'Diese E-Mail ist schon vergeben.',
  REGISTER_INVALID_PASSWORD: 'Das Passwort erfüllt die Anforderungen nicht.',

  // Berechtigungen
  not_allowed: 'Dazu fehlt dir die Berechtigung.',
  not_plan_owner: 'Dieser Plan gehört jemand anderem.',
  not_commitment_owner: 'Dieser Vertrag gehört jemand anderem.',
  not_household_owner: 'Das darf nur der Besitzer des Haushalts.',
  not_household_member: 'Du bist kein Mitglied dieses Haushalts.',
  not_a_member: 'Du bist kein Mitglied dieses Haushalts.',

  // Fachliches
  plan_not_found: 'Für diesen Monat gibt es noch keinen Plan.',
  plan_already_exists: 'Für diesen Monat gibt es schon einen Plan.',
  commitment_not_found: 'Der Vertrag existiert nicht mehr.',
  position_not_found: 'Der Posten existiert nicht mehr.',
  household_not_found: 'Der Haushalt existiert nicht mehr.',
  last_owner_cannot_leave:
    'Du bist der letzte Besitzer — übergib den Haushalt, bevor du austrittst.',
  already_a_member: 'Diese Person ist schon im Haushalt.',
  invitation_already_open: 'An diese Adresse läuft schon eine Einladung.',
  invitation_not_found: 'Diese Einladung gibt es nicht.',
  invitation_not_open: 'Diese Einladung wurde schon bearbeitet.',
  invitation_expired: 'Diese Einladung ist abgelaufen.',
  invitation_email_mismatch:
    'Die Einladung gilt für eine andere E-Mail-Adresse.',
  first_due_date_required:
    'Bei nicht-monatlichem Rhythmus braucht es eine erste Fälligkeit.',
  due_day_must_match_first_due_date:
    'Fälligkeitstag und erste Fälligkeit widersprechen sich.',
  target_only_for_savings_goal: 'Ein Zielbetrag gehört nur zu einem Sparziel.',
  remaining_debt_only_for_debt: 'Eine Restschuld gehört nur zu einer Schuld.',
}

export function errorText(error: unknown): string {
  if (error instanceof ApiError) {
    return ERROR_TEXT[error.code] ?? 'Da ist etwas schiefgegangen.'
  }
  return 'Das Backend ist gerade nicht erreichbar.'
}

/** Zieht den Fehler-Code aus der Antwort — egal in welcher Form er kommt. */
function extractCode(body: unknown, status: number): string {
  if (typeof body === 'object' && body !== null && 'detail' in body) {
    const detail = (body as { detail: unknown }).detail
    if (typeof detail === 'string') return detail
    if (typeof detail === 'object' && detail !== null && 'code' in detail) {
      return String((detail as { code: unknown }).code)
    }
    // Validierungsfehler von Pydantic: erste Meldung nehmen.
    if (Array.isArray(detail) && detail.length > 0) {
      const first = detail[0] as { msg?: string }
      return first.msg?.replace('Value error, ', '') ?? `http_${status}`
    }
  }
  return `http_${status}`
}

async function request<T>(
  path: string,
  init: RequestInit = {},
  { form = false }: { form?: boolean } = {}
): Promise<T> {
  const token = getToken()
  const headers = new Headers(init.headers)

  if (!form && init.body !== undefined) {
    headers.set('Content-Type', 'application/json')
  }
  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  const response = await fetch(`${BASE_URL}${path}`, { ...init, headers })

  if (response.status === 401) {
    // Token abgelaufen oder ungültig — wegwerfen, der Schutz leitet um.
    clearToken()
    throw new ApiError('unauthorized', 401)
  }

  if (!response.ok) {
    let body: unknown = null
    try {
      body = await response.json()
    } catch {
      // Antwort ohne JSON — dann bleibt nur der Status.
    }
    throw new ApiError(extractCode(body, response.status), response.status)
  }

  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

export const api = {
  get: <T>(path: string) => request<T>(path),

  post: <T>(path: string, body?: unknown) =>
    request<T>(path, {
      method: 'POST',
      body: body === undefined ? undefined : JSON.stringify(body),
    }),

  patch: <T>(path: string, body: unknown) =>
    request<T>(path, { method: 'PATCH', body: JSON.stringify(body) }),

  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),

  /**
   * fastapi-users erwartet beim Login `application/x-www-form-urlencoded`
   * mit den Feldern `username` und `password` — nicht JSON.
   */
  login: (email: string, password: string) =>
    request<{ access_token: string }>(
      '/auth/jwt/login',
      {
        method: 'POST',
        body: new URLSearchParams({ username: email, password }),
      },
      { form: true }
    ),
}
