# Customer requirements: Where bookings live, the bottom bar, positions and booking kinds

Format: 2

## Background

The audit of all decisions (`docs/plans/beschluesse-audit.md`, B-17, B-27, B-30, B-32) showed that the code does something else than the human decided. On 2026-11-01 the human answered the open questions (cards P-072, P-073). Issue #51 asks for booking lists that tell the kinds apart.

## Goal

A booking is entered where it is planned, its history lives at its account, a position shows which bookings belong to it, and every booking list says at a glance what kind of booking a line is and what it was for.

## Users

People and households who plan a month, mostly on a phone.

## Requirements

R1 (must): The bottom bar on the phone shows exactly Start, Plan, Haushalt, Mehr, all readable and at least 44 px tall at 360 px; Sparen and Verträge are reached through Mehr — because the human said: "untere Leiste mach start plan haushalt und mehr. das sollte reichen. Weil den eigenen plan nutzt man viel und den haushaltsplan".
R2 (must): The target Haushalt opens the household plan; without a household it shows the way to found or join one — because that target exists for the household plan, and someone without a household needs the way in.
R3 (must): New bookings are only created in the month of the plan; the menu entry and the page Buchungen are gone and the old address leads to the current month — because the human said "Nur im Monat, Menüpunkt weg".
R4 (must): The plus and the start page open the current month with the booking dialog — because the fastest way to book must lead to the same place.
R5 (must): Movements by date stand at their account, with edit and delete — because a booking must stay findable and changeable without the page Buchungen.
R6 (must): Date and plan month stay separate as in spec 66 R7 — because the human decided so on 2026-10-31.
R7 (must): Several bookings can belong to one position, also with another category, also after the position was ticked — because the human said: "Zu einem Posten können mehrere Buchungen gehören. Zb für Lebensmittel. Oder eben zb bei GEZ, dass eine 2. Buchung zb zu Mahnkosten zählt, aber immer noch Teil des Postens ist."
R8 (must): A position shows which bookings belong to it — because otherwise nobody can check the booked sum.
R9 (must): Booking lists tell income, expense, transfer and carry-over apart by colour and by word, colour never alone — because of issue #51; "Farbe nie allein".
R10 (must): "Letzte Buchungen" shows what a booking was for — because a date and an account do not say that.
R11 (wont): Any other rule of bookings, plans or grants — because the task names only these decisions.

## Constraints

Radzen and the existing theme; no new dependency; no migration. Grants of spec 68 stay as they are.

## Answered questions

- Q1: Which targets does the bottom bar have? First six on 2026-11-01, then the same day replaced by the human with four: Start, Plan, Haushalt, Mehr.
- Q2: What happens with the page Buchungen? "Nur im Monat, Menüpunkt weg"; movements at the account (the human, 2026-11-01, P-072).
- Q3: What does splitting at a position mean? No splitting of the position itself: several bookings belong to one position (the human, 2026-11-01, P-073).

## Open questions

None.

## Approval

Approved by the human on 2026-11-01 through his decisions (P-072, P-073, issue #51), which the lead passed on word by word; he has not read this text himself.
