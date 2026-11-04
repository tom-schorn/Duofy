# Customer requirements: Day curve counts booked income of the plan month on the 1st

Format: 2

## Background

The day curve of a month (spec 82, R14, `Shared/Duofy.Shared/Services/MonthCurve.cs`) counts open income positions on the 1st of the month (Tom, 03.11.2026), but booked income on its real date. Jasmin's Kindergeld and Kinderzuschlag arrive around the 19th and count for the month they are paid in. In past months, where only bookings count, the curve therefore dips until the 19th and shows a gap that the plan does not have.

The human, 04.11.2026: "und diese tagesdiagramm rechnet der mit plan auch runter? sonst waren in den vormonaten immer lücken weil kindergeld zu dem monat zähle wo es ausgezahlt wurde" — and on the question whether booked income should count on the 1st: "Ja, Einnahmen am 1."

## Goal

The day curve shows whether a month carries as a whole: every income of the plan month, booked or open, counts on the 1st; expenses keep their own day.

## Users

Everyone who looks at the month review, in the current and in past months.

## Requirements

R1 (must): A booked income whose plan month is this month counts on the 1st of the month, whatever its date — because the human chose "Einnahmen am 1.".
R2 (must): Expenses, booked or open, keep their current rule (booked on their day, open on their due day or tomorrow) — because only income was asked.
R3 (must): Transfers between own accounts still do not change the curve — because that rule stays.
R4 (must): Open income keeps counting on the 1st, as today — because it is the existing decision of 03.11.2026.

## Constraints

- No backend change; the curve is computed in Shared from what the API sends.

## Open questions

None.
