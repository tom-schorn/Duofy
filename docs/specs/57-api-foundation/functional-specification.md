# Functional specification: API foundation, users and design

Format: 2

> **Alt (Stand vor der Radzen-Umstellung, Issue 76):** Diese Spezifikation nennt die frühere Oberflächenbibliothek MudBlazor und die Tabler-Symbole. Heute gelten Radzen und Material Symbols, siehe `docs/plans/radzen-plan.md`. Fachliche Regeln und Abläufe bleiben gültig.

## Summary

Duofy gets its own API (`Duofy.Api`) that alone talks to the database. The browser version and the Android app become two thin hosts around the shared pages in `Duofy.Shared` and get their data only from that API. People can register, sign in, sign out, change their name and password and delete their account, on a MudBlazor interface that already looks like Duofy, and the Android app is set up for Google Play from the first build.

## Customer requirements

[customer-requirements.md](customer-requirements.md), requirements R1 to R12. R13 (wont) is only prepared for, see Non-goals.

## Components

- [User account](docs/components/user-account.md)

## Solution

**Projects.** Two new projects and one changed role for the existing ones:

- `Duofy.Schemas` (new, class library): the request and response records of the API, shared by API, web, app and tests, so the three never disagree about a field.
- `Duofy.Api` (new, ASP.NET Core): references `Duofy.Data` and `Duofy.Domain`, hosts ASP.NET Core Identity with the bearer token scheme (`IdentityConstants.BearerScheme`, the built-in tokens chosen on 2026-10-21), applies the migrations at start, and offers the endpoints under `/api/v1` listed in "Data and interfaces". The identity endpoints are written by us in a few lines with `SignInManager` and the bearer token options, the same way `MapIdentityApi` does it internally, because `MapIdentityApi`'s own register endpoint knows no names and no consent.
- `Duofy.Shared` (changed): MudBlazor, the layout, the pages of this foundation, the German resource file, and the typed API clients (`IAuthApi`, `IUserApi`) built on `HttpClient`. It declares the interfaces that each host fills in: `ITokenStore` (where the tokens live) and `IThemePreferenceStore` (where the light/dark choice lives). A shared `DelegatingHandler` adds the access token to every request and, on a 401, refreshes once and repeats the request.
- `Duofy.Web` (changed): no database any more. It removes `DbContext`, Identity stores and the cookie account pages from `feat/data-database`. The browser holds only an HttpOnly cookie of the web host; the API tokens stay on the web server (the "backend for frontend" pattern), so no script in the browser can read them. The human's commitments page stays as a file but leaves the navigation; the human rewires it to the API in the commitments feature.
- `Duofy` (MAUI, changed): Android only for now. Package name `de.tomschorn.duofy`, display name "Duofy", target API level 36, minimum API level 24. Tokens live in `SecureStorage`, the theme choice in `Preferences`. The API address comes from configuration; a Debug build may reach the development computer over plain HTTP on the local network, a Release build only over HTTPS. Release builds produce a signed AAB; keystore path and passwords come from environment variables and are never committed.

**Design.** MudBlazor with one `MudTheme` in `Duofy.Shared`: primary colours taken from the original's `frontend/src/index.css`, the font Geist (bundled, SIL Open Font License) as in the original, light and dark palettes. The mode follows the system until the person picks one; the pick is remembered per device. Layout: app bar with the name "Duofy" and the mode switch, a navigation drawer that is permanent on wide screens and slides in on phones.

**Pages of this foundation** (in `Duofy.Shared`, built by the team as readable examples): Register, Sign in, Account (name, password, sign out everywhere, delete account), Start (greets the person by first name, nothing else yet), Not found. The template pages Counter and Weather are removed.

**Texts.** All visible texts in `Duofy.Shared/Resources/SharedResources.resx` (German as the neutral language), read through `IStringLocalizer<SharedResources>`. API errors are codes (`email_taken`, …); the shared UI maps each code to a German text from the resource file.

**Passwords.** At least 10 characters, no composition rules (current NIST advice); Identity lockout after 5 failed attempts for 5 minutes.

