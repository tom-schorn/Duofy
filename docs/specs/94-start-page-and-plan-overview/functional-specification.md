# Functional specification: Start page and plan overview show the important things at a glance

Format: 2

## Summary

The start page gets a new order: Verplanbar large with the account balance line, pot lines in euro with fixed colours, a household block below the own block, a year line. The plan overview puts the current month first, drops empty future cards for a "Nächsten Monat anlegen" button, uses planned amounts as bar targets and loads from one endpoint. The month shows unplanned bookings as a row per pot and the contract change card as one line.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R12.

## Components

None.

## Solution

- **Start page** (`Shared/Duofy.Shared/Pages/Start.razor` and its blocks): hero with Verplanbar (existing figure) and below it a line of the available accounts (`countsAsAvailable`) with their balances and the sum, from the accounts API. Replace the "Soll und Ist" card by one line per pot: name, bar (target = planned sum of the pot, fill = booked, capped at 100 % with a red end and "überzogen" above), text "<booked> € von <planned> €". Remove the percent guide texts from the start page. Household block: when the user is in a household, a smaller block with the household plan's Verplanbar and the same pot lines, from the existing household plan endpoint. Year line: one bar per month of the current year up to the current month, value = the month's Verplanbar rest (positive green, negative red), each bar with an accessible label "<Monat>: <Betrag>"; tapping navigates to the plan overview.
- **Pot colours**: one set of CSS variables per pot (needs, wants, savings) in `duofy.css`, light and dark, used by the start page, the plan overview and the month; the existing month colours are the source. Text always next to the colour.
- **Plan overview** (`Shared/Duofy.Shared/Pages/PlanOverview.razor`, `MonthCardList.razor`): order current month, then past months descending; future months only if a plan exists; a button "Nächsten Monat anlegen" when the next month has no plan (it creates it with the existing create flow). Pot bars: target = planned sum, fill = booked. The Verplanbar label with "inkl. Übertrag" uses no-wrap for the figure and lets the hint wrap below it.
- **One endpoint** (R10): `GET /api/v1/plans/overview?year=<y>` returns for the user every existing month of that year plus existing future months: year, month, Verplanbar, carried-in flag, and per pot planned and booked sums — computed with the same `PlanFigures` as the month. The plan overview and the start page year line use it; the old per-month calls for the cards go away.
- **Month** (`Shared/Duofy.Shared/Pages/Month/…`): per pot a final row "Ungeplant · <n> Buchungen · <Summe>" when the pot has bookings without position, with a muted style and no tick box, expandable to the bookings (tap opens the booking as today). The contract change card becomes a one-line bar "<n> Verträge geändert · Ansehen" that expands to the existing list and buttons.
- **Texts** in resx, German. The help texts that describe the old start page are updated, and the approved help text list (docs/specs/82-blueprint-foundation/erklaerspalte-texte.md) gets the new texts; the human approves them in the review round.

## Behaviour

- R1: A user with Girokonto 1.200 € and Tagesgeld (not available) sees "Girokonto 1.200 € · Summe 1.200 €" under Verplanbar.
- R2: A pot planned 1.778 € with 1.634 € booked shows "1.634 € von 1.778 €" and a bar filled to 92 %.
- R3: The same pot has the same colour on start page, plan overview and month; overspent shows the red end and "überzogen".
- R4: A user in a household sees a second, smaller block "Haushalt"; a user without one does not.
- R5: On 6 November the year line shows January to November, no December.
- R6: With plans for September to November and none for December, the overview shows November, October, September and a button "Nächsten Monat anlegen".
- R7: The month card bar uses the planned sum, not 50 % of income.
- R8: Three unplanned grocery bookings of 88,89 € in total show as one row "Ungeplant · 3 Buchungen · 88,89 €" under Grundbedarf.
- R9: Two changed contracts show as one line "2 Verträge geändert · Ansehen".
- R10: Opening the plan overview makes one overview request.
- R11: At 360 px "Verplanbar 6.339,37 €" stays on one line, "inkl. Übertrag" goes below.
- R12: The measured check of #101 passes for Start, Plan and Monat at all five widths.

## Data and interfaces

New `GET /api/v1/plans/overview?year=<y>` (read only, authorised like `GET /api/v1/plans`), response list of { year, month, free, carriedIn, pots: [{ pot, planned, booked }] }. The household plan's existing endpoint is reused for R4.

External input: yes

The query parameter year is validated against the PlanRules year range; anything else answers 400.

## Threats

- Can someone pretend to be someone else? No new sign-in path; the endpoint requires the same authorisation as GET /api/v1/plans.
- Can someone change data? No: the endpoint is read only; "Nächsten Monat anlegen" uses the existing create flow with its checks.
- Can someone deny an action? Not relevant: nothing is written by this change.
- Can someone read what they should not? The endpoint returns only plans of the signed-in owner; a test asks as a second user and gets none of the first user's months. The household block uses the existing household endpoint with its grants.
- Can someone bring it to a halt? The year is validated against the PlanRules range (400 otherwise); one year is at most 12 months plus existing future plans, so the query is bounded.
- Can someone gain rights they should not have? No new roles or grants.

