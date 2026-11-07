# Customer requirements: Responsive layout that fits every width

Format: 2

## Background

The human, 05.11.2026 (P-100, screenshot of the Windows app in a narrow window): the key figure cards (Einnahmen, Verplanbar, Anstehend) run out to the right and texts are cut; on the phone it is worst. The UI/UX report of 05.11.2026 (`.workday/ui-recherche/2026-11-05-ui-ux-bericht.md`) reproduced it on a Pixel 7 in the emulator: the four cards of the month sit in a sideways strip without page margin and without a hint, and "Übertrag" is not visible without swiping. Plagevo switches its layout at 768 and 1280 px; Android (Material 3) switches at 600 and 840 dp, Windows at 640 and 1008 px. The status bar of the Android app shows white icons on the light background.

Decisions of the human in the lead session, 05.11.2026: switch at 600 and 840 like Android; on the phone the four key figure cards stand as a 2 × 2 grid; the status bar fix is part of this issue, the notification prompt is not.

## Goal

Every page of Plagevo can be read and used at every width from 360 px on, without sideways scrolling and without cut amounts.

## Users

Everyone, on phone, tablet, narrow and wide windows.

## Requirements

R1 (must): Below 600 px the phone layout applies (bottom bar, no side bar) — because the human chose the Android steps.
R2 (must): From 600 to 839 px the side bar is folded to icons; from 840 px on it shows the full menu; the help column stays from 1280 px on — because the human chose 600 / 840 and the help column was not asked to change.
R3 (must): Below 600 px the four key figure cards of the month stand in a grid of two columns, all four visible without swiping; from 600 px on they stand in one row — because the human chose the 2 × 2 grid.
R4 (must): At 360, 412, 600, 840 and 1008 px no page scrolls sideways (except real tables and charts) and no amount is cut — because the human wants it "endlich behoben" with measured checks (WCAG 1.4.10).
R5 (must): The Android app shows dark status bar icons on the light theme and light icons on the dark theme — because the icons were almost invisible (report finding 6).
R6 (should): Touch targets stay at least 44 px below 1280 px, as today — because the existing rule must not get lost with the new steps.

## Constraints

- No new dependency; CSS and the shared layout, plus one Android setting.
- The notification prompt (report finding 7) is out of scope.

## Open questions

None.
