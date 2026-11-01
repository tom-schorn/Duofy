# Customer requirements: Start page with blocks, greeting and quick facts

Format: 2

## Background

The start page only greets the person and says "Hier entsteht bald deine Übersicht". Issues #33 and #34 of tom-schorn/duofy-dotnet ask for the real start page: a greeting, the free amount of the month, a quick way to a new booking, a hint to plan together, the payments coming up and the target-actual bars of the month.

## Goal

Whoever opens the app sees at once how much is free this month, what is due in the next days, how the month stands against its plan, and can start a booking or open the matching page in one step.

## Users

Every signed-in person, daily on the phone (one column) and at the desk (several columns); new people without a plan or a household as well.

## Requirements

R1 (must): The start page greets the person by first name and shows the free amount of the current month in a colour block, with the carry-over named when one is included (issue #33) — because the free amount is the first thing a person wants to know.
R2 (must): The start page offers a quick way to a new booking (issue #33) — because most visits are to book something.
R3 (must): While the person is in no household, a hint "Mit jemandem zusammen planen?" leads to the household page (issue #33) — because planning together is not found by chance.
R4 (must): A person without a plan for the current month sees an empty state in light and dark, on the phone and on the desk, with a clear next step (issue #33) — because a new person must not stand in front of a blank page.
R5 (must): The start page lists the payments of the next days and the overdue ones (issue #34) — because that is what a person has to act on.
R6 (must): The start page shows the target-actual bars of the pots of the month in a compact form (issue #34) — because it answers "how am I doing" at a glance.
R7 (must): Every block opens the matching page (issue #34) — because the start page is an entry, not an end.
R8 (must): The start page shows the figures of the person whose numbers are open, or of the household, and shows another person's amounts only as far as the grants and the household plan allow (spec 68, spec 69) — because shared data stays shared only as far as agreed.
R9 (wont): The hint when a person deviates from the guideline value (issue #34) — because it depends on P6, which is not built yet.

## Constraints

No new migration and no new endpoint. Existing building blocks (colour block, target-actual bar, line table) are used. The menu and the width of the other pages belong to another task. No Radzen.

## Open questions

None.
