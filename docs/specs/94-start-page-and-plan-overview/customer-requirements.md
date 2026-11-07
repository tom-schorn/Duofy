# Customer requirements: Start page and plan overview show the important things at a glance

Format: 2

## Background

The human, 05.11.2026 (P-097, P-098, P-103, P-105, issue #94), and the UI/UX report of 05.11.2026 (`.workday/ui-recherche/2026-11-05-ui-ux-bericht.md`, findings 2 to 5): the start page says the same thing several times ("Ziel 50 %, Ist 20 %", a bar with "40 %", "Ist 20 % von Soll 50 %"), nobody understands the guide values, the pot colours differ between pages, the account balances are missing, own plan and household plan are not both visible, there is no year view. The plan overview starts with three empty future months, its bars compare booked amounts with the guide value instead of the plan, the month cards load one by one, and "inkl. Übertrag" breaks lines. Unplanned expenses hide as text at the bottom of the month. The contract change card (#100) is far too big.

Decisions of the human in the lead session, 06.11.2026, one rule at a time:
1. On top of the start page: Verplanbar large, directly below one line with the balances of the available accounts and their sum.
2. Soll/Ist per pot: one line in euro, "1.634 € von 2.100 €", the bar is the plan, the fill is what is booked; no percent guide values on the start page.
3. One fixed colour per pot everywhere (needs blue, wants yellow, savings green), always with the name; overspent shows a red end of the bar and the text "überzogen".
4. Own plan and household plan on the start page one below the other: first the own block, below a smaller household block with Verplanbar and pots.
5. A small year line on the start page: one small bar per month from January to now for what was left or missing, green or red; tapping opens the plan overview; no future months.
6. Plan overview: the current month on top, past months below; future months only when they already exist; instead of empty cards a button "Nächsten Monat anlegen".
7. Pot bars on the month cards: the target is the sum of the planned positions of the pot, the fill is what is booked ("1.634 € von 1.778 € geplant").
8. In the month, unplanned bookings stand as their own row "Ungeplant · 3 Buchungen · 88,89 €" under the positions of each pot, styled differently from a position, without a tick box; tapping expands the bookings.
9. The contract change card becomes one narrow line on top of the month ("2 Verträge geändert · Ansehen"); tapping opens the list with the differences and the buttons to take them over.
Also asked by the human (P-098): one API endpoint that delivers the plan overview at once, and no line break from "inkl. Übertrag".

## Goal

The start page and the plan overview show at a glance what is left, what is on the accounts and how far the plan has come, with one number per statement.

## Users

Everyone who plans a month, alone or in a household; on phone, tablet and desktop.

## Requirements

R1 (must): The start page shows Verplanbar of the current month large, and directly below one line with the balance of each available account and their sum — because the human chose rule 1 on 06.11.2026.
R2 (must): The start page shows each pot as one line with name, bar and "<booked> € von <planned> €", without percent guide values — because the human chose rule 2 on 06.11.2026.
R3 (must): Each pot has one fixed colour on every page (needs, wants, savings), always shown together with its name; an overspent pot shows a red bar end and the word "überzogen" — because the human chose rule 3 on 06.11.2026.
R4 (must): A member of a household sees below the own block a smaller household block with the household's Verplanbar and pots; without a household the block is not shown — because the human chose rule 4 on 06.11.2026.
R5 (must): The start page shows a year line with one bar per month from January of the current year to the current month, green for a rest, red for a gap, without future months; tapping it opens the plan overview — because the human chose rule 5 on 06.11.2026.
R6 (must): The plan overview lists the current month first, then the past months; future months appear only when they exist; a button "Nächsten Monat anlegen" replaces empty future cards — because the human chose rule 6 on 06.11.2026.
R7 (must): The pot bars on the month cards of the plan overview use the sum of the planned positions of the pot as the target and the booked amount as the fill — because the human chose rule 7 on 06.11.2026.
R8 (must): In the month, each pot ends with one row "Ungeplant" (count and sum of the bookings without position in that pot), visibly different from a position and without a tick box; tapping it shows those bookings — because the human chose rule 8 on 06.11.2026.
R9 (must): The contract change card in the month is one narrow line with the number of changed contracts and "Ansehen"; tapping opens the differences and the buttons to take them over — because the human chose rule 9 on 06.11.2026.
R10 (should): The plan overview gets its month cards from one API call — because it is the human's wish in P-098.
R11 (should): "inkl. Übertrag" never causes a line break of the Verplanbar figure — because the human asked for it in P-098.
R12 (must): Every page of this change passes the measured responsive check of #101 at 360, 412, 600, 840 and 1008 px — because #101 must not regress.

## Constraints

- No database change. A new read-only API endpoint is allowed for R10.
- Colours meet WCAG contrast 3 : 1 for bars and 4.5 : 1 for text, in light and dark theme.

## Open questions

None.
