# Functional specification: Every booking belongs to a pot

Format: 2

## Summary

Bookings without a position now count as booked in their pot, so the gap chart of a person's month and of the household month shows unplanned spending. The API keeps refusing an expense or income line without a pot, and the booking dialog starts an expense with no pot chosen and marks the field as required. A new transfer carries a required source pot and a required target pot; they are only remembered and change no figure.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R5 and R9. R6 and R7 are out of scope, and R8 is not built (wont, decision of the human on 2026-10-31).

## Components

- [Booking](docs/components/booking.md)
- [Plan](docs/components/plan.md)

## Solution

- **Domain**: the rule for unplanned bookings moves from `HouseholdPlanFigures` to `PlanFigures` as one function; `PlanFigures.PotTotals` adds its result to what is booked on positions. The household plan and the plan of a person use the same function.
- **API**: the plan of a person passes the bookings of its plan month and the ids of the carry-over bookings around that month. `BookingValidator` already refuses a line without a pot; it stays as it is.
- **Dialog**: `BookingDraft` starts an expense line with an empty pot; a chosen category or position still fills it. An empty pot goes to the API as no pot, and the API answers `pot_required`. The field carries `aria-required` and the label "Topf (Pflicht)".
- **Transfer pots (R9)**: the pot of the one line of a transfer is the source pot; the new nullable column `target_pot` of `bookings` holds the target pot (the target is a fact of the whole transfer, not of a line). `BookingValidator.CheckTransfer` answers the new code `transfer_pots_required` when one of the two is missing. No calculation reads the pots of a transfer: `PlanFigures` leaves transfers out of the pot sums and the quotas, and a transfer that fills a position counts through the position as before. The tick of a position with a target account books a transfer whose source and target pot are both the pot of the position (assumption). The dialog replaces the box "Das Geld wird gespart" with the two required fields "Von Topf" and "Zu Topf"; the list shows "Topf: Sparen → Wünsche" as text.

## Behaviour

- R1: An expense or income line without a pot is answered with `400 pot_required`. A transfer has its own rule, see R9.
- R2: An expense with a pot and without a position is saved as before.
- R3: `booked` of a pot is the sum of the positions of that pot without pass-through, plus the lines of that pot without a position, without transfers and without the bookings of the carry-over into and out of the month. Lines on a position count through the position as before.
- R3: The planned sum, the income and what is still to go out do not change.
- R5: The actual of a quota counts the positions of that pot without pass-through only, as in specs 66 and 69; lines without a position do not count in it. The change of commit 3b09a02 is taken back.
- R8: Not built (wont). Date and plan month stay separate as in spec 66 R7 (previous and next month allowed); the field "Plan-Monat" stays in the dialog; no error code `date_outside_plan_month` exists. The human decided this on 2026-10-31 and took back the wish of 2026-10-30.
- R9: A new transfer (POST) and a changed one (PUT) need exactly one line over the whole amount without a category, whose pot is the source pot, and the field `targetPot`; each of the two is one of `needs`, `wants`, `savings`, and they may be the same. A missing pot is answered with `400 transfer_pots_required`, the pot `income` or an unknown pot with `400 transfer_pot_invalid`. The answer and the list of the month return both pots. The pots are only remembered: pot totals, quotas, the household plan, the overviews and the balances of the accounts are the same with and without them. An older transfer without pots (no line, or one line in Sparen and no target) stays readable and is listed; when it is changed, the dialog asks for the missing pots. A transfer goes between own accounts only; there is no transfer from person to person.
- R4: A new expense shows "(bitte wählen)" in the pot field until a category, a position or the person chooses a pot. An income keeps its only pot. Editing a stored booking shows its stored pot.

## Data and interfaces

No new endpoint. One new nullable column `bookings.target_pot` (R9) with an additive migration `AddTransferTargetPot` (`ALTER TABLE bookings ADD target_pot character varying(20)`); stored bookings stay unchanged. `SaveBookingRequest` gets `targetPot` and `BookingResponse` returns `targetPot`, both null except for a transfer. `overview.pots[].booked` of `GET /api/v1/plans/{year}/{month}` and `pots[].booked` of `GET /api/v1/households/{id}/plans/{year}/{month}` now include the unplanned lines. `quotas[].actual` of both, and the member quotas of the household plan, stay as they were before commit 3b09a02: positions only. The new error code `transfer_pots_required` is added for R9.

External input: yes

## Acceptance criteria

