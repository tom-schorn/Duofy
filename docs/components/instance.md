# Component: Instance

## Purpose

One running Duofy server and the person who runs it: who may register, who administers it and which legal texts it publishes. It is not about the data of the people on it; an administrator never sees that.

## Terms

| Term | Meaning |
| --- | --- |
| Instance | One Duofy server with its database, hosted or self-hosted. |
| Administrator | A user with `is_instance_admin`, who manages registration invitations. |
| Setup code | A secret in the environment that makes the first administrator while none exists. |
| Registration mode | `open`, `invite` or `closed`, from the environment; `invite` by default. |
| Registration invitation | A link `<server>/registrieren?einladung=<token>`, valid 7 days, usable once, optionally for one email address. |
| Legal texts | Imprint, privacy policy and terms, each a configured text file, public, plain text. |

## Data

- Users: `is_instance_admin` (bool).
- Registration invitation: `id`, `token` (32 random bytes, URL-safe, unique), `created_by_id`, `email` (optional, lower case), `created_at`, `expires_at`, `used_at`, `used_by_id`.
- Configuration: `SetupCode`, `RegistrationMode`, `ImprintFile`, `PrivacyFile`, `TermsFile`; `Hosted` and `Mail:Host`, `Port`, `User`, `Password`, `From`, `UseTls` (spec 53).

## Rules

- The setup code works only while no administrator exists, at registration or for an existing account.
- The registration mode applies to everybody except the setup-code path.
- An invitation is used once; revoking deletes it.
- The last administrator cannot delete their account.
- Administrators get no access to plans, bookings, accounts or contracts of others.
- A legal text that is not configured, unreadable, empty or larger than 200 KB does not exist.
- Only the hosted version sends mail; it does not start without mail host, port, sender and public address.
- On the hosted version a registration invitation with an address is also mailed there, at most 20 per administrator and day.
- On a self-hosted server the administrator creates password reset links; only the administrator learns whether an address has an account.

## Interfaces

`GET /api/v1/instance`, `POST /api/v1/auth/register` (setup code, invitation token), `POST /api/v1/users/me/claim-admin`, `/api/v1/admin/invitations` (GET, POST, DELETE), `/api/v1/legal` and `/api/v1/legal/{name}`; see `docs/specs/70-instance`. `/api/v1/instance` names `hosted`, and `/api/v1/admin/password-reset-links` (POST, self-hosted only) comes with `docs/specs/53-email-and-password-reset`.

## Open questions

None.
