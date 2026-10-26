# Functional specification: Household plan

Format: 2

## Summary

`GET /households/{id}/plans/{year}/{month}` composes the household plan from the counted members' months, creating missing months first, with owner names, figures per pot, unplanned bookings and the household's quotas; `GET /households/{id}/plans` lists the months. Account balances appear only for members who granted accounts. The app's switch gains "Haushalt", which turns the plan page into the read-only household plan.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R9. R10 is out of scope.

## Components

- [Plan](docs/components/plan.md)
- [Household](docs/components/household.md)
- [Grant](docs/components/grant.md)

## Solution

- **Domain**: one function that takes the counted members' months (positions with owner) and the household quotas and returns the figures, reusing the figure rules of #66 (pass-through and carry-over excluded from income and quotas, a limit or a ticked position is never "still to go out", never negative).
- **API** (`Duofy.Api`): counted members = members whose joined date is on or before the last day of the month. For each counted member without that month, the month is created exactly as `GET /plans/{y}/{m}` of #66 would create it. Positions carry `ownerId` and `ownerFirstName`. `unplanned` sums the counted members' bookings of the plan month without a position, by pot, without transfers and without the carry-over pair. `accounts` lists the default and other active accounts with balances of counted members whose `accounts` grant to the viewer is at least `view` (always the viewer's own), and `accountsMissing` the first names of the others. Membership is checked first (`404 household_not_found` for others).
- **Client and pages**: `IHouseholdApi` gains both calls; the active-member state of #68 gains the value "Haushalt"; the plan page shows, in that state, the household name, month paging, the household quotas, positions grouped by pot with owner name, planned, booked and tick state, without add, tick, edit, delete or carry-over controls, plus the accounts with the notice for missing names.

## Behaviour

- R1, R5: A member who left is no member any more and counts in no month; a member who joined on 2026-11-10 counts from November 2026 on.
- R2: Creating a member's missing month uses that member's contracts and quotas, as #66 R1; it happens once (unique month per person).
- R3: Positions are sorted by pot, then due day, then name; every position shows owner first name, planned, booked and `doneAt`.
- R4: `income` = planned income positions without pass-through; per pot `planned` and `booked`; `unpaid` as in the original's `_summarize`; quota actual per pot = booked expense positions of that pot without pass-through divided by income without pass-through and without carry-over; targets from the household.
- R6: The household plan offers no write path; writes on another member's data go through the endpoints of #66 with `owner` and the checks of #68.
- R7: `GET /households/{id}/plans` answers year and month of every month in which at least one counted member has a plan, newest first.
- R8: The notice reads "Ohne Freigabe der Konten fehlen hier: <Namen>."
- R9: The switch shows "Haushalt" only while the household has at least two members; in that state pages other than the plan show "Für den Haushalt gibt es hier keine gemeinsame Sicht."

## Data and interfaces

| Method and path | Body | Answers |
| --- | --- | --- |
| `GET /api/v1/households/{id}/plans/{year}/{month}` | – | `200 HouseholdPlanResponse` (householdName, quotas, positions with owner, figures, unplanned, accounts, accountsMissing); `400` for months outside 2000–2100; `404 household_not_found` |
| `GET /api/v1/households/{id}/plans` | – | `200 [ { year, month } ]`; `404` |

External input: yes

## Acceptance criteria

