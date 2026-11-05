# Customer requirements: Day curve counts booked income on its own day again

Format: 2

## Background

Spec 92 (live since 04.11.2026) puts every booked income of the plan month on the 1st. The question behind it was worded in a misleading way: the human meant that income received before the month (the salary of the 28th of the previous month) counts on the 1st, not that income inside the month moves to the 1st. Jasmin's Kindergeld and Kinderzuschlag arrive around the 19th and are for that same month, so her curve must show the gap until the 19th.

The human, 05.11.2026 (P-104): "Bei Jasmin hätte eine Lücke entstehen müssen, weil Kindergeld und Kinderzuschlag erst um den 19. kamen und für denselben Monat galten." On open income, in the meeting 05.11.2026: open income keeps counting on the 1st.

## Goal

The day curve shows real gaps inside a month: money that comes in during the month counts from the day it comes.

## Users

Everyone who looks at the month review, in the current and in past months.

## Requirements

R1 (must): A booked income dated inside the month counts on its own day — because the human wants to see the gap until a mid-month income (P-104).
R2 (must): A booked income of the plan month dated before the month counts in the start balance (day 1), as before spec 92 — because that is the salary received early for this month.
R3 (must): An open (not yet fully booked) income position keeps counting with its rest on the 1st — because the human chose it on 03.11. and again on 05.11.2026; a position only knows its day, and the salary for the next month would otherwise show a false gap.
R4 (must): A booked income of the plan month dated after the month end counts on the last day — because it belongs to the month but came late, like an expense.
R5 (must): Expenses and transfers keep their current rules — because only income was asked.

## Constraints

- No backend change; the curve is computed in Shared from what the API sends.

## Open questions

None.
