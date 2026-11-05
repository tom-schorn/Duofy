# Functional specification: Verplanbar counts money taken back from savings

Format: 2

## Summary

`PlanFigures.Free` also counts transfers of the plan month that cross the line between available and not available accounts: money taken back into available accounts raises Verplanbar, money put away without a position lowers it. With the human's October data this moves Verplanbar from -409.97 to about -64.97.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R4.

## Components

- [Plan](docs/components/plan.md)
- [Booking](docs/components/booking.md)

## Solution

In the backend, where spec 93 passes the booked lines of the plan month to `PlanFigures.Free`, transfers are passed too, together with whether their source and target account count as available (`Account.CountsAsAvailable`). Free adds `Σ transfers not-available → available` and subtracts `Σ transfers available → not-available whose line has no position`. Carry-over bookings stay out as before. `PlanStore` (month and month list) and `HouseholdPlanStore` use the same path. In the household plan only the months whose bookings the viewer may see count their transfers, like the carry-over. The help text that defines Verplanbar names the rule in one sentence.

## Behaviour

- R1: A month with Free 0 and a transfer of 270 from Depot-Verrechnung (not available) to Kontokorrent (available) shows Free 270.
- R2: A transfer of 120 from Kontokorrent to Tagesgeld-Verrechnung on the position "Sparen Allgemein" leaves Free unchanged; the same transfer without a position lowers Free by 120.
- R3: A transfer of 90 from Kontokorrent to Kreditkarte (both available) and one of 160 from Depot-Verrechnung to Tagesgeld-Verrechnung (both not available) change nothing.
- R4: Month list, month and household plan show the same value.

## Data and interfaces

No API shape change; `Overview.Free` carries the new value.

External input: no

## Acceptance criteria

- [x] A1 (R1): Given a plan month with Free 0 before and a transfer of 270 from a not-available to an available account, when Free is computed, then it is 270.
- [x] A2 (R2): Given a transfer of 120 from an available to a not-available account with its line on a position, when Free is computed, then Free does not change beyond the position rule; without a position it is 120 lower.
- [x] A3 (R3): Given a transfer between two available accounts and one between two not-available accounts, when Free is computed, then it does not change.
- [x] A4 (R4): Given a month with such transfers, when the month list, the month and the household plan are read, then all show the same Free.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | `PlanFiguresTests.A1_a_withdrawal_from_savings_raises_free` |
| R2 | A2 | `PlanFiguresTests.A2_a_deposit_without_position_lowers_free_and_on_a_savings_position_counts_once` |
| R3 | A3 | `PlanFiguresTests.A3_transfers_on_the_same_side_change_nothing` |
| R4 | A4 | `PlanEndpointsTests.A4_month_list_month_and_household_show_the_same_free_with_transfers` |

## Non-goals

- No change to how bookings are assigned to plan months.
- No new switch in the booking dialog.

## Risks

The human's October would show about -64.97, not exactly -66.49; the 1.52 difference is not explained yet and is checked with the human after the build.

## Open questions

None.

## Approval

Approved by the human on 2026-11-05 (in chat after the check-in).
