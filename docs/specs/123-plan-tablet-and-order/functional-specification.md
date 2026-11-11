# Functional specification: Plan on tablet and carry-over order

Format: 2

## Summary

The bookings table of the personal plan behaves like the one of the household plan on narrow screens, both from one component, and the carry-over lines sort by the 1st of the month.

## Customer requirements

[customer-requirements.md](customer-requirements.md)

## Components

- [Plan](docs/components/plan.md)
- [Booking](docs/components/booking.md)

## Solution

Find why the personal plan's table is wider than the household plan's (column set, min-widths, missing scroll container) and make both use one table component with a horizontal scroll container and the same responsive column rules. Sort bookings by date descending with the carry-over lines dated on the 1st of the plan month.

## Behaviour

- R1: at 800 px the table either fits or its container scrolls sideways with touch; the page itself does not scroll sideways.
- R2: a booking of the 10th, one of the 5th and the carry-over: order 10th, 5th, carry-over.
- R3: one component renders both tables.

## Data and interfaces

None.

External input: no

## Acceptance criteria

- [x] A1 (R1): Given the personal plan with ten bookings, when it renders at 360, 412, 600, 800 and 840 px, then the page has no sideways scroll and the table either fits or sits in a container with horizontal scrolling enabled (overflow-x auto), verified by a test of the rendered markup and by a screenshot check on a tablet-sized browser.
- [x] A2 (R2): Given bookings on the 10th and 5th and a carry-over from the previous month, when the personal and the household plan list them, then the order is 10th, 5th, carry-over.
- [x] A3 (R3): Given the personal and the household plan, when their bookings sections render, then both use the same table component.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | `Tests/Duofy.Shared.Tests/PlanBookingsTableTests.cs`: `A1_the_table_sits_in_its_own_region_that_scrolls_sideways`, `A1_the_card_frame_no_longer_clips_the_table`, `A1_the_tablet_range_uses_narrower_cell_padding`; measured in a real browser at 360, 412, 600, 800 and 840 px |
| R2 | A2 | `Tests/Duofy.Shared.Tests/PlanBookingsTableTests.cs`: `A2_the_list_runs_newest_first_and_the_carry_over_stands_at_the_end`, `A2_a_booking_on_the_first_stands_above_the_carry_over_which_carries_the_first_of_the_plan_month`, `A2_the_household_plan_lists_in_the_same_order` |
| R3 | A3 | `Tests/Duofy.Shared.Tests/PlanBookingsTableTests.cs`: `A3_the_personal_and_the_household_plan_use_the_same_bookings_component` |

## Non-goals

- No new columns or filters.
- No change to the household plan's content.

## Risks

None.

## Open questions

None.

## Approval

Approved by the human on 2026-11-11

Amended on 2026-11-11 after the review, approved by the human: the outgoing carry-over keeps its booking date (end of month) and stands at the top; only the incoming one stands at the end with the 1st.

In the lead session: the human gave the points on 11.11.2026 and ordered "spezifizieren und zur Arbeit schicken".