**Prepared for later.** Identity's token providers stay registered and `AppUser.EmailConfirmed` stays in the table, so email confirmation and "forgot password" (R13, issue 53) can be added with an email sender and two endpoints, without changing the user table or the sign-in flow.

## Behaviour

- R1: The only project that references `Duofy.Data` or Entity Framework Core is `Duofy.Api` (and its tests). Web and app call the API over HTTP.
- R2: Register needs first name, last name (each 1–100 characters after trimming), a valid email, a password of at least 10 characters and the consent flag set to true. The email is compared without case. On success the person is not signed in automatically by the API; the shared UI signs in right after. Errors come back as `400` with one code: `first_name_required`, `last_name_required`, `email_invalid`, `email_taken`, `password_too_short`, `terms_not_accepted`. The consent time is stored with the user.
- R3: Sign in with email and password returns an access token (lifetime 15 minutes), a refresh token (lifetime 30 days) and the lifetime in seconds. A wrong password, an unknown email and a locked account all answer `401` with the same code `login_failed`, so nobody learns which addresses exist. Sign out removes the tokens from the device (app) or the web host's session (web) and shows the sign-in page.
- R4: When the access token has expired, the shared handler trades the refresh token once for a new pair, stores it and repeats the request. If the refresh fails, the tokens are removed and the sign-in page shows. On app start with a stored refresh token, the app opens signed in.
- R5: Deleting the account asks for the password again. The API deletes the user and every row the user owns (today: commitments through `OwnerId`); afterwards sign in and refresh fail. The web page `/konto/loeschen` exists in the browser version; without a session it leads to sign-in and back. Its address goes into the Play Console.
- R6: Name changes follow the rules of R2. A password change needs the current password and follows the password rule; the old password stops working at once.
- R7: "Sign out on all devices" changes the user's security stamp, which makes every refresh token issued before invalid; access tokens already handed out run out within 15 minutes.
- R8: Besides the lockout, a rate limit allows 10 requests per minute per client address on `/api/v1/auth/*`; the 11th answers `429`.
- R9: Every page uses the shared layout and MudBlazor components; no Bootstrap remains.
- R10: No visible German or English word is written directly in a `.razor` file of `Duofy.Shared`; every text comes from the resource file.
- R11: See the app part of the Solution; a release build passes Google's 16 KB alignment check.
- R12: The README describes how to pair the phone with wireless debugging, how to start the API so that it listens on the local network, and how to install the Debug app.

## Data and interfaces

**Database** (one new migration in `Duofy.Data`): `AppUser` gets `first_name` (text, required, max 100), `last_name` (text, required, max 100), `terms_accepted_at` (timestamp with time zone, required). The foreign key from commitments to the user deletes in cascade.

**API** (`/api/v1`, JSON in camelCase, records in `Duofy.Schemas`):

| Method and path | Auth | Body | Answers |
| --- | --- | --- | --- |
| `POST /auth/register` | no | `RegisterRequest { firstName, lastName, email, password, acceptedTerms }` | `201`; `400 { code }` |
| `POST /auth/login` | no | `LoginRequest { email, password }` | `200 TokenResponse { tokenType, accessToken, expiresIn, refreshToken }`; `401 { code: login_failed }` |
| `POST /auth/refresh` | no | `RefreshRequest { refreshToken }` | `200 TokenResponse`; `401` |
| `POST /auth/logout-everywhere` | yes | – | `204` |
| `GET /users/me` | yes | – | `200 MeResponse { id, email, firstName, lastName }` |
| `PATCH /users/me` | yes | `UpdateMeRequest { firstName, lastName }` | `200 MeResponse`; `400 { code }` |
| `POST /users/me/password` | yes | `ChangePasswordRequest { currentPassword, newPassword }` | `204`; `400 { code: password_wrong | password_too_short }` |
| `DELETE /users/me` | yes | `DeleteMeRequest { password }` | `204`; `400 { code: password_wrong }` |
| `GET /health` | no | – | `200` |

