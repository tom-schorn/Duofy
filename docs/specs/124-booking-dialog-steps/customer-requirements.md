# Customer requirements: Booking dialog in steps with clear choices

Format: 2

## Background

On 11.11.2026 the human found in the booking dialog: the choice Ausgabe/Einnahme/Umbuchung shows no visible selection; the account choice has an extra 'Bitte wählen' button instead of a combo box; category and position choices are confusing; 'Aufteilen' makes the dialog grow without end, although dialogs work in steps (spec 11 step-dialog exists); native Android dialogs (date and time pickers) appear dark although the light theme is active.

## Goal

The booking dialog leads through clear steps on phone and tablet, every choice shows what is selected, and native pickers follow the app theme.

## Users

Everybody who books, mostly on the phone or tablet.

## Requirements

R1 (must): The booking dialog uses the step dialog of spec 11 (step x of y, back and next at a fixed place, input kept when going back) — because the human decided dialogs work in steps (repeatedly).
R2 (must): The kind Ausgabe/Einnahme/Umbuchung shows the selected kind clearly (filled, contrast at least 3:1 against the others) in light and dark theme — because nothing visible was selected (11.11.2026).
R3 (must): Account, category and position are chosen with combo boxes (searchable list in the field, no separate 'Bitte wählen' button); categories grouped by pot, positions grouped by due day with name and planned amount — because the current choices are confusing (11.11.2026).
R4 (must): 'Aufteilen' opens its own step for the split lines instead of growing the dialog; each line is added in that step; the dialog height stays within the screen — because the dialog grew without end (11.11.2026).
R5 (must): Native Android date and time pickers follow the app theme: light theme gives light pickers, dark theme dark pickers, with readable buttons — because they were dark in the light theme (11.11.2026).

## Constraints

- Reuse the existing step dialog and account picker components (specs 11, 16); no new UI library.
- Booking rules (spec 59, 72, 112) unchanged.

## Open questions

None.
