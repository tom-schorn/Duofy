# Functional specification: Responsive layout that fits every width

Format: 2

## Summary

The layout steps move from 768 / 1280 to 600 / 840 (help column still from 1280). The key figure cards of the month become a grid that has two columns below 600 px and one row above. Every page is checked at five widths for sideways scrolling and cut amounts. The Android status bar icons follow the theme.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R6.

## Components

None.

## Solution

- `Shared/Duofy.Shared/Layout/WidthRanges.cs`: PhoneMax 599, TabletMin 600, TabletMax 839, DesktopMin 840; a separate constant for the help column at 1280. All media queries in `Shared/Duofy.Shared/wwwroot/duofy.css` use these values: `max-width: 767px` → 599, `min-width: 768px` → 600, the tablet range → 600 to 839, the full side bar from 840; the help column rule stays at 1280. The touch rule "unter 1280 px wird getippt" stays at 1279.
- Key figure cards of the month (`Shared/Duofy.Shared/Pages/Month/…`, the strip with Einnahmen, Verplanbar, Anstehend, Übertrag): a CSS grid inside the page margins, `repeat(2, minmax(0, 1fr))` below 600 px, `repeat(4, minmax(0, 1fr))` from 600 px; no horizontal scroll container. Amounts use `.num` and shrink one font step below 600 px instead of being cut; labels may wrap.
- Every page: fix what the measured check (below) finds, with `min-width: 0` and wrapping, not with `overflow: hidden` on amounts.
- `docs/layout.md`: the table of width ranges names the new steps.
- Android (`Mobile/Duofy/Platforms/Android/…`): status bar icons dark on the light theme and light on the dark theme, switched with the theme the app shows.
- A measured check: a test (bUnit or a Playwright script under `Tests/`) that renders the main pages (Start, Plan, Monat, Buchungen, Konten, Verträge, Haushalt, Sparen) at 360, 412, 600, 840 and 1008 px and fails when `document.documentElement.scrollWidth > clientWidth` or an element with `.num` is clipped. If a browser test is too heavy for the PC, the worker states it and the reviewer runs it once.

## Behaviour

- R1: At 599 px the bottom bar is shown and no side bar; at 600 px the folded side bar.
- R2: At 839 px the side bar is folded, at 840 px full; the help column appears at 1280 px.
- R3: On a 412 px phone the month shows Einnahmen and Verplanbar in the first row, Anstehend and Übertrag in the second.
- R4: At each of the five widths, no page scrolls sideways and every amount is fully visible.
- R5: In the light theme the status bar shows dark icons, in the dark theme light ones.
- R6: Buttons and tick boxes below 1280 px are at least 44 px high.

## Data and interfaces

No API change.

External input: no

## Acceptance criteria

- [x] A1 (R1, R2): Given the width constants and the stylesheet, when the media queries are read, then the phone range ends at 599, the folded side bar covers 600 to 839, the full side bar starts at 840 and the help column at 1280.
- [x] A2 (R3): Given the month at 412 px, when it is rendered, then the four key figure cards are in two rows of two and none is outside the page margins.
- [x] A3 (R3): Given the month at 600 px or wider, when it is rendered, then the four cards stand in one row.
- [x] A4 (R4): Given the main pages at 360, 412, 600, 840 and 1008 px, when the measured check runs, then no page is wider than the viewport and no amount is clipped.
- [x] A5 (R5): Given the Android app in the light theme, when it shows a page, then the status bar icons are dark; in the dark theme they are light.
- [x] A6 (R6): Given the stylesheet, when the touch rule is read, then it still applies below 1280 px with a minimum of 44 px.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | `InterfaceRulesTests.A1_the_width_steps_are_599_600_839_840_and_1280` (new or changed) |
| R2 | A1 | `InterfaceRulesTests.A1_the_width_steps_are_599_600_839_840_and_1280` (new or changed) |
| R3 | A2, A3 | `MonthFiguresLayoutTests.A2_on_a_phone_the_cards_stand_two_by_two`, `A3_from_600_the_cards_stand_in_one_row` (new), plus the card positions in `check.mjs` (asserts 2x2 below 600 px and one row of four from 600 px, and that four cards exist) |
| R3 | A2, A3 | Old test: `MonthPageTests.The_blocks_stand_in_one_row_and_as_two_by_two_on_phones` reads the CSS rules (grid, no strip) |
| R4 | A4 | `Tests/responsive-widths/check.mjs` (new, measured in a real browser; a bUnit test cannot measure widths; asserts sign-in, route, clipping by ancestors, with a built-in self-test of a deliberately clipped `.num`) |
| R5 | A5 | `StatusBarThemeTests.A5_status_bar_icons_follow_the_theme` and `StatusBarThemeTests.The_listener_watches_the_file_preferences_default_writes_to` (source checks; the live switch is checked in the emulator by the lead) |
| R6 | A6 | `InterfaceRulesTests` touch rule (existing) |

## Non-goals

- No redesign of the start page or plan overview (that is #94).
- No change to the notification prompt.
- No change of colours, fonts or spacing beyond what the widths need.

## Risks

Specs 9, 10 and 51 name the old steps; their documents are not rewritten, `docs/layout.md` is the current source.

## Open questions

None.

## Approval

Approved by the human on 2026-11-05 (in chat, after the UI/UX report).
