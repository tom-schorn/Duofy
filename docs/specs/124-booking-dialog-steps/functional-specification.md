# Functional specification: Booking dialog in steps with clear choices

Format: 2

## Summary

The booking dialog becomes a step dialog (kind and amount, accounts, category and position, split, check and save), with combo boxes and a clear kind selection; Android pickers follow the theme.

## Customer requirements

[customer-requirements.md](customer-requirements.md)

## Components

- [Booking](docs/components/booking.md)
- [Account](docs/components/account.md)
- [Plan](docs/components/plan.md)

## Solution

Rebuild the booking dialog on the spec-11 step component. Steps: 1 kind, amount, date; 2 account(s); 3 pot/category and optional position; 4 split (only if chosen); 5 summary and save. Combo boxes reuse one searchable select component. The Android theme sets the dialog theme from the app's chosen theme (not only the device's night mode).

## Behaviour

- R1: step x of y visible; back keeps input; save only in the last step.
- R2: selected kind visible in both themes.
- R3: typing filters the list; no extra button.
- R4: with three split lines the dialog stays within the screen at 360 px and 800 px.
- R5: light app theme on a dark device gives light pickers.

## Data and interfaces

None.

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given the booking dialog, when it opens, then it shows 'Schritt 1 von n' with back/next at a fixed place, and going back from step 3 to 1 keeps all input.
- [ ] A2 (R2): Given the kind choice, when Umbuchung is selected, then that option carries the selected state (aria-pressed or checked) and a contrast of at least 3:1 to the others in light and dark theme.
- [ ] A3 (R3): Given the account, category and position fields, when they render, then each is a combo box with filtering and no separate 'Bitte wählen' button; categories are grouped by pot and positions show name, due day and planned amount.
- [ ] A4 (R4): Given 'Aufteilen' with three lines, when the split step renders at 360 and 800 px, then the dialog content stays within the viewport height (scrolls inside) and the lines are edited in their own step.
- [ ] A5 (R5): Given the app in light theme while the device is in dark mode, when a date or time picker opens on Android, then its theme is the light one (test of the platform theme setting).

- [ ] A6 (R6): Given the booking dialog in step 2 with amount and account set, when "Speichern" in the foot is pressed, then the booking is saved like from the last step; before amount and account are set the button is not shown.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | Tests/Duofy.Shared.Tests/BookingDialogStepsTests.cs |
| R2 | A2 | Tests/Duofy.Shared.Tests/BookingDialogStepsTests.cs |
| R3 | A3 | Tests/Duofy.Shared.Tests/BookingDialogStepsTests.cs |
| R4 | A4 | Tests/Duofy.Shared.Tests/BookingDialogStepsTests.cs, Tests/booking-dialog-viewport/check.mjs |
| R6 | A6 | Tests/Duofy.Shared.Tests/BookingDialogStepsTests.cs |
| R5 | A5 | Tests/Duofy.App.Tests/AndroidNightModeTests.cs |

## Non-goals

- No change to booking rules, pots or the API.
- No redesign of other dialogs in this spec.

## Risks

Longer flow for quick bookings: contained by the early save of R6.

## Open questions

None.

## Approval

Approved by the human on 2026-11-11

Amended on 2026-11-11, decided by the human after the review: early save from step 2 on (R6/A6).

In the lead session: the human gave the points on 11.11.2026 and ordered "spezifizieren und zur Arbeit schicken".