**Configuration** (`.env.example` and `appsettings`): connection string, API base address for web and app, allowed CORS origins none (web calls server to server), Android signing variables `DUOFY_KEYSTORE_PATH`, `DUOFY_KEYSTORE_PASSWORD`, `DUOFY_KEY_ALIAS`, `DUOFY_KEY_PASSWORD`.

External input: yes

## Acceptance criteria

- [ ] A1 (R1): Given the solution, when a test reads the project files of Duofy.Web, Duofy.Shared and Duofy, then none of them references Duofy.Data or an Entity Framework Core package.
- [ ] A2 (R2): Given no account for anna@example.org, when POST /api/v1/auth/register is sent with first name, last name, that email, a 10-character password and acceptedTerms true, then the answer is 201 and after signing in GET /api/v1/users/me returns the same first and last name.
- [ ] A3 (R2): Given a register request with acceptedTerms false, or an empty first name, or an empty last name, or a 9-character password, or an invalid email, when it is sent, then the answer is 400 with the matching code and no user exists afterwards.
- [ ] A4 (R2): Given an account for anna@example.org, when another person registers with ANNA@example.org, then the answer is 400 with code email_taken.
- [ ] A5 (R3): Given a registered person, when they sign in with the right password, then the answer is 200 with an access token, a refresh token and expiresIn 900; when they use a wrong password, or an unknown email is used, then both answers are 401 with code login_failed.
- [ ] A6 (R3): Given a valid access token, when GET /api/v1/users/me is sent with it, then the answer is 200; when it is sent without a token, then the answer is 401.
- [ ] A7 (R4): Given a refresh token from sign-in, when POST /api/v1/auth/refresh is sent with it, then the answer is 200 with a new token pair; when a changed token is sent, then the answer is 401.
- [ ] A8 (R4): Given the shared auth handler with a stored refresh token and an API that answers the first call with 401, when a shared API client makes a call, then the handler refreshes exactly once, repeats the call, returns its answer and the token store holds the new pair.
- [ ] A9 (R4): Given the shared auth handler and an API whose refresh answers 401, when a call is made, then the token store is empty afterwards and the authentication state is "not signed in".
- [ ] A10 (R3): Given a signed-in person on the shared Account page, when they click sign out, then the token store is empty and the navigation goes to the sign-in page.
- [ ] A11 (R5): Given a signed-in person who owns two commitments, when DELETE /api/v1/users/me is sent with the right password, then the answer is 204, signing in with that email answers 401, and no commitment with their id as owner remains.
- [ ] A12 (R5): Given a signed-in person, when DELETE /api/v1/users/me is sent with a wrong password, then the answer is 400 with code password_wrong and the account still exists.
- [ ] A13 (R5): Given the web host and no session, when /konto/loeschen is requested, then the answer redirects to the sign-in page with a return address of /konto/loeschen.
- [ ] A14 (R6): Given a signed-in person, when PATCH /api/v1/users/me sets new names, then the answer is 200 with the new names; when a name is empty, then the answer is 400 and the old names stay.
- [ ] A15 (R6): Given a signed-in person, when POST /api/v1/users/me/password is sent with the right current password and a new valid one, then the answer is 204, signing in with the new password works and with the old one answers 401.
- [ ] A16 (R7): Given a person signed in on two devices, when one of them sends POST /api/v1/auth/logout-everywhere, then the answer is 204 and a refresh with the other device's refresh token answers 401.
- [ ] A17 (R8): Given five failed sign-ins for one account, when the sixth uses the right password within five minutes, then the answer is 401 with code login_failed.
- [ ] A18 (R8): Given ten requests to /api/v1/auth/login from one client address within a minute, when the eleventh is sent, then the answer is 429.
- [ ] A19 (R9): Given the shared main layout rendered in a component test, when it renders, then it contains the MudBlazor theme provider with the Duofy theme, the app bar and the navigation; when the mode switch is clicked, then the dark mode is on and the choice is passed to IThemePreferenceStore.
- [ ] A20 (R9): Given the solution, when a test searches Duofy.Shared, Duofy.Web and Duofy for Bootstrap files or classes, then none is found.
- [ ] A21 (R10): Given every .razor file in Duofy.Shared, when a test looks for literal text between tags or in Text, Label, Placeholder and Title attributes, then it finds none, and every key used with the localizer exists in SharedResources.resx.
- [ ] A22 (R11): Given the Android project, when a test reads Duofy.csproj and the Android manifest, then the package name is de.tomschorn.duofy, the target SDK is 36, the minimum is 24, Release builds produce an aab and the signing values come from the DUOFY_KEYSTORE_* and DUOFY_KEY_* environment variables.
- [ ] A23 (R11): Given a Release build of the Android app, when the check script runs zipalign with the 16 KB page check on the bundle's native libraries, then it reports them aligned.
- [ ] A24 (R11, R12): Given the Android project, when a test reads the network security configuration, then Debug permits plain HTTP and Release does not.
- [ ] A25 (R12): Given the README, when a test reads it, then it contains a section for testing on an Android phone over Wi-Fi with the steps pair, start API, install app.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | pending |
| R2 | A2, A3, A4 | pending |
| R3 | A5, A6, A10 | pending |
| R4 | A7, A8, A9 | pending |
| R5 | A11, A12, A13 | pending |
| R6 | A14, A15 | pending |
| R7 | A16 | pending |
| R8 | A17, A18 | pending |
| R9 | A19, A20 | pending |
| R10 | A21 | pending |
| R11 | A22, A23, A24 | pending |
| R12 | A24, A25 | pending |