## Acceptance criteria

- [x] A1 (R1): Given a user with available and not-available accounts, when the start page renders, then below Verplanbar only the available accounts and their sum are listed.
- [x] A2 (R2): Given a pot planned 1.778 € with 1.634 € booked, when the start page renders, then its line reads "1.634 € von 1.778 €" and no percent guide text is shown.
- [x] A3 (R3): Given the pot needs, when start page, plan overview and month render, then they use the same colour variable and show the pot name; an overspent pot shows "überzogen".
- [x] A4 (R4): Given a user in a household, when the start page renders, then a household block with the household Verplanbar and pots is shown; given a user without a household, then it is not.
- [x] A5 (R5): Given today 2026-11-06 and plans for January to November, when the start page renders, then the year line has 11 bars, positive rests green and negative red, each with an accessible label, and tapping goes to the plan overview.
- [x] A6 (R6): Given plans for September to November and none for December, when the plan overview renders, then the order is November, October, September and the button "Nächsten Monat anlegen" is shown; no empty future cards.
- [x] A7 (R7): Given a month whose needs pot has 1.778 € planned and 1.634 € booked, when its card renders, then the bar target is 1.778 €.
- [x] A8 (R8): Given three bookings without position in pot needs summing to 88,89 €, when the month renders, then one row "Ungeplant · 3 Buchungen · 88,89 €" ends the needs pot, has no tick box and expands to the three bookings.
- [x] A9 (R9): Given two changed contracts, when the month renders, then one line "2 Verträge geändert · Ansehen" is shown and tapping it shows the existing list and buttons.
- [x] A10 (R10): Given the plan overview, when it loads, then it calls GET /api/v1/plans/overview once and the endpoint returns the figures equal to the month endpoint for each month; an invalid year answers 400 and a second user gets none of the first user's months.
- [x] A11 (R11): Given 360 px width and a carry-over, when the plan overview renders, then the Verplanbar figure is on one line.
- [ ] A12 (R12): Given the measured check of #101, when it runs for Start, Plan and Monat at 360, 412, 600, 840 and 1008 px, then it passes.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | `StartPageTests.A1_the_account_line_lists_available_accounts_and_their_sum` (new) |
| R2 | A2 | `StartPageTests.A2_a_pot_line_reads_booked_of_planned_without_guide_values` (new) |
| R3 | A3 | `PotColourTests.A3_each_pot_uses_one_colour_on_every_page` (new; also `An_overspent_pot_shows_a_red_end_and_the_word_on_every_page`, `The_pot_colours_reach_three_to_one_against_card_and_background`) |
| R4 | A4 | `StartPageTests.A4_the_household_block_shows_only_for_members` (new) |
| R5 | A5 | `StartPageTests.A5_the_year_line_has_one_bar_per_past_month` (new) |
| R6 | A6 | `PlanOverviewTests.A6_current_month_first_and_a_button_for_the_next` (new) |
| R7 | A7 | `PlanOverviewTests.A7_the_pot_bar_target_is_the_planned_sum` (new) |
| R8 | A8 | `MonthPageTests.A8_unplanned_bookings_form_one_row_per_pot` (new) |
| R9 | A9 | `MonthPageTests.A9_the_contract_change_card_is_one_line` (new) |
| R10 | A10 | `PlanOverviewEndpointTests.A10_one_call_returns_the_same_figures_as_the_months` (new; also `A10_an_invalid_year_answers_bad_request`, `A10_a_second_person_gets_none_of_the_first_persons_months`, `PlanOverviewPageTests.A10_plan_opens_the_list_of_months_with_one_overview_request`, `PlanClientTests.GetOverview_reads_the_overview_path_with_the_year_and_names_the_owner_only_when_one_is_given`) |
| R11 | A11 | `PlanOverviewTests.A11_the_free_figure_does_not_wrap` (new) |
| R12 | A12 | `Tests/responsive-widths/check.mjs` (existing, run in the review; not run by the frontend engineer because it needs a running instance and credentials) |

## Non-goals

- No change to how Verplanbar is computed.
- No new chart library.
- No change to the bookings menu (that is #104).

## Risks

The start page is the most used page: the review checks it at all widths and both themes in the emulator.

## Open questions

None.

## Implementation notes (frontend engineer, 2026-11-06)

- The overview endpoint also answers `?owner=` with the grant check of `GET /api/v1/plans` (Planung ansehen), so the member switch keeps working; without `owner` it reads only the signed-in person's plans. The response additionally carries `unpaid` (Anstehend) so the cards need no per-month call.
- Future months in the endpoint: all plans of the requested year plus plans after the current month in later years.
- The household view of the plan overview still loads its cards per month (there is no household overview endpoint); the start page household block uses the existing household endpoints.
- Earlier years load on demand with "Frühere Monate zeigen" (one request per year); the page opens with one request.
- Pot colours live in `theme.css` (`--pot-needs`, `--pot-wants`, `--pot-savings`, light and dark); the light yellow is slightly darker than the chart yellow so bars reach 3 : 1.

## Approval

Approved by the human on 2026-11-06 (in the lead session, after the check-in).