- [x] A1 (R1): Given an expense line without a pot, when it is sent to the API, then the answer is `pot_required`.
- [x] A2 (R2, R3): Given a month with Wünsche planned 0.00, when an expense of 40.00 in Wünsche without a position is booked, then it is saved and the overview of the month shows Wünsche planned 0.00 and booked 40.00.
- [x] A3 (R3): Given a position in Grundbedarf booked 100.00 and an unplanned expense of 40.00 in Grundbedarf, when the pot totals are computed, then Grundbedarf has booked 140.00.
- [x] A4 (R3): Given a transfer without a position and the carry-over pair, when the overview of the month is read, then neither counts as booked in any pot.
- [x] A5 (R3): Given Ben with an expense of 40.00 in Wünsche without a position, when the household figures are computed, then Wünsche has booked 40.00 and unplanned Wünsche stays 40.00.
- [x] A6 (R4): Given a new booking in the dialog, when it opens, then the pot field shows "(bitte wählen)", has `aria-required="true"` and the label "Topf (Pflicht)"; when a category is chosen, then the pot field shows the pot of that category.
- [x] A7 (R1, R4): Given a new expense in the dialog without a chosen pot, when it is turned into a request, then the line carries no pot.
- [x] A8 (R5): Given income of 1000.00, a position of 50.00 in Wünsche booked and an expense of 100.00 in Wünsche without a position, when the plan or the household plan is read, then the actual of the quota Wünsche is 5 percent.
- [x] A9 (R9): Given a transfer from Sparen to Wünsche with both pots, when it is saved, then it is stored and the answer and the list of the month return the source pot `savings` and the target pot `wants`.
- [x] A10 (R9): Given a transfer without a target pot, or without a line or a source pot, when it is sent, then the answer is `transfer_pots_required` and nothing is stored; the pot `income` or an unknown pot answers `transfer_pot_invalid`; the same pot on both sides and any of the three pots are accepted.
- [x] A11 (R9): Given Wünsche planned 300 and Sparen planned 200 and a transfer of 100 from Sparen to Wünsche, when the plan is read before and after the transfer, then the pot totals, the quotas and the booked amounts of the positions are the same.
- [x] A12 (R9): Given a transfer stored before this rule without a target pot or without a line, when the bookings are read, then it is listed and readable, the dialog opens it with empty fields for the missing pots, and the migration keeps such a row unchanged with an empty target pot.
- [x] A13 (R9): Given a new transfer in the dialog, when it opens, then "Von Topf (Pflicht)" and "Zu Topf (Pflicht)" show "(bitte wählen)" and carry `aria-required="true"`; when both are chosen and it is saved, then the request carries the source pot as the pot of its line and the target pot as `targetPot`; the booking list shows "Topf: Sparen → Wünsche" as text.
- [x] A14 (R9): Given a position with a target account, when it is ticked, then the transfer carries the pot of the position on both sides.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1, A7 | `Tests/Duofy.Backend.Tests/BookingRulesTests.cs`: `Expense_line_without_a_pot_answers_pot_required` (existed before this work); `Tests/Duofy.Shared.Tests/BookingDraftTests.cs`: `A_new_expense_has_no_pot_until_one_is_chosen_and_sends_none` |
| R2 | A2 | `Tests/Duofy.Backend.Tests/PlanTests.cs`: `A_booking_without_a_position_counts_as_booked_in_its_pot` |
| R3 | A2, A3, A4, A5 | `Tests/Duofy.Backend.Tests/PlanTests.cs`: `A_booking_without_a_position_counts_as_booked_in_its_pot`, `A_pot_adds_what_is_booked_on_its_positions_and_what_is_unplanned`, `Transfers_and_the_carry_over_count_as_booked_in_no_pot`; `Tests/Duofy.Backend.Tests/Domain/PlanOverviewFiguresTests.cs`: `Pot_totals_add_what_is_booked_on_positions_and_what_is_unplanned`; `Tests/Duofy.Backend.Tests/Domain/HouseholdPlanFiguresTests.cs`: `A_booking_without_position_counts_as_booked_in_its_pot_and_stays_in_the_unplanned_breakdown`, `Transfers_and_the_carry_over_count_as_booked_in_no_pot` |
| R5 | A8 | `Tests/Duofy.Backend.Tests/Domain/PlanFiguresTests.cs`: `Quota_actual_keeps_a_booking_without_position_out`; `Tests/Duofy.Backend.Tests/PlanTests.cs`: `A_booking_without_a_position_does_not_count_in_the_actual_of_its_quota` (the household plan uses the same `PlanFigures.QuotaActual`). Commit 3b09a02 was reverted. |
| R9 | A9, A10 | `Tests/Duofy.Backend.Tests/BookingTests.cs`: `A_transfer_from_savings_to_wants_is_stored_and_returned_with_both_pots`, `A_transfer_without_a_target_pot_is_refused_and_not_stored`, `Changing_a_transfer_changes_its_pots`; `Tests/Duofy.Backend.Tests/BookingRulesTests.cs`: `A_transfer_with_any_source_and_target_pot_is_valid`, `A_transfer_without_its_source_or_target_pot_answers_transfer_pots_required`, `Transfer_with_the_income_pot_or_an_unknown_pot_answers_transfer_pot_invalid` |
| R9 | A11 | `Tests/Duofy.Backend.Tests/PlanTests.cs`: `A_transfer_between_pots_changes_no_figure_of_the_plan`, `Transfers_and_the_carry_over_count_as_booked_in_no_pot` |
| R9 | A12 | `Tests/Duofy.Backend.Tests/TransferTargetPotMigrationTests.cs`: `The_migration_keeps_an_older_transfer_unchanged_and_leaves_its_target_pot_empty`; `Tests/Duofy.Shared.Tests/BookingDraftTests.cs`: `An_older_transfer_without_pots_opens_with_empty_fields_for_what_is_missing`; `Tests/Duofy.Shared.Tests/BookingsPageTests.cs`: `An_older_transfer_without_pots_is_still_listed` |
| R9 | A13 | `Tests/Duofy.Shared.Tests/BookingsPageTests.cs`: `A_transfer_asks_for_two_accounts_and_hides_the_lines`, `A_transfer_shows_its_source_and_target_pot_as_text`; `Tests/Duofy.Shared.Tests/BookingDraftTests.cs`: `A_new_transfer_has_no_pots_until_both_are_chosen_and_sends_them_as_line_and_target`, `A_saved_transfer_becomes_a_draft_with_both_pots` |
| R9 | A14 | `Tests/Duofy.Backend.Tests/PlanTests.cs`: `Ticking_a_savings_goal_with_a_target_account_books_a_transfer`, `Ticking_a_contract_with_a_target_account_fills_its_position_whatever_the_pot` |
| R4 | A6, A7 | `Tests/Duofy.Shared.Tests/BookingDialogPlanTests.cs`: `The_pot_is_marked_as_required_starts_empty_and_follows_the_category`, `An_income_offers_only_its_pot_and_nothing_to_choose`; `Tests/Duofy.Shared.Tests/BookingDraftTests.cs`: `A_new_expense_has_no_pot_until_one_is_chosen_and_sends_none`, `A_split_line_of_an_expense_has_no_pot_either` |

