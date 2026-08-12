/**
 * The wire to the backend.
 *
 * Frontend and backend can live on separate domains, so the base URL comes from
 * the environment and is never hard-coded.
 *
 * The backend reports errors as a **code** (`{"detail": {"code": "..."}}`) and
 * the wording is added here. That keeps the API free of any language.
 *
 * ## Two tokens, and why only one of them is here
 *
 * The **access token** lives in the module variable below — in memory, nowhere
 * else. A reload loses it, which is intended: an injected script cannot read it out
 * of storage, and it is only good for fifteen minutes anyway.
 *
 * The **refresh token** is not in this file at all. It sits in an HttpOnly cookie,
 * so JavaScript never sees it, and the browser sends it to the refresh endpoint by
 * itself. That is also why it survives on iOS, where script-writable storage is
 * deleted after seven days without interaction.
 *
 * Every request therefore goes out with `credentials: 'include'`; without it the
 * browser would leave the cookie at home.
 */

const BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:8000/api/v1'

/**
 * The access token. In memory on purpose — see the note above.
 *
 * `undefined` means "not asked yet", `null` means "asked, and there is no session".
 * The difference matters at start-up: the route guard has to wait for the answer
 * instead of sending a returning visitor to the sign-in page.
 */
let accessToken: string | null | undefined = undefined

export function getToken(): string | null | undefined {
  return accessToken
}

export function setToken(token: string): void {
  accessToken = token
}

export function clearToken(): void {
  accessToken = null
}

/**
 * Ask the backend for a new access token, using the cookie.
 *
 * **One request for everybody.** A page with five queries produces five 401s at
 * once; without this the client would fire five refreshes, the first would rotate
 * the token and the other four would arrive with a value that has just been
 * replaced — which the backend reads as a stolen token and answers by ending every
 * session. The pending promise is therefore shared.
 */
let pending: Promise<string | null> | null = null

export function refreshSession(): Promise<string | null> {
  pending ??= (async () => {
    try {
      const response = await fetch(`${BASE_URL}/auth/refresh`, {
        method: 'POST',
        credentials: 'include',
      })
      if (!response.ok) {
        accessToken = null
        return null
      }
      const body = (await response.json()) as { access_token: string }
      accessToken = body.access_token
      return accessToken
    } catch {
      // The backend is unreachable. Not the same as "no session", but there is
      // nothing to work with either way.
      accessToken = null
      return null
    } finally {
      pending = null
    }
  })()

  return pending
}

/** An error carrying the backend code — the UI turns it into a sentence. */
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

/** German wording for the codes the backend knows. */
const ERROR_TEXT: Record<string, string> = {
  // Sign-in
  LOGIN_BAD_CREDENTIALS: 'E-Mail oder Passwort stimmt nicht.',
  LOGIN_USER_NOT_VERIFIED: 'Bitte bestätige zuerst deine E-Mail-Adresse.',
  REGISTER_USER_ALREADY_EXISTS: 'Diese E-Mail ist schon vergeben.',
  REGISTER_INVALID_PASSWORD: 'Das Passwort erfüllt die Anforderungen nicht.',

  // Permissions
  not_allowed: 'Dazu fehlt dir die Berechtigung.',
  not_plan_owner: 'Dieser Plan gehört jemand anderem.',
  not_commitment_owner: 'Dieser Vertrag gehört jemand anderem.',
  not_household_owner: 'Das darf nur der Besitzer des Haushalts.',
  not_household_member: 'Du bist kein Mitglied dieses Haushalts.',
  not_a_member: 'Du bist kein Mitglied dieses Haushalts.',

  // Domain rules
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

/** Pull the error code out of the response, whatever shape it arrives in. */
function extractCode(body: unknown, status: number): string {
  if (typeof body === 'object' && body !== null && 'detail' in body) {
    const detail = (body as { detail: unknown }).detail
    if (typeof detail === 'string') return detail
    if (typeof detail === 'object' && detail !== null && 'code' in detail) {
      return String((detail as { code: unknown }).code)
    }
    // Pydantic validation errors: take the first message.
    if (Array.isArray(detail) && detail.length > 0) {
      const first = detail[0] as { msg?: string }
      return first.msg?.replace('Value error, ', '') ?? `http_${status}`
    }
  }
  return `http_${status}`
}

async function send(
  path: string,
  init: RequestInit,
  form: boolean
): Promise<Response> {
  const headers = new Headers(init.headers)

  if (!form && init.body !== undefined) {
    headers.set('Content-Type', 'application/json')
  }
  if (accessToken) {
    headers.set('Authorization', `Bearer ${accessToken}`)
  }

  // `include`, not the default: without it the browser omits the refresh cookie,
  // and a cross-origin request would never carry a session at all.
  return fetch(`${BASE_URL}${path}`, {
    ...init,
    headers,
    credentials: 'include',
  })
}

async function request<T>(
  path: string,
  init: RequestInit = {},
  { form = false, retry = true }: { form?: boolean; retry?: boolean } = {}
): Promise<T> {
  let response = await send(path, init, form)

  // A 401 usually means the fifteen minutes are up, not that the session is over.
  // So: fetch a new access token once and repeat the request.
  //
  // `retry` guards against a loop — the repeat is not allowed to try again, and
  // the refresh call itself never comes through here.
  if (response.status === 401 && retry) {
    const fresh = await refreshSession()
    if (fresh !== null) {
      response = await send(path, init, form)
    }
  }

  if (response.status === 401) {
    // Now it really is over: no session, or the backend refused the refresh.
    clearToken()
    throw new ApiError('unauthorized', 401)
  }

  if (!response.ok) {
    let body: unknown = null
    try {
      body = await response.json()
    } catch {
      // Response without JSON — then the status is all there is.
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
   * Sign in. `/auth/login` rather than fastapi-users' `/auth/jwt/login`, because
   * only ours also starts a session and sets the refresh cookie.
   *
   * The body is `application/x-www-form-urlencoded` with the fields `username` and
   * `password`, as the OAuth2 password flow prescribes — `username` carries the
   * email address.
   *
   * `retry: false`: a 401 here means the password is wrong. Refreshing and trying
   * again would be pointless and would hide the actual error.
   */
  login: (email: string, password: string) =>
    request<{ access_token: string }>(
      '/auth/login',
      {
        method: 'POST',
        body: new URLSearchParams({ username: email, password }),
      },
      { form: true, retry: false }
    ),

  /**
   * Sign out. Deletes the session on the server, so it takes effect at once rather
   * than whenever the refresh token would have expired.
   */
  logout: () => request<void>('/auth/logout', { method: 'POST' }, { retry: false }),
}
