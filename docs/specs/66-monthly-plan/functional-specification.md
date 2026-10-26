# Functional specification: Monthly plan

Format: 2

## Summary

The tab Plan shows a month: its positions grouped by pot with planned and booked amounts, quota target and actual, a notice when contracts changed, a plus button for one-off positions and a button "Übertrag". Opening a month creates it from the contracts. Bookings get a plan month and their lines an optional position.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R11. R12 is out of scope.

## Components

- [Plan](docs/components/plan.md)
- [Commitment](docs/components/commitment.md)
- [Booking](docs/components/booking.md)

## Solution

- **Domain**: `Plan` gains owner, quota targets and creation from the extended contracts; `PlanPosition` gains id, commitment id (null for one-off), category, pot, accounts, pass-through, payment method, `DoneAt`, `ChangedByHand`, `CreatedByTick` booking id. A function that compares a month with the current contracts and returns the differences (changed fields, new contract due, contract no longer due). Booking gains `PlanYear` and `PlanMonth`; booking lines gain `PositionId`.
- **Data**: tables `plans` (unique owner, year, month) and `plan_positions`; columns `quota_needs`, `quota_wants`, `quota_savings` on users; `plan_year`, `plan_month` on bookings (existing rows: month of the date); `position_id` on booking lines (on delete restrict, see R10).
- **API** (`Duofy.Api`): endpoints below, every query filtered by the owner from the token.
- **Client and pages**: `IPlanApi`; page `/plan` with month paging (replaces the placeholder of spec 65), position dialog, tick dialog, change notice, carry-over dialog; quota settings on the page Benutzerkonto; the booking dialog gets plan month and position per line.

## Behaviour

- R1: `GET /plans/{year}/{month}` creates the month when it does not exist and returns it; positions sorted by due day, then name. A month before the person's first account opening date is created empty.
- R2: The month response carries `changes`: for each position from a contract whose contract fields differ, each contract due without a position, each position whose contract is no longer due. `POST /plans/{y}/{m}/changes/apply` takes all or a list. A position that is ticked or `ChangedByHand` is listed with a note and only changed when the person picks it explicitly.
- R3: One-off positions: name 1–100, amount > 0, day 1–last day of the month, category and pot as for contracts.
- R4: `booked` = sum of the booking lines assigned to the position (expenses and transfers out positive for expense positions, income positive for income positions).
- R5: Tick without bookings → booking dated today (or the due day if that is in the past within the month), plan month of the position, from the position's paying account (default account when empty), a transfer when it has a target account; the position stores the booking id. Tick with bookings → dialog with the difference prefilled; 0 closes without booking. Untick → removes the booking created by the tick, if any, and clears `DoneAt`.
- R6: The position picker per line lists positions of the booking's plan month that are not ticked and whose pot kind fits (income lines only income positions, expense lines the others); a transfer line may pick a savings or debt position.
- R7: Without a position the plan month is the month of the date, the previous or the next month; anything else answers `400 plan_month_invalid`. With a position the plan month is set from it.
- R8: Quotas are whole percents 0–100 that sum to 100, otherwise `400 quota_invalid`. Actual per pot = sum of the month's expense positions' booked amounts in that pot (pass-through excluded) divided by the month's income without pass-through and without carry-over.
- R9: `POST /plans/{y}/{m}/carry-over` with the amount and optional real balances per account; balancing bookings are dated the last day of the previous month. The carry-over itself is a pair of bookings on the default account that cancel out in the balance: an expense "Übertrag nach <Monat>" dated the last day of the previous month with the previous plan month, and an income "Übertrag aus <Monat>" dated the first day of the month with this plan month (pot Einnahmen; the builder names the categories as an assumption). Only one carry-over per month (`409 carry_over_exists`); removing it deletes both bookings.
- R10: Deleting a position with booking lines answers `409 position_has_bookings`.
- R11: Foreign plan or position ids answer `404 plan_not_found` or `404 position_not_found`.

## Data and interfaces

