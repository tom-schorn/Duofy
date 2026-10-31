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
| Carry-over | What is really left of a month (also zero or negative), booked as a pair that cancels out in the balance: an expense "Übertrag nach <Monat>" in the old plan month and an income "Übertrag aus <Monat>" in the new one; changed or removed only through the plan. |

## Data

- Plan: `owner_id`, `year`, `month`, `quota_needs`, `quota_wants`, `quota_savings`, `carry_over_income_booking_id` and `carry_over_expense_booking_id` (the two bookings of its carry-over pair, both empty without one).
- Position: `plan_id`, `commitment_id` (optional), `label`, `amount_planned`, `due_day`, `category_id`, `pot`, `account_id`, `counter_account_id`, `pass_through`, `payment_method`, `done_at`, `changed_by_hand`, `tick_booking_id`.

## Rules

- A month is created from the contracts due in it the first time it is opened; positions are copies.
- Contract changes are offered as a change notice, never applied silently to ticked or hand-changed positions.
- Ticking is manual; ticking without bookings creates one, with bookings it offers a balancing booking.
- A position with bookings cannot be deleted.
- Pass-through positions count in no quota.
- Booked per pot is what is booked on its positions without pass-through plus the bookings of the plan month without a position, without transfers and the carry-over; the actual of a quota counts the same (spec 72).

## Interfaces

`/api/v1/plans/{year}/{month}` with positions, tick, changes and carry-over; `/api/v1/users/me/quotas`; see `docs/specs/66-monthly-plan`.

## Open questions

None.
