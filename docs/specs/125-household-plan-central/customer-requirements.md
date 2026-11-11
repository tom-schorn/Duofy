# Customer requirements: Household plan as the central workplace; print and contract timing

Format: 2

## Background

On 11.11.2026 the human stated that the household plan is the central place of the whole app and that he never wanted it read-only; spec 69 R6 ('read-only') is replaced. The household plan lacks the buttons the personal plan has: print, add position, add booking. 'Drucken' in the personal plan does nothing. A contract change scheduled for next month already shows in the current month's plan.

## Goal

From the household plan everyone works for every member within the grants, with the same buttons at the same place as in the personal plan; printing works; contract changes apply from their month.

## Users

Household members, mostly couples and families, on tablet, phone and desktop.

## Requirements

R1 (must): The household plan shows the buttons Drucken, Posten hinzufügen and Buchung hinzufügen at exactly the same place as the personal plan — because the household plan is the central workplace (human, 11.11.2026).
R2 (must): Posten hinzufügen and Buchung hinzufügen in the household plan first ask for the person (members who granted the asker at least Anlegen in that area, and the asker), then open the normal dialog for that person — because one works for every member from there (human, 11.11.2026).
R3 (must): Ticking and changing positions and bookings of other members work in the household plan under the grants of spec 68, the same way as in that member's own plan — because the household plan is not read-only (human, 11.11.2026).
R4 (must): Drucken in the personal plan prints the month; Drucken in the household plan first asks whether to print one person's plan or the whole household plan — because printing does nothing today (11.11.2026).
R5 (wont): A contract change scheduled for a later month does not change the positions of months before it — because the human clarified on 11.11.2026 that a change from next month is done by ending the old contract and starting a new one, which already works.

## Constraints

- Grants of spec 68 decide every action; no new rights.
- Reuse the personal plan's buttons, dialogs and print layout.

## Open questions

None.
