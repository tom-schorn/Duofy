# Component: Booking

## Purpose

What really happened with money: an expense, an income or a transfer between own accounts. It is not a plan of what will happen; that is the plan's position.

## Terms

| Term | Meaning |
| --- | --- |
| Expense | Money leaves an account; pot needs, wants or savings. |
| Income | Money arrives on an account; pot income. |
| Transfer | Money moves between two own accounts; no category. A new transfer carries a source pot and a target pot. |
| Pot | One of income, needs (Grundbedarf), wants (Wünsche), savings — the 50/30/20 budgets. |
| Month book | The bookings of one month by their date, as shown on the bookings page, without the two bookings of a carry-over. |

## Data

- `account_id`, `counter_account_id` (transfer only), `date`, `amount` (> 0, two decimals), `pot`, `category_id`, `note` (≤ 200), `owner_id`.
- `target_pot` (transfer only, nullable): the target pot of a transfer; the source pot is the pot of its one line. Empty for older transfers, which move nothing between pots.

## Rules

- The amount is positive; the sign follows from the pot or the transfer direction.
- Every booking except a transfer has a pot and a category; a position is voluntary. The dialog starts an expense without a pot (spec 72).
- A new transfer has exactly one line without a category (its pot is the source pot) and a target pot. Both pots are required, each is needs, wants or savings, and they may be the same; a missing one is answered with `transfer_pots_required`. The pots move money: when source and target pot differ, the amount lowers the source pot and raises the target pot in the booked sum per pot and in the quota actual of the plan month; the same pot on both sides moves nothing, and a transfer whose line sits on a plan position moves nothing either, it counts only through the position (spec 112, R7); no sum, free amount, fixed sum or balance changes (spec 112, amending spec 72, R9). A transfer goes between own accounts only.
- An older transfer without pots stays readable and listed; the dialog asks for the missing pots when it is changed.
- Ticking a position with a target account books a transfer with the pot of the position on both sides.
- The plan year of a booking lies between 2000 and 2100.
- A balancing booking of the carry-over that lowers a balance is an expense in Wünsche with the category Ausgleich; one that raises a balance is an income (spec 72, amendment 2026-11-01).
- The two bookings of a carry-over stand on the last day of the old month (older pairs: the income on the first of the new one). The bookings page and its sums leave them out; PUT and DELETE answer `409 carry_over_booking`. The balance still counts them, and they cancel out (spec 66, amendment 2026-11-01).
- Date and plan month stay separate: the date may lie one month before or after the plan month (spec 66, R7).
- The date lies after the opening date of every touched account and not after today (Europe/Berlin); only the bookings of a carry-over made before the end of the month lie ahead.
- New bookings only on active accounts.
- Deleting asks for confirmation in the UI.

## Interfaces

`/api/v1/bookings` (GET by month, POST, PUT, DELETE), see `docs/specs/59-bookings`.

## Open questions

None.
