# Customer requirements: Every booking belongs to a pot

Format: 2

## Background

The review of the plan month (finding M1) showed that the gap chart of the month overview only compares what is booked on positions. A booking without a position counts in no pot, so spending that was never planned shows no deviation at all. In the check-in of 2026-10-30 (`.workday/minutes/2026-10-30-check-in.md`, decision 9) the human decided: every booking belongs to at least one pot (Grundbedarf, Wünsche, Sparen), a position is voluntary, and unplanned bookings count in their pot, because the gap chart should show deviations from the plan.

## Goal

Whoever looks at a month sees in the gap chart how far the real spending per pot differs from the plan, including everything that was never planned.

## Users

People and households who check a month against their plan, in the overview of their own month or of the household.

## Requirements

R1 (must): A booking without a pot is refused — because a booking outside every pot would be invisible in the comparison with the plan.
R2 (must): A position stays voluntary; a booking with a pot and without a position is accepted — because not everything that happens was planned.
R3 (must): Bookings without a position count as booked in their pot, for a person and for a household, except transfers and the carry-over — because the gap chart should show deviations from the plan, and the rule for unplanned bookings of the household plan already leaves those two out.
R4 (must): The booking dialog shows the pot as required and does not choose one silently — because a silent default would put unplanned spending into Grundbedarf without anybody deciding it.
R5 (must): The actual of a quota counts positions only, as in specs 66 and 69; bookings without a position do not count in it — because the human decided in the wrap-up of 2026-10-30 that the quota bars stay a comparison of positions and that the comparison of plan and reality per month is its own topic (year view, issue #50). This takes back the change of commit 3b09a02.
R6 (wont): Changing or checking stored bookings — because the human said on 2026-10-30 that bookings without a pot do not exist (card P-059).
R7 (wont): A year view comparing plan and reality month by month — because the human names it as a later topic (issue #50).
R8 (wont): A booking can only be added in its plan month — because the human decided on 2026-10-31 that date and plan month stay separate, as in the base block: rule R7 of spec 66 (previous and next month allowed, field "Plan-Monat" in the dialog) stays unchanged. This takes back the wish of 2026-10-30 (assumption A-010).
R9 (must): Every new transfer carries a source pot and a target pot, both required, and both are shown with the booking — because the human wants to see which pots a transfer is meant to move money between. The two pots are only remembered: no figure of the plan, no quota and no sum changes because of them (decision of 2026-10-31, card P-062). A transfer goes only between own accounts; a transfer from person to person does not exist and is not built.

## Constraints

One additive database column for the target pot of a transfer (R9); stored bookings stay as they are. The rule of the household plan for unplanned bookings stays the one rule (spec 69).

## Open questions

None.

## Answered questions

Answered on 2026-10-31 by the human:
Q1 (how a transfer carries its two pots): the pots are only remembered. Example: Wünsche planned 300, Sparen planned 200, 100 moved from the savings account to the current account: plan, booked, quotas and all sums stay unchanged. Pots are Grundbedarf, Wünsche and Sparen; the same pot on both sides is allowed.
Q2 (transfer from person to person): there is none, and none is built. Transfers go between own accounts only.
