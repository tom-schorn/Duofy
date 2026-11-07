# Functional specification: A sign-in holds seven days and is extended by use

Format: 2

## Summary

The refresh token lifetime goes from 30 to 7 days. Because every refresh issues a new refresh token, each use within the 7 days extends the sign-in by 7 days from then. The web session cookie and its token cache follow the same 7 days.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R4.

## Components

None.

## Solution

`Backend/Duofy.Backend/Api/Program.cs`: `RefreshTokenExpiration = TimeSpan.FromDays(7)`, named as one constant with a German comment (the human, 05.11.2026). `Web/Duofy.Web/Session/SessionCookie.cs` (`MaxAge`) and `SessionTokenCache.cs` (`AbsoluteExpirationRelativeToNow`) use the same 7 days; the cookie is renewed (same id, max age 7 days) on every normal page request that carries a valid session id, not on `/_blazor` (the human, card P-107, 05.11.2026). When the token cache entry of a running web connection has expired, the API client treats it as signed out: the authentication state changes and the sign-in page opens, without an error. The apps need no change: on a failed refresh the API client already clears the store and shows the sign-in page; this is checked by a test, not changed.

## Behaviour

- R1: A refresh token older than 7 days is refused by `POST /auth/refresh`.
- R2: A refresh on day 6 returns a new refresh token that is valid 7 days from day 6.
- R3: The web session cookie has a max age of 7 days and is renewed on every normal page request with a valid session id.
- R4: A refused refresh leads to the sign-in page without an error banner.

## Data and interfaces

No API shape change.

External input: no

## Acceptance criteria

- [x] A1 (R1): Given a refresh token issued 8 days ago (fake clock), when it is used at POST /auth/refresh, then the API answers 401.
- [x] A2 (R2): Given a refresh token issued 6 days ago, when it is used, then a new token pair is returned and that refresh token is still accepted 6 days later.
- [x] A3 (R3): Given a web sign-in, when the session cookie is written or renewed, then its max age is 7 days.
- [x] A5 (R3): Given a request with a valid session cookie, when a normal page is requested, then the cookie is sent again with the same id and a max age of 7 days.
- [x] A6 (R4): Given a running web connection whose token cache entry has expired, when the next API call is made, then no refresh is tried, the state changes to "signed out" and the call fails as "not signed in".
- [x] A4 (R4): Given a stored token pair whose refresh is refused, when the API client refreshes, then the token store is cleared and the state is "signed out" without an error.
- [x] A7 (R4): Given the call fails as "not signed in", when the start page loads, then it shows no error and leaves the redirect to the sign-in page.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | `AuthEndpointsTests.A1_a_refresh_token_older_than_seven_days_is_refused` |
| R2 | A2 | `AuthEndpointsTests.A2_a_refresh_within_seven_days_extends_the_sign_in` |
| R3 | A3 | `SessionCookieTests.A3_the_session_cookie_lives_seven_days`, `SessionCookieTests.A3_the_token_cache_keeps_the_tokens_seven_days_from_each_save` |
| R3 | A5 | `WebHostTests.Known_session_cookie_is_renewed_with_the_same_id` |
| R4 | A6 | `AuthTokenHandlerTests.Signs_out_and_notifies_when_the_stored_tokens_have_disappeared` |
| R4 | A4 | `AuthTokenHandlerTests.Clears_the_tokens_and_signs_out_when_the_refresh_answers_401` (existing) |
| R4 | A7 | `StartPageTests.An_ended_sign_in_shows_no_error_when_the_user_cannot_be_loaded`, `StartPageTests.An_ended_sign_in_shows_no_error_when_the_plan_cannot_be_loaded`, `StartPageTests.An_ended_sign_in_leads_from_the_start_page_to_the_sign_in_page_through_the_router` |

## Non-goals

- No fingerprint or passkey sign-in (own issue, V2).
- No shared Android release key (own topic).

## Risks

Existing sessions keep their 30-day refresh tokens until their next refresh; after that the 7 days apply.

## Open questions

None.

## Approval

Approved by the human on 2026-11-05 (in chat after the check-in).
