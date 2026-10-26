# Component: Booking

## Purpose

What really happened with money: an expense, an income or a transfer between own accounts. It is not a plan of what will happen; that is the plan's position.

## Terms

| Term | Meaning |
| --- | --- |
| Expense | Money leaves an account; pot needs, wants or savings. |
| Income | Money arrives on an account; pot income. |
| Transfer | Money moves between two own accounts; no category, optionally pot savings. |
| Pot | One of income, needs (Grundbedarf), wants (Wünsche), savings — the 50/30/20 budgets. |
| Month book | The bookings of one month, as shown on the bookings page. |

## Data

- `account_id`, `counter_account_id` (transfer only), `date`, `amount` (> 0, two decimals), `pot`, `category_id`, `note` (≤ 200), `owner_id`.

## Rules

- The amount is positive; the sign follows from the pot or the transfer direction.
- Every booking except a transfer has a pot and a category.
- The date lies after the opening date of every touched account and not after today (Europe/Berlin).
- New bookings only on active accounts.
- Deleting asks for confirmation in the UI.

## Interfaces

`/api/v1/bookings` (GET by month, POST, PUT, DELETE), see `docs/specs/59-bookings`.

## Open questions

None.
