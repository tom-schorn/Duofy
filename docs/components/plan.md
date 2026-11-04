# Component: Plan

## Purpose

What a person expects to pay and receive in one month, and how far it has happened. A plan belongs to one person and one month. It is not a budget limit and not a booking; bookings fill its positions.

## Terms

| Term | Meaning |
| --- | --- |
| Plan | The month of one person, created when it is first opened. |
| Position | One line of a plan, copied from a contract or added by hand. |
| One-off position | A position without contract, valid only in its month. |
| Booked | The sum of the booking lines assigned to a position. |
| Done | The position was ticked off by hand; a difference to the plan may stay. |
| Change notice | The list of differences between a month and the current contracts. |
| Plan month | The month a booking counts in; may differ from its date by one month. |
| Quota | The target share of income for Bedürfnisse, Wünsche and Sparen. |
| Carry-over | What is really left of a month (also zero or negative), booked as a pair that cancels out in the balance, both on the last day of the old month: an expense "Übertrag nach <Monat>" in the old plan month and an income "Übertrag aus <Monat>" in the new one; shown as its own line and not as bookings, changed or removed only through the plan. |

## Data

- Plan: `owner_id`, `year`, `month`, `quota_needs`, `quota_wants`, `quota_savings`, `carry_over_income_booking_id` and `carry_over_expense_booking_id` (the two bookings of its carry-over pair, both empty without one).
- Position: `plan_id`, `commitment_id` (optional), `label`, `amount_planned`, `due_day`, `category_id`, `pot`, `account_id`, `counter_account_id`, `pass_through`, `payment_method`, `done_at`, `changed_by_hand`, `tick_booking_id`.

## Rules

- A month is created from the contracts due in it the first time it is opened; positions are copies.
- Contract changes are offered as a change notice, never applied silently to ticked or hand-changed positions.
- Ticking is manual; ticking without bookings creates one, with bookings it offers a balancing booking.
- A position with bookings cannot be deleted.
- Pass-through positions count in no quota.
- Booked per pot is what is booked on its positions without pass-through plus the bookings of the plan month without a position, without transfers and the carry-over; the actual of a quota counts the positions only (spec 72, R5).
- A position with a target account does not become income; one stored that way before stays editable and says how to repair it when ticked (spec 66, amendment 2026-11-01).
- The month shows a carry-over as one line each, "Übertrag nach <Monat>" in the old month and "Übertrag aus <Monat>" in the new one, in no pot and no quota, also on the tab Buchungen and under "Letzte Buchungen"; without a view of the bookings there is no line. The household shows the lines with the first name only for members whose bookings the viewer may see (spec 66, amendment 2026-11-01).
- The booked sums of the plan count only real money; the suggestion for the next carry-over counts the carried rest, computed separately (spec 66, amendment 2026-11-01).
- The free amount is planned income minus planned expenses plus the carry-over from the previous month, plus booked income lines without a position and minus booked expense lines without a position (transfers and carry-over bookings do not count; spec 93), computed in `PlanFigures.Free`; each position counts there with its planned amount, or with its booked amount when more was booked, for income and expense positions, open or ticked, pass-through positions staying out (spec 93, R7); the text at the number says when a carry-over is included. Fixed, quotas, pots and booked sums stay without it, and the carry-over going out of a month does not lower that month's free amount. Whoever does not see a person's bookings gets that person's free amount without the carry-over, also as a share of the household sum, so the household sum depends on the viewer; the household month names every member without the grant (spec 66, amendment 2026-11-01). The unplanned lines of all counted members count in the household sum regardless of the grant, as the household's unplanned figure already shows them to everyone (spec 93).
- Stored pairs with the income on the first of the month stay as they are and are recognised through the two booking ids of the plan (spec 66, amendment 2026-11-01).
- If an entered real balance differs from the computed one, the carry-over dialog shows the difference per account and asks: book the missing bookings first or adopt the difference as a balancing booking (spec 66, amendment 2026-11-01).

## Interfaces

`/api/v1/plans/{year}/{month}` with positions, tick, changes and carry-over; `/api/v1/users/me/quotas`; see `docs/specs/66-monthly-plan`.

## Open questions

None.
