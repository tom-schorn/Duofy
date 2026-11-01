# Functional specification: Start page with blocks, greeting and quick facts

Format: 2

## Summary

The start page replaces its placeholder text by the blocks of issues #33 and #34: greeting with the free amount of the month, the way to a new booking, the hint to plan together, the coming payments and the compact target-actual bars. On the desk the blocks stand in several columns, on the phone in one.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R8. R9 is out of scope.

## Components

- [Plan](docs/components/plan.md)
- [Household](docs/components/household.md)

## Solution

- **Data**: no new field. The page asks the plan API for the list of months and, only when the current month is in it, for that month (it never creates a month by looking at it). For the household view it uses the list of household plans and the household plan. `MonthView` turns both answers into the same model as the month overview; the page calculates nothing.
- **Blocks**: `StartHero` (greeting and free amount in a colour block), `StartSection` (a titled block whose title is a link to its page), and the existing `TargetActualBar` and `LineTable`.
- **Layout**: a grid of one column below 1280 px and two columns from 1280 px; the hero block spans both. Changed on 2026-11-01 from 900 px, because all pages share the three width ranges of spec 9 (docs/layout.md).
- **Grants**: the plan needs the plan grant (view); the coming payments and the carry-over need the bookings grant (view), else the text "Diese Person teilt diesen Bereich nicht mit dir." replaces them.

## Behaviour

- R1: The hero block shows "Hallo, {Vorname}!", the title "Frei im {Monat}" and the free amount of the current month; with a carry-over in it the note "Enthält Übertrag: …" stands under the amount, as in the month overview. A free amount of 0 reads "Alles verplant".
- R2: The button "Neue Buchung" opens the booking dialog for the person whose numbers are open; it is disabled with the reason of the grant when the person may not book. In the household view it does not appear.
- R3: Without a household a block "Mit jemandem zusammen planen?" with the link "Haushalt gründen oder beitreten" to `/haushalt` is shown; with a household it is not.
- R4: Without a plan for the current month the page shows the greeting and a block "Für {Monat} gibt es noch keinen Plan" with the link "Plan für {Monat} anlegen" to `/plan/{Jahr}/{Monat}`; no figures are shown. For another member or the household the text names whose plan is missing and has no link.
- R5: "Anstehend und überfällig" lists the rows of `MonthView.Due` (overdue first, then the next 7 days) with the badge "überfällig" or "anstehend"; without any the text "In den nächsten Tagen ist nichts fällig." stands there. At most five rows show; the title links to the plan month.
- R6: "Soll und Ist" shows one `TargetActualBar` per pot; the title links to the plan month.
- R7: The hero block, "Anstehend und überfällig" and "Soll und Ist" link to `/plan/{Jahr}/{Monat}`, the hint to `/haushalt`. The link text is a real link, reachable by keyboard.
- R8: With another member chosen the figures are theirs; without the plan grant the text "Diese Person teilt diesen Bereich nicht mit dir." stands in place of the blocks; in the household view the figures are the household sum and the carry-over note names who is missing.

## Data and interfaces

None. Existing endpoints only: the list of plan months and the plan of a month for a person, the list of household plans and the household plan of a month.

External input: no

## Acceptance criteria

- [x] A1 (R1): Given Anna with a plan for October 2026 with a free amount of 340.00, when the start page loads on 21 October 2026, then it shows "Hallo, Anna!", "Frei im Oktober" and 340,00 €.
- [x] A2 (R1): Given a carry-over of 50.00 into the month, when the start page loads, then the note "Enthält Übertrag: 50,00 €" stands in the hero block.
- [x] A3 (R2): Given a person who may book, when "Neue Buchung" is pressed, then the booking dialog opens; given a person who may not, then the button is disabled.
- [x] A4 (R3): Given Anna in no household, when the page loads, then the hint links to `/haushalt`; given Anna in a household, then the hint is not there.
- [x] A5 (R4): Given no plan for October 2026, when the page loads, then the empty state with the link to `/plan/2026/10` is shown, no plan is created (no call of the month endpoint) and no figures are shown.
- [x] A6 (R5): Given a position of 80.00 due on 23 October and one of 30.00 due on 15 October, when the page loads, then both rows show, the overdue one first with the badge "überfällig" and the other with "anstehend"; given none, the empty text shows.
- [x] A7 (R6): Given quotas of 50, 30 and 20, when the page loads, then three bars show with their pot names.
- [x] A8 (R7): Given a plan, when the page loads, then the blocks of A1, A6 and A7 link to `/plan/2026/10`.
- [x] A9 (R8): Given Ben, who shares his plan but not his bookings, chosen, when the page loads, then the figures are asked for Ben, and the coming payments are replaced by "Diese Person teilt diesen Bereich nicht mit dir.".
- [x] A10 (R8): Given Ben chosen who shares nothing, when the page loads, then no plan is asked for and "Diese Person teilt diesen Bereich nicht mit dir." stands in place of the blocks.
- [x] A11 (R8): Given the household view with two members, when the page loads, then the household plan is shown and no button for a new booking.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1, A2 | `Tests/Duofy.Shared.Tests/StartPageTests.cs`: `The_hero_block_greets_by_name_and_shows_the_free_amount_of_the_current_month`, `A_free_amount_of_zero_reads_all_planned`, `The_hero_block_names_a_carry_over_that_is_in_the_free_amount`, `Without_a_carry_over_the_hero_block_has_no_note` |
| R2 | A3 | `Tests/Duofy.Shared.Tests/StartPageTests.cs`: `New_booking_opens_the_booking_dialog`, `New_booking_is_locked_when_the_person_may_not_book` |
| R3 | A4 | `Tests/Duofy.Shared.Tests/StartPageTests.cs`: `Without_a_household_a_hint_leads_to_the_household_page`, `With_a_household_there_is_no_hint` |
| R4 | A5 | `Tests/Duofy.Shared.Tests/StartPageTests.cs`: `Without_a_plan_for_the_month_the_empty_state_leads_to_the_plan_and_creates_nothing` |
| R5 | A6 | `Tests/Duofy.Shared.Tests/StartPageTests.cs`: `Coming_and_overdue_payments_are_listed_overdue_first_with_their_badge`, `Without_anything_due_the_block_says_so`, `At_most_five_payments_are_shown` |
| R6 | A7 | `Tests/Duofy.Shared.Tests/StartPageTests.cs`: `The_target_actual_bars_show_one_bar_per_pot` |
| R7 | A8 | `Tests/Duofy.Shared.Tests/StartPageTests.cs`: `The_blocks_open_the_month_of_the_plan` |
| R8 | A9, A10, A11 | `Tests/Duofy.Shared.Tests/StartPageTests.cs`: `Another_member_who_shares_the_plan_but_not_the_bookings_shows_their_figures_without_payments`, `Another_member_who_does_not_share_the_plan_shows_the_note_instead_of_the_blocks`, `The_household_view_shows_the_household_plan_and_offers_no_booking`, `Choosing_another_member_reloads_the_figures` |

## Non-goals

- The hint about a deviation from the guideline value (R9; waits for P6).
- Menu, bottom bar and width of the other pages (another task).
- New endpoints, new fields or a migration.
- A year view or any new calculation.

## Risks

The page asks up to three endpoints on opening; each answers from rows that are already loaded for the month page, so no new load is expected. The look in light and dark, phone and desk, has to be seen in the browser by a human.

## Open questions

None.

## Approval

Pending: to be filled in by the lead after the human has approved.