## Threats

- Can someone pretend to be someone else? Only with a stolen access token; the owner checks of spec 68 stay unchanged.
- Can someone change data? Only their own or with the grant of spec 68; there is no new write path.
- Can someone deny an action? Saving a booking keeps its existing time stamps and logs.
- Can someone read what they should not? A viewer without the bookings grant gets no bookings, so the sum stays 0 for them; the household figures already show unplanned sums per pot to every member (spec 69).
- Can someone bring it to a halt? No new query; the sum runs over the bookings of the month that are already loaded.
- Can someone gain rights they should not have? No: no access check changes.

## Non-goals

- A check or any change to stored bookings: the human said on 2026-10-30 that bookings without a pot do not exist (card P-059). The one migration of this spec only adds the empty column `target_pot`.
- A year view comparing plan and reality month by month; the human names it as a later topic (card P-060).
- R8, the booking only in its plan month and the error code `date_outside_plan_month`: the human decided on 2026-10-31 that date and plan month stay separate.
- A transfer from person to person: it does not exist and is not built (Q2 answered on 2026-10-31).
- Any figure that reads the pots of a transfer: they are only remembered. Whether the line of a transfer may fill only certain positions (spec 59, 66) is not changed here.
- New texts for the household plan, or the web page.

## Risks

Q1 and Q2 of the customer requirements were answered by the human on 2026-10-31 (see "Answered questions" there): the pots of a transfer are only remembered, and a transfer from person to person does not exist.

The booked sums per pot on the household's Plan tab now contain the amounts of the line "Ohne Posten gebucht" below them; that line stays as a breakdown of the sums.

## Open questions

None.

## Approval

Approved by the human (Tom) on 2026-10-31, in the session of his finance day; R8 dropped and R9 decided the same day.

Amended on 2026-11-01 by the human: a balancing booking of the carry-over that lowers a balance is an expense in Wünsche, no longer in Grundbedarf; one that raises a balance stays an income, and the carried rest still arrives as an income in the next month. By R3 the balancing expense counts as booked in Wünsche, by R5 not in the actual of the quota. Balancing bookings already stored stay as they are; no data migration. Proof: `PlanTests.A_balancing_expense_counts_in_wants_a_balancing_income_stays_income_and_the_balances_match_what_was_entered`.