| Method and path | Body | Answers |
| --- | --- | --- |
| `GET /api/v1/plans/{year}/{month}` | – | `200 PlanResponse` (positions, sums, quotas, changes) |
| `POST /api/v1/plans/{year}/{month}/positions` | `CreatePositionRequest` | `201`; `400` |
| `PATCH /api/v1/plans/{year}/{month}/positions/{id}` | `UpdatePositionRequest` | `200`; `400`; `404` |
| `DELETE /api/v1/plans/{year}/{month}/positions/{id}` | – | `204`; `404`; `409 position_has_bookings` |
| `POST /api/v1/plans/{year}/{month}/positions/{id}/tick` | `{ amount?, date?, accountId? }` | `200`; `404` |
| `DELETE /api/v1/plans/{year}/{month}/positions/{id}/tick` | – | `200`; `404` |
| `POST /api/v1/plans/{year}/{month}/changes/apply` | `{ items?: [...] }` | `200 PlanResponse` |
| `POST /api/v1/plans/{year}/{month}/carry-over` | `{ amount, balances?: [{ accountId, realBalance }] }` | `201`; `400`; `409` |
| `DELETE /api/v1/plans/{year}/{month}/carry-over` | – | `204`; `404` |
| `GET/PUT /api/v1/users/me/quotas` | `{ needs, wants, savings }` | `200`; `400 quota_invalid` |

Bookings: requests and responses gain `planYear`, `planMonth` and per line `positionId`.

External input: yes

## Acceptance criteria

