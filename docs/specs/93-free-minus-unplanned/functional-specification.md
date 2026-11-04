# Functional specification: "Verplanbar" subtracts booked expenses without a position

Format: 2

## Summary

The backend computes Verplanbar with the booked lines of the plan month that have no position: expense lines lower it, income lines raise it. Every view already shows the backend's Free value, so the change is in one place plus the help text.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R7.

## Components

- [Plan](docs/components/plan.md)
- [Booking](docs/components/booking.md)

## Solution

`PlanFigures.Free(positions, carriedIn)` gets the booked lines of the plan month (the bookings the viewer may see, as `CarriedIn` already does) and becomes `Income − Fixed + carriedIn + Σ income lines without position − Σ expense lines without position`; transfers are ignored, and carry-over bookings are not counted twice (they are already in carriedIn). `PlanStore` (month and month list) and `HouseholdPlanStore` pass the bookings. The help text Help_month_2_Body (and Help_start/plans texts if they define Verplanbar) names the new rule. Shared shows `Overview.Free` unchanged.

- **R7 (amendment of 2026-11-04 evening)**: in `PlanFigures.Income` and `PlanFigures.Fixed` as used for Free, each position counts with max(planned, booked); pass-through positions stay out as before.

## Behaviour

- R1: A month with income 2000, fixed 1500, no carry-over and a grocery booking of 100 without position shows Verplanbar 400.
- R2: The month list (`GET /api/v1/plans`) and the month (`GET /plans/{y}/{m}`) show the same Free; the household plan subtracts the unplanned lines of all members it shows.
- R3: A booking line on a position, a transfer and the carry-over booking do not change Free beyond the existing rule.
- R4: An income line of 20 without position raises Free by 20.
- R7: An expense position planned 4.48 with 5.48 booked lowers Free by 5.48; a Lebensmittel position planned 600, ticked with 426 booked, lowers Free by 600; an income position planned 267 with 300 booked raises Free by 300, with 0 booked by 267.
- R5: The help text says that expenses without a position are subtracted.

## Data and interfaces

No schema or database change; `PlanOverview.Free` and `PlanMonthResponse.Free` keep their names with the new value.

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given income 2000, fixed 1500 and an expense of 100 without position in the plan month, when the plan is read, then Free is 400.
- [ ] A2 (R2): Given that month, when the month list is read, then its Free equals the month's Free.
- [ ] A3 (R2): Given a household plan with one member's unplanned expense of 50, when it is read, then its Free is 50 lower than without it.
- [ ] A4 (R3): Given an expense line on a position, a transfer between own accounts and a carry-over, when the plan is read, then Free changes only by the existing rule.
- [ ] A5 (R4): Given an income line of 20 without position, when the plan is read, then Free is 20 higher.
- [ ] A6 (R5): Given the help texts, when they are read, then the Verplanbar text mentions that expenses without a position are subtracted.
- [ ] A8 (R7): Given an expense position planned 4.48 with 5.48 booked, a ticked budget position planned 600 with 426 booked and an income position planned 267 with 300 booked, when the plan and the month list are read, then Free counts 5.48, 600 and 300.
- [ ] A7 (R6): Given Plagevo after the release, when Tom opens November, then Verplanbar matches what is left (shown to Tom).

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | `FreeMinusUnplannedTests.A1_A2_an_expense_without_a_position_lowers_free_in_the_month_and_in_the_list`, `PlanFreeTests.Free_subtracts_the_booked_expenses_without_a_position` |
| R2 | A2, A3 | `FreeMinusUnplannedTests.A1_A2_…`, `A3_the_household_plan_subtracts_the_unplanned_expense_of_a_member`, `A3_the_household_sum_counts_the_unplanned_lines_of_every_member_with_and_without_the_booking_grant` |
| R3 | A4 | `FreeMinusUnplannedTests.A4_lines_on_a_position_transfers_and_the_carry_over_do_not_count_twice`, plus the unchanged `FreeWithCarryOverTests` |
| R4 | A5 | `FreeMinusUnplannedTests.A5_an_income_without_a_position_raises_free`, `PlanFreeTests.Free_adds_the_booked_income_without_a_position_together_with_the_carry_over` |
| R5 | A6 | `HelpColumnTests.The_free_amount_text_says_that_expenses_without_a_position_are_subtracted` |
| R6 | A7 | release check on Plagevo (lead) |
| R7 | A8 | `FreeMinusUnplannedTests.A8_each_position_counts_with_the_planned_or_the_higher_booked_amount_in_the_month_and_in_the_list`, `A8_the_household_counts_the_higher_booked_amount_and_an_income_with_less_booked_keeps_the_plan`, `PlanFreeTests.Each_position_counts_with_the_planned_amount_or_with_the_booked_amount_when_more_was_booked`, `PlanFreeTests.Pass_through_positions_stay_out_even_when_more_was_booked`, `HelpColumnTests.The_free_amount_text_says_that_a_position_counts_with_the_planned_or_the_higher_booked_amount` |

## Non-goals

- No new figure next to Verplanbar, no change to the pots or the day curve.
- No change to how positions or carry-overs are computed.

## Risks

- Bookings in the plan month that the viewer may not see would be missing in a household view: contained by using the same visible bookings as CarriedIn.

## Open questions

None.

## Approval

Approved by the human on 2026-11-04 in the wrap-up (including R4 income without position, and release today together with the merged features). Amended the same evening with R7 (max of planned and booked per position), approved by the human in chat.
