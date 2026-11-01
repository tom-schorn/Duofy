# Component: Account

## Purpose

A place where a person's money sits: a Girokonto, a Sparkonto or cash. It holds no balance of its own; the balance follows from the opening balance and the bookings. It is not a securities account and not a household's shared pot.

## Terms

| Term | Meaning |
| --- | --- |
| Account type | Girokonto, Sparkonto or Bargeld; presets "counts as available". |
| Opening balance | The balance at the end of the opening date. |
| Opening date | The day the opening balance applies to; bookings on or before it are contained in it. |
| Counts as available | Money on the account is spendable (on) or set aside (off). |
| Default account | The one account preselected when booking; at most one per person. |
| Closed account | An account no longer in use: hidden in pickers, bookings kept. |

## Data

- `name` (1–100), `type`, `opening_balance` (two decimals, may be negative), `opening_date`, `counts_as_available`, `iban` (optional, normalised, checked), `is_default`, `active`, `owner_id`.

## Rules

- Balance = opening balance + bookings after the opening date.
- The first account created automatically at registration opens the day before registration (Europe/Berlin), so a booking is possible on the first day; every other account takes no booking on its opening date (spec 59, amendment 2026-11-01).
- At most one default account per person; the first Girokonto becomes default.
- Accounts are closed, never deleted; closing removes the default mark.
- Only the owner sees and changes an account.

## Interfaces

`/api/v1/accounts` (GET, POST, PATCH), see `docs/specs/58-accounts`.

## Open questions

None.