- [x] A1 (R1, R3): Given Anna with a position "Miete" 890.00 and Ben with "Strom" 80.00 in November 2026, both in one household, when Ben opens the household plan of November, then it lists Miete 890.00 by Anna and Strom 80.00 by Ben with planned, booked and tick state, although Anna granted Ben nothing.
- [x] A2 (R2): Given Anna has opened November and Ben has not, when Anna opens the household plan of November, then Ben's November exists afterwards with his due contracts, and opening it again creates nothing new.
- [x] A3 (R4): Given November with income 3000.00 by Anna and 2000.00 by Ben, Bedürfnisse booked 2500.00, a pass-through position of 200.00 and household quotas 50/30/20, when the household plan is fetched, then income is 5000.00 and actual Bedürfnisse is 50 percent against a target of 50.
- [x] A4 (R4): Given Ben has a booking of 40.00 in pot Wünsche without a position in plan month November, when the household plan is fetched, then unplanned Wünsche is 40.00, and a transfer without position and the carry-over pair do not count.
- [x] A5 (R5): Given Ben left the household in December, when Anna opens the household plan of November, then no position of Ben appears; given Clara joined on 2026-11-10, then she counts in November and not in October.
- [x] A6 (R6): Given the household plan, when any POST, PATCH, PUT or DELETE is sent to a household plan path, then the answer is 405 or 404 and nothing changes.
- [x] A7 (R7): Given plans of Anna for October and November and of Ben for November, when the household months are listed, then the answer is November, October.
- [x] A8 (R8): Given Anna granted Ben accounts view and Clara granted him nothing, when Ben opens the household plan, then the accounts list Ben's and Anna's accounts and accountsMissing is ["Clara"].
- [x] A9 (R1): Given Clara of another household, when she requests the household plan or the months of Anna's household, then the answer is 404 household_not_found.
- [x] A10 (R9, R6): Given Ben in a household of two, when he chooses Haushalt in the switch, then the plan page shows the household name, positions with owner names and no add, tick or carry-over button; given a household of one, the switch offers no Haushalt.
- [x] A11 (R2): Given Anna and Clara of another household each with a contract and Ben without his November, when Anna opens the household plan of November, then Ben's created November holds only Ben's due contracts.
- [x] A12 (R9): Given Ben in the app, when he joins Anna's household through the invitation page, then the switch offers Haushalt without restarting; after he leaves, it no longer does and no household view stays chosen.
- [x] A13 (R4): Given Anna carried a negative rest from October into November, when the household plan of October is fetched, then the income "Übertrag nach November" counts neither in the household income nor in the unplanned income.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1, A9 | `Tests/Duofy.Backend.Tests/HouseholdPlanTests.cs`: `Every_member_sees_every_position_with_its_owner_although_nothing_is_granted`, `Someone_outside_the_household_gets_household_not_found`, `A_month_outside_the_range_is_refused_and_a_stranger_cannot_probe_for_it`, `The_household_plan_needs_a_signed_in_person`; `Tests/Duofy.ApiClient.Tests/HouseholdPlanClientTests.cs`: `The_plan_of_a_month_is_read_from_the_household_path`, `A_stranger_gets_not_found` |
| R2 | A2, A11 | `Tests/Duofy.Backend.Tests/HouseholdPlanTests.cs`: `Opening_creates_the_missing_months_of_the_members_once`; `Tests/Duofy.Backend.Tests/OwnerBypassListTests.cs`: `Every_bypass_in_the_product_code_is_on_the_approved_list` |
| R3 | A1 | `Tests/Duofy.Backend.Tests/HouseholdPlanTests.cs`: `Every_member_sees_every_position_with_its_owner_although_nothing_is_granted`, `Positions_are_sorted_by_pot_then_due_day_then_name`; `Tests/Duofy.Shared.Tests/HouseholdPlanPageTests.cs`: `The_plan_page_shows_the_household_plan_with_owner_names_and_no_buttons_to_change_anything` |
| R4 | A3, A4, A13 | `Tests/Duofy.Backend.Tests/Domain/HouseholdPlanFiguresTests.cs`: `Income_and_quota_actual_add_up_over_the_members_and_leave_pass_through_out`, `The_carry_over_counts_as_no_income`, `The_income_of_a_negative_carry_over_into_the_next_month_counts_nowhere`, `Without_income_every_actual_is_zero`, `Unpaid_is_what_is_left_of_open_positions_and_never_negative`, `Unplanned_sums_bookings_without_position_by_pot_and_skips_transfers_and_the_carry_over`, `Planned_sums_leave_out_pass_through_positions`; `Tests/Duofy.Backend.Tests/HouseholdPlanTests.cs`: `Income_and_quotas_count_over_all_members_without_pass_through`, `Bookings_without_a_position_are_unplanned_by_pot_but_transfers_and_the_carry_over_are_not`, `A_negative_carry_over_into_the_next_month_counts_nowhere_in_the_household_plan`; `Tests/Duofy.Shared.Tests/HouseholdPlanPageTests.cs`: `The_sums_and_quotas_of_the_household_are_shown` |
| R5 | A5 | `Tests/Duofy.Backend.Tests/HouseholdPlanTests.cs`: `Whoever_left_disappears_from_the_plan_and_whoever_joined_counts_from_their_month_on`, `Joining_after_midnight_in_berlin_counts_from_that_day_on_although_utc_is_still_the_day_before` |
| R6 | A6, A10 | `Tests/Duofy.Backend.Tests/HouseholdPlanTests.cs`: `The_household_plan_has_no_write_path`; `Tests/Duofy.Shared.Tests/HouseholdPlanPageTests.cs`: `The_plan_page_shows_the_household_plan_with_owner_names_and_no_buttons_to_change_anything`, `Other_pages_say_there_is_no_joint_view` |
| R7 | A7 | `Tests/Duofy.Backend.Tests/HouseholdPlanTests.cs`: `The_months_are_listed_newest_first_for_the_months_with_a_plan_of_a_counted_member`; `Tests/Duofy.ApiClient.Tests/HouseholdPlanClientTests.cs`: `The_months_are_read_from_the_plans_path` |
| R8 | A8 | `Tests/Duofy.Backend.Tests/HouseholdPlanTests.cs`: `Accounts_show_only_for_members_who_granted_them_and_the_others_are_named`; `Tests/Duofy.Shared.Tests/HouseholdPlanPageTests.cs`: `Accounts_are_shown_for_those_who_granted_them_and_the_others_are_named`, `Without_missing_accounts_there_is_no_notice` |
| R9 | A10, A12 | `Tests/Duofy.Shared.Tests/HouseholdPlanPageTests.cs`: `In_a_household_of_two_the_switch_offers_the_household_even_without_any_grant`, `In_a_household_of_one_there_is_no_switch`, `Granted_members_and_the_household_are_both_offered_and_choosing_the_household_clears_the_owner`, `Choosing_a_person_or_me_leaves_the_household_view`, `The_household_plan_pages_between_the_months`, `Leaving_the_household_of_two_drops_the_household_view`; `Tests/Duofy.Shared.Tests/InvitationPageTests.cs`: `Joining_refreshes_the_switch`; `Tests/Duofy.Shared.Tests/HouseholdPageTests.cs`: `Founding_refreshes_the_switch`, `Leaving_refreshes_the_switch_and_drops_the_household_view`, `Dissolving_refreshes_the_switch`, `Removing_refreshes_the_switch_and_drops_the_household_view` |

