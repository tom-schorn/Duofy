# Component: User account

## Purpose

The person who uses Duofy: how they register, sign in, stay signed in, manage and delete their account. It is not about households or permissions between people; those get their own component.

## Terms

| Term | Meaning |
| --- | --- |
| User account | One person's registration in Duofy, identified by email, owner of their data. |
| Access token | Short-lived proof (15 minutes) that a request comes from a signed-in person; sent with every API call. |
| Refresh token | Long-lived proof (30 days) that is traded for a new token pair without the password. |
| Token store | The place where a host keeps the tokens: SecureStorage in the app, the server session in the web host. |
| Consent | The person's agreement to the privacy policy and the terms, stored with its time at registration. |
| Sign out everywhere | Making every refresh token of the person invalid at once. |
| Hosted version | A server with `Duofy:Hosted` true; only it sends mail (spec 53). |
| Reset link | A one-time link (24 hours) the administrator of a self-hosted server creates so a person sets a new password. |

## Data

- `id` (Guid), `email` (unique without case), password hash (Identity), `first_name` and `last_name` (1–100 characters), `terms_accepted_at`, `email_confirmed` (required for sign-in on the hosted version; accounts from before spec 53 are confirmed), security stamp, lockout counters.
- Notification settings (spec 118, hosted only): `user_id`, `due_today`, `overdue`, `advance_warning`, `household` (all default true), `morning_time` (default 09:00), `evening_enabled` (default true), `evening_time` (default 19:00), `warning_days` (1 to 7, default 2), times in Europe/Berlin to the minute. Spec 84's on/off and time became `overdue` and `morning_time`; the migration set all switches to the old on/off value.
- Password reset link (self-hosted): `id`, `user_id`, `token_hash` (SHA-256), `created_by_id`, `expires_at`, `used_at`.

## Rules

- Anybody may register; registration needs first name, last name, email, a password of at least 10 characters and consent.
- One email belongs to at most one account, compared without case.
- Sign-in errors never reveal whether an email is registered.
- After five failed sign-ins the account is locked for five minutes.
- Deleting the account needs the current password and removes every row the person owns.
- On the hosted version a new account signs in only after confirming its address (link valid 24 hours, resend at most once every five minutes); a forgotten password is reset by a mailed link valid one hour; a new email counts only after confirming it through the new address, and the old address is told.
- On a self-hosted server no mail is sent: the administrator creates a reset link for a forgotten password, and a new email counts at once.
- Changing the email needs the current password; a new password or email signs the person out everywhere.
- Answers to mail requests never reveal whether an address has an account.
- Only the API reads or writes accounts; web and app ask the API.
- Notifications (spec 118): the page "Benachrichtigungen" (`/benachrichtigungen`, reached from "Mehr" and the account page, only where the server has reminders) holds the four switches, the morning and evening time, the evening switch and the warning days; every choice is saved at once and confirmed with "Gespeichert", there is no save button. A time is accepted as "HH:mm" or "HH:mm:ss" (seconds dropped); an empty or impossible time is refused and the stored one stays ("Löschen" in the device's time dialog shows a hint, not an error). The visible notification text names the position and the amount; the push payload holds only an id and the type.

## Interfaces

API under `/api/v1`: `auth/register`, `auth/login`, `auth/refresh`, `auth/logout-everywhere`, `users/me` (GET, PATCH, DELETE), `users/me/password`; see the functional specification of `docs/specs/57-api-foundation`. Spec 53 adds `auth/confirm-email`, `auth/confirm-email/resend`, `auth/password/forgot`, `auth/password/reset`, `auth/confirm-email-change` (hosted only), `auth/password/reset-by-link` (self-hosted only) and `users/me/email`; see `docs/specs/53-email-and-password-reset`.

## Open questions

None.
