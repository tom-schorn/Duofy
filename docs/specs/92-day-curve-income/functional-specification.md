# Functional specification: Day curve counts booked income of the plan month on the 1st

Format: 2

## Summary

In the day curve, every income of the plan month counts on the 1st, booked or open; expenses stay on their day. Past months no longer show a gap until a mid-month income such as Kindergeld.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R4.

## Components

None.

## Solution

`MonthCurve.Build` receives booked moves with a flag whether they are income (or the caller passes income moves separately). Booked income of the plan month is added to day 1 (income dated before the month already counts in the start balance and stays there); booked expenses keep their day, and a booking dated after the month keeps counting on the last day. The caller in `MonthView`/`MonthReview` passes the flag from the booking kind. The doc comment of `MonthCurve` names the new rule with the date 04.11.2026.

## Behaviour

- R1: An income booked on the 19th of this plan month raises the curve from day 1 on; an income booked after the month end with this plan month also counts on day 1.
- R2: An expense booked on the 10th lowers the curve from day 10 on; open expenses as before.
- R3: A transfer between own accounts changes nothing.
- R4: An open income rest counts on day 1, as before.

## Data and interfaces

`CurveMove` gets an `Income` flag (default false) or an equivalent; no API change.

External input: no

## Acceptance criteria

- [x] A1 (R1): Given a past month with an income of 758 booked on the 19th and an expense of 600 on the 5th and no carry-over, when the curve is built, then the balance on day 5 is 158 and no day is below zero.
- [x] A2 (R1): Given an income of the plan month dated after the month end, when the curve is built, then it counts on day 1.
- [x] A3 (R2): Given an expense booked on the 10th, when the curve is built, then the balance drops on day 10 and not before.
- [x] A4 (R3): Given a transfer between own accounts, when the curve is built, then no day changes.
- [x] A5 (R4): Given an open income rest in the current month, when the curve is built, then it counts on day 1.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1, A2 | `MonthCurveTests.A1_booked_income_of_a_past_month_counts_on_the_first_so_there_is_no_gap_before_the_child_benefit`, `A2_booked_income_dated_after_the_month_end_counts_on_the_first`, `From_the_plan_a_booked_income_counts_on_the_first` |
| R2 | A3 | `MonthCurveTests.A3_a_booked_expense_keeps_its_day` |
| R3 | A4 | `MonthCurveTests.A4_a_transfer_between_own_accounts_changes_no_day` |
| R4 | A5 | `MonthCurveTests.A5_an_open_income_rest_of_the_current_month_counts_on_the_first` |

## Non-goals

- No change to how the plan assigns bookings to months.
- No change to the other charts of the month review.

## Risks

None.

## Open questions

None.

## Approval

Approved by the human on 2026-11-04 (in chat after the check-in).
