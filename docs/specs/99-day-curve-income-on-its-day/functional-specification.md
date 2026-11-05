# Functional specification: Day curve counts booked income on its own day again

Format: 2

## Summary

Reverts the income rule of spec 92 for booked income inside the month: it counts on its own day, so Jasmin's month shows a gap until the Kindergeld on the 19th. Income dated before the month stays in the start balance; open income rests stay on the 1st.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R5.

## Components

None.

## Solution

In `Shared/Duofy.Shared/Services/MonthCurve.cs`, `Build` places a booked income dated inside the month on its own day and one dated after the month on the last day (the same placement as an expense); income dated before the month already counts in the start balance. `CurveMove.Income` is no longer needed for the placement: remove the flag and its use in the callers (`MonthView`/`MonthReview`), or keep it without effect only if removing it touches more than these files. `CurveOpen.Income` (open income rest on the 1st) stays. The doc comments of `MonthCurve`, `CurveMove` and `CurveOpen` name the rule with the date 05.11.2026 and the reason (P-104). The help text of the month review, if it describes the income rule, says the same.

## Behaviour

- R1: An income of 758 booked on the 19th raises the curve from day 19 on.
- R2: A salary booked on the 28th of the previous month with this plan month counts in the start balance.
- R3: An open income rest counts on day 1.
- R4: An income of the plan month dated after the month end counts on the last day.
- R5: Expenses and transfers as before.

## Data and interfaces

No API change.

External input: no

## Acceptance criteria

- [x] A1 (R1): Given a past month with no carry-over, an expense of 600 on the 5th and an income of 758 booked on the 19th, when the curve is built, then the balance on day 5 is -600, the first day below zero is the 5th, and the balance on day 19 is 158.
- [x] A2 (R2): Given a salary of 2000 of this plan month dated on the 28th of the previous month, when the curve is built, then the start balance includes 2000.
- [x] A3 (R3): Given an open income rest of 255 due on the 19th in the current month, when the curve is built, then it counts on day 1.
- [x] A4 (R4): Given an income of the plan month dated after the month end, when the curve is built, then it counts on the last day and not before.
- [x] A5 (R5): Given an expense booked on the 10th and a transfer between own accounts, when the curve is built, then the balance drops on day 10 and the transfer changes no day.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | `MonthCurveTests.A1_booked_income_inside_the_month_counts_on_its_day_so_a_gap_shows` (new); also `From_the_plan_a_booked_income_counts_on_its_day`, `In_the_household_plan_a_booked_income_counts_on_its_day` |
| R2 | A2 | `MonthCurveTests.A2_income_dated_before_the_month_counts_in_the_start` (new) |
| R3 | A3 | `MonthCurveTests.A5_an_open_income_rest_of_the_current_month_counts_on_the_first` (existing), `Open_income_counts_on_the_first_even_when_today_is_later_and_only_its_open_rest` (changed) |
| R4 | A4 | `MonthCurveTests.A4_income_dated_after_the_month_counts_on_the_last_day` (new, replaces spec 92 A2) |
| R5 | A5 | `MonthCurveTests.A3_a_booked_expense_keeps_its_day`, `A4_a_transfer_between_own_accounts_changes_no_day` (existing) |

## Non-goals

- No change to how the plan assigns bookings to months.
- No change to Verplanbar (#98) or other charts.

## Risks

Spec 92's tests for "booked income on the 1st" must be changed, not deleted silently; the change is named in the commit.

## Open questions

None.

## Approval

Approved by the human on 2026-11-05 (in chat after the check-in).