- [x] A1 (R1): Given a contract "Miete" 890.00 monthly from 2026-01-01, when GET /api/v1/plans/2026/11 is called for the first time, then the plan exists with one position "Miete" 890.00 on day 1, and a second call returns the same position id.
- [x] A2 (R1): Given the November plan exists, when the contract amount is changed to 950.00, then the November position still shows 890.00.
- [x] A3 (R2): Given the November plan with an untouched position "Miete" 890.00 and the contract changed to 950.00, when the plan is fetched, then changes lists Miete 890.00 → 950.00; when the changes are applied, then the position shows 950.00 and changes is empty.
- [x] A4 (R2): Given a ticked position and a changed contract, when all changes are applied without picking it, then the ticked position keeps its amount.
- [x] A5 (R2): Given a new monthly contract created after the November plan exists, when the plan is fetched, then changes lists it as new; when applied, then a position for it exists.
- [x] A6 (R3): Given the November plan, when a one-off position "Geschenk" 50.00 on day 20 is created, then it is listed with no commitment id, and the December plan does not contain it.
- [x] A7 (R4, R5): Given a position "Miete" 890.00 without bookings, when it is ticked, then a booking of 890.00 from the default account exists with the position on its line, the position shows booked 890.00 and is done; when the tick is removed, then that booking is gone and the position is open.
- [x] A8 (R5): Given a position "Lebensmittel" 600.00 with bookings of 250.00 and 300.00, when it is ticked with amount 0, then no new booking is created, it is done and shows booked 550.00; when ticked with 50.00 instead, then a booking of 50.00 is created.
- [x] A9 (R5): Given a savings goal position with target account Sparkonto, when it is ticked, then the created booking is a transfer to the Sparkonto.
- [x] A10 (R6): Given a booking with two lines, when line 1 is assigned to position Lebensmittel and line 2 to position Drogerie of the same plan month, then each position shows the amount of its line as booked.
- [x] A11 (R7): Given a salary booking dated 2026-09-25 with plan month 2026-10 and no position, when it is saved, then it counts in October's plan and the account balance on 2026-09-30 contains it; plan month 2026-12 answers 400 plan_month_invalid.
- [x] A12 (R8): Given a person without settings, when GET /users/me/quotas is called, then the answer is 50/30/20; when PUT 60/20/20, then a month created afterwards carries 60/20/20 and an earlier month keeps 50/30/20; PUT 60/30/20 answers 400 quota_invalid.
- [x] A13 (R8): Given October income 3000.00, Bedürfnisse booked 1500.00 and a pass-through position of 200.00 in Bedürfnisse, when the plan is fetched, then actual Bedürfnisse is 50 percent.
- [x] A14 (R9): Given October with income 3000.00 and expenses 2770.00, when the carry-over for November is opened, then the suggested amount is 230.00; when it is saved with 230.00 and the real balance of Giro 10.00 lower than computed, then November has an income booking "Übertrag aus Oktober" of 230.00 on 2026-11-01, October has an expense "Übertrag nach November" of 230.00 on 2026-10-31, the default account balance is unchanged by the pair, and Giro has a balancing booking of 10.00 on 2026-10-31; a second carry-over for November answers 409.
- [x] A15 (R10): Given a position with one booking line, when it is deleted, then the answer is 409 position_has_bookings; without bookings the answer is 204 and the contract still exists.
- [x] A16 (R11): Given two people, when person B requests the plan, a position or the quotas of person A by id, then the answer is 404 and nothing of A changes.
- [x] A17 (R1, R4): Given the app tab Plan, when it is opened, then it shows the current month with positions grouped by pot, planned and booked per position, and month paging; the placeholder of spec 65 is gone.
- [x] A18 (R9): Given a November carry-over pair, when PUT or DELETE is sent for either of its two bookings on /bookings, then the answer is 409 carry_over_booking and both bookings are unchanged; removing the carry-over in the plan still deletes both.
- [x] A19 (R5, R6): Given a ticked position "Umschlag Lebensmittel" in pot Bedürfnisse with target account Bargeld (a transfer of 400.00), when the booking is changed to 380.00, then it is saved with its position, and the booking dialog offers that position for the transfer line.
- [x] A20 (R9): Given October income 2000.00 and expenses 2150.00, when the carry-over for November is saved with -150.00, then the pair is booked with -150.00 carried (expense "Übertrag nach November" and income "Übertrag aus Oktober" swapped in direction so the balance is unchanged); with 0.00 no pair is booked but the balancing bookings for real balances are.
- [x] A21 (R9): Given a carry-over request naming the same account twice in the real balances, when it is sent, then the answer is 400 and nothing is booked.
- [x] A22 (R5, R9): Given a position, when two tick requests arrive at the same time, then exactly one booking exists and the position links to it; the same for two carry-over requests of one month.
- [x] A23 (R10): Given a position with booking lines, when its pot is changed to another kind (an expense pot to Einnahmen or back), then the answer is 409 position_has_bookings; changing between expense pots still works.
- [x] A24 (R2): Given a monthly contract since 2020 and the first account opened in October 2026, when September 2026 is fetched, then changes lists no new contract.
- [x] A25 (R8): Given an actual quota of exactly 12.25 percent, when the plan is fetched, then it shows 12.3.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1, A2, A17 | `Tests/Duofy.Backend.Tests/PlanTests.cs`: `Opening_a_month_creates_it_once_from_the_due_contracts`, `A_month_before_the_first_account_opening_is_created_empty`, `A_changed_contract_does_not_rewrite_the_month_but_is_offered_as_a_change`; `Tests/Duofy.Backend.Tests/Domain/PlanPositionCopyTests.cs`: `Position_copies_all_planning_fields_of_the_commitment`; `Tests/Duofy.Shared.Tests/PlanPageTests.cs`: `It_opens_the_current_month_with_positions_grouped_by_pot_planned_and_booked`, `Paging_shows_the_next_and_the_previous_month` |
| R2 | A3, A4, A5, A24 | `Tests/Duofy.Backend.Tests/PlanTests.cs`: `A_changed_contract_does_not_rewrite_the_month_but_is_offered_as_a_change`, `Applying_all_changes_leaves_a_ticked_position_alone_until_it_is_picked`, `A_contract_created_after_the_month_is_offered_as_new`, `An_ended_contract_is_offered_for_removal`, `Changing_a_contract_position_by_hand_marks_it`, `A_month_before_the_first_account_opening_is_created_empty`; `Tests/Duofy.Backend.Tests/Domain/PlanChangesTests.cs`: `A_month_before_the_first_account_opening_offers_no_new_contracts`; `Tests/Duofy.Shared.Tests/PlanPageTests.cs`: `Changed_contracts_are_shown_with_before_and_after_and_can_be_applied_all_or_one` |
| R3 | A6 | `Tests/Duofy.Backend.Tests/PlanTests.cs`: `A_one_off_position_belongs_to_its_month_only`, `A_one_off_position_follows_the_rules_for_name_day_pot_and_category`; `Tests/Duofy.Shared.Tests/PlanPageTests.cs`: `The_add_button_creates_a_one_off_position`; `Tests/Duofy.Shared.Tests/AppLayoutTests.cs`: `Plan_shows_the_month_and_its_plus_button_adds_a_position` |
| R4 | A7, A17 | `Tests/Duofy.Backend.Tests/PlanTests.cs`: `Ticking_without_bookings_books_the_position_and_unticking_removes_the_booking`; `Tests/Duofy.Backend.Tests/Domain/PlanFiguresTests.cs`: `Booked_is_the_sum_of_the_lines_assigned_to_a_position`; `Tests/Duofy.Shared.Tests/PlanPageTests.cs`: `It_opens_the_current_month_with_positions_grouped_by_pot_planned_and_booked` |
| R5 | A7, A8, A9, A19, A22 | `Tests/Duofy.Backend.Tests/PlanTests.cs`: `Ticking_without_bookings_books_the_position_and_unticking_removes_the_booking`, `Ticking_a_past_position_books_it_on_its_due_day`, `Ticking_a_position_with_bookings_books_only_the_balance_that_was_asked_for`, `Ticking_a_savings_goal_with_a_target_account_books_a_transfer`, `Ticking_a_contract_with_a_target_account_fills_its_position_whatever_the_pot`, `The_transfer_of_a_ticked_position_with_a_target_account_can_be_changed_with_its_position`, `Two_ticks_at_the_same_time_book_the_position_once`; `Tests/Duofy.Shared.Tests/PlanPageTests.cs`: `Ticking_a_position_without_bookings_books_it_at_once`, `Ticking_a_position_with_bookings_offers_the_open_rest_as_balance`, `Removing_the_tick_unticks_the_position` |
| R6 | A10, A19 | `Tests/Duofy.Backend.Tests/PlanTests.cs`: `Each_line_of_a_split_booking_fills_its_own_position`, `A_line_may_only_fill_a_position_of_the_same_kind_and_plan_month`, `The_transfer_of_a_ticked_position_with_a_target_account_can_be_changed_with_its_position`; `Tests/Duofy.Shared.Tests/BookingDialogPlanTests.cs`: `A_line_offers_the_open_positions_of_the_plan_month_and_sends_the_chosen_one`; `Tests/Duofy.Shared.Tests/BookingDraftPlanTests.cs`: `The_picker_offers_open_positions_of_the_fitting_kind`, `A_transfer_line_may_fill_any_position_with_its_own_target_account` |
| R7 | A11 | `Tests/Duofy.Backend.Tests/PlanTests.cs`: `A_booking_counts_in_its_plan_month_while_the_balance_follows_its_date`, `Without_a_plan_month_a_booking_counts_in_the_month_of_its_date`; `Tests/Duofy.Backend.Tests/Domain/PlanFiguresTests.cs`: `A_booking_may_count_in_its_month_or_the_one_before_or_after`; `Tests/Duofy.Shared.Tests/BookingDialogPlanTests.cs`: `Choosing_the_next_month_loads_its_positions_and_sends_it_as_plan_month` |
| R8 | A12, A13, A25 | `Tests/Duofy.Backend.Tests/PlanTests.cs`: `Quotas_start_at_50_30_20_and_a_new_month_copies_the_settings`, `Quota_actual_leaves_out_pass_through_positions`; `Tests/Duofy.Backend.Tests/Domain/PlanFiguresTests.cs`: `Quota_actual_ignores_pass_through_positions_and_the_carry_over`, `Quota_actual_rounds_half_away_from_zero`; `Tests/Duofy.Shared.Tests/QuotaSettingsTests.cs`: `Quotas_are_prefilled_and_can_be_saved` |
| R9 | A14, A18, A20, A21, A22 | `Tests/Duofy.Backend.Tests/PlanTests.cs`: `The_carry_over_books_a_pair_that_cancels_out_and_balances_each_account_once`, `Removing_the_carry_over_deletes_both_bookings_of_the_pair`, `The_two_bookings_of_a_carry_over_change_only_through_the_plan`, `A_negative_rest_is_carried_with_the_pair_swapped_and_a_rest_of_zero_only_balances_the_accounts`, `A_carry_over_naming_an_account_twice_is_refused_and_books_nothing`, `Two_carry_overs_of_one_month_at_the_same_time_book_one_pair`; `Tests/Duofy.Shared.Tests/PlanPageTests.cs`: `The_carry_over_dialog_suggests_the_rest_and_sends_real_balances`, `The_carry_over_dialog_takes_a_negative_rest` |
| R10 | A15, A23 | `Tests/Duofy.Backend.Tests/PlanTests.cs`: `A_position_with_bookings_cannot_be_deleted_and_deleting_one_keeps_the_contract`, `The_pot_kind_of_a_position_with_bookings_cannot_change`; `Tests/Duofy.Shared.Tests/PlanPageTests.cs`: `Only_positions_without_bookings_offer_delete` |
| R11 | A16 | `Tests/Duofy.Backend.Tests/PlanTests.cs`: `Another_persons_positions_look_unknown_and_stay_unchanged` |