## Threats

- Can someone pretend to be someone else? Only with a stolen access token; membership comes from the token.
- Can someone change data? No: the household plan has no write path; creating a missing month changes no amounts and follows the member's own contracts.
- Can someone deny an action? Nothing is written except missing months, which carry their creation time.
- Can someone read what they should not? Members of the household see every position of the others by the human's decision (private positions follow in #41); account balances only with the accounts grant; non-members get 404.
- Can someone bring it to a halt? Months limited to 2000–2100; a household has few members; creating months is idempotent.
- Can someone gain rights they should not have? Writes on others' data still go through the central check of #68.

## Non-goals

- Private positions (#41) and any hiding of positions from the household.
- Ticking, booking or editing inside the household view.
- The household curve over all accounts (#32), plan hints, household budgets of their own.
- The web page of the household plan (the human's part).

## Risks

- Creating months of other members when opening the household plan is the lead's adaptation of the original, which instead waited for every member; if a member's contracts change before they look, their month holds the state of that moment, exactly as if they had opened it themselves.
- Every member sees all positions of the others until #41; the human chose this knowingly.

## Open questions

None.

## Approval

Approved by the human on 2026-10-24

Presented in the lead session on Saturday 2026-10-24. The human chose that every member sees every position for now (private positions stay in V2, #41). Creating missing member months on opening, instead of the original's wait for every member, was presented as the lead's adaptation and accepted.

Amendment 2026-10-25 (final review rounds 3 and 4, decided by the human): the switch refreshes after founding, joining, leaving, dissolving and removing in the same session (M1 of round 3); a negative carry-over stays out of the household figures (found by kai while fixing round 2); the month-creation test includes a stranger's contract (m2).