## Threats

- Can someone pretend to be someone else? Only with that person's password or a stolen token; passwords are hashed by Identity, guessing is slowed by lockout and rate limit, access tokens live 15 minutes, and refresh tokens are kept in SecureStorage (app) or on the web server (web), never in browser script storage.
- Can someone change data? Only their own: every user endpoint acts on the user of the token, never on an id from the request.
- Can someone deny an action? Account deletion and password change require the current password and are logged with user id and time (no password, no token in the log).
- Can someone read what they should not? No: /users/me returns only the caller's data, sign-in errors do not reveal whether an email exists, and logs never contain passwords or tokens.
- Can someone bring it to a halt? Floods on the auth endpoints are cut off by the rate limit with 429; other endpoints need a valid token.
- Can someone gain rights they should not have? No roles exist yet; tokens are signed by the server's data protection keys and a changed token is refused (A7).

## Non-goals

- Email confirmation, "forgot password" and any email sending (R13, issue 53) – only kept possible.
- Two-factor sign-in (issue 55), sign-in with Google or other providers.
- Roles, admin page, the first-admin setup code (issue 33).
- Households, household invitations, permissions; feature pages such as accounts, commitments, plans, bookings, import.
- Rewiring the human's commitments page to the API (the human does that in the commitments feature).
- iOS, Windows and Mac builds of the app; a Play Console account, store listing, privacy policy text and the hosting of the API (parking lot P-001, P-002).
- Detecting reuse of an old refresh token and revoking single sessions; only "sign out everywhere".
- Offline mode and push notifications.

## Risks

- The Android SDK and a JDK are not installed on the development computer (checked 2026-10-21; the .NET Android workload is). Installing them changes the computer, so the lead asks the human first; without them A23 and the phone test cannot run.
- Interactive Blazor Server pages cannot write the browser cookie after the page has loaded, so a token refresh in the web host must be kept on the server side; the team follows Microsoft's "backend for frontend" sample and covers it with A8 and A9.
- Identity's refresh tokens cannot be revoked one by one; accepted for now, contained by R7.
- MudBlazor and Testcontainers versions are pinned; the API tests need Docker running.

## Open questions

None.

## Approval

Approved by the human on 2026-10-21

Approved in the dialog after the check-in of 2026-10-21, which decided that parts approved this afternoon may be built today. Password rule (10 characters, no composition rules), lockout and rate limit were proposed by the lead and accepted with the approval.