## Threats

- Can someone pretend to be someone else? Only with a stolen token; the owner always comes from the token.
- Can someone change data? Only their own plans; positions, accounts, categories and bookings of others answer 404 or 400.
- Can someone deny an action? Bookings created by a tick or a carry-over are marked as such and carry timestamps.
- Can someone read what they should not? No: every query filters by owner; foreign ids answer 404.
- Can someone bring it to a halt? Months are limited to 2000–2100; a month is created once per person and month (unique index).
- Can someone gain rights they should not have? No roles involved; owner fields cannot be set by the request.

## Non-goals

- Savings goal progress, rate recommendations, the reached-goal question, pausing a rate, the account protection for earmarked savings.
- Household plan, private positions, change history of positions, the web page.
- Automatic carry-over, automatic ticking, limits that fill without ticking.

## Risks

- The largest feature so far; if it does not fit one day, the builder reports the criteria done and the rest as not done.
- Existing bookings get a plan month from their date; the human's local database is migrated only after the yes.

## Open questions

None.

## Approval

Approved by the human on 2026-10-24

Rules decided one at a time on 2026-10-21, 2026-10-23 and 2026-10-24. The lead's additions (tick date, position picker limited to open matching positions, whole-percent quotas summing to 100, pass-through and carry-over excluded from quota income, one removable carry-over per month with balancing bookings on the last day of the previous month, existing bookings get the month of their date, savings goal logic moved to its own specification) were presented separately and accepted, together with the migrations.

Amendment 2026-10-24 (card P-012): the carry-over is a pair of bookings that cancels out in the balance, decided by the human after kai found that a single income booking would count the money twice.

Amendment 2026-10-25 (final review round 2, decided by the human): the two bookings of a carry-over pair are changed or removed only through the plan (M1); a transfer line may fill any position that has its own target account (M2); a negative rest is carried like a positive one, a rest of 0 only balances the accounts (m3); duplicate accounts in the real balances are refused (m2), parallel ticks and carry-overs book once (m8), the pot kind of a position with bookings cannot change (m4), months before the first account opening offer no new contracts (m1), quota percentages round half away from zero (m6). Balancing bookings keep counting in the quotas (m5, the human chose "as before").
