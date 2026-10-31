# Functional specification: Household bookings only with grant

Format: 2

## Summary

The household month shows the recent bookings of a member only if the member granted the area Buchungen (or is the viewer). The server filters; the page lists the shown bookings and names the members whose bookings are "nicht freigegeben", on the overview as well as on the tab Buchungen.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R4.

## Components

- [Grant](docs/components/grant.md)
- [Household](docs/components/household.md)
- [Booking](docs/components/booking.md)

## Solution

- **API**: already in place on the base branch (`HouseholdPlanStore.ComposeAsync`): `bookings` holds the bookings of counted members whose `book` level for the viewer includes `view` (the viewer's own always); `bookingsMissing` holds the first names of the others, sorted. This specification fixes the rule with tests, including that nothing else in the response carries the hidden bookings.
- **Client and pages**: the overview section "Letzte Buchungen" (`MonthOverview`) gets the same notice as the tab Buchungen: "Nicht freigegebene Buchungen von: <Namen>", also when no booking is shown at all; the empty text does not claim that the month has no bookings while names are missing. The notice appears only in the household view.
- **Security check**: the household plan answers are searched for the ids of hidden bookings; a viewer without grant gets none of them.

## Behaviour

- R1: A viewer sees their own bookings and those of members with `book` at least `view` for them; the grant is per direction (Ben granting Anna does not let Ben see Anna); revoking removes the bookings from the next answer.
- R2: `bookingsMissing` lists each hidden member once with the first name; the page shows "Nicht freigegebene Buchungen von: Ben, Clara" on the overview and on the tab; with no names missing there is no notice.
- R3: The two household plan endpoints are the only household paths that carry individual bookings; the serialized answers hold no id of a hidden booking. Positions carry the sum `booked` only.
- R4: Figures (`income`, `pots`, `quotas`, `unplanned`, `fixed`, `free`, position `booked`) are computed from all counted members as before.

## Data and interfaces

None. `HouseholdPlanResponse.bookings` and `bookingsMissing` exist already.

External input: yes

## Acceptance criteria

- [x] A1 (R1): Given Anna and Ben in one household with a booking each and no grants, when Anna opens the household plan, then `bookings` holds only Anna's booking and `bookingsMissing` is ["Ben"].
- [x] A2 (R1): Given Ben granted Anna `book` = view and Anna granted Ben nothing, when Anna opens the household plan, then both bookings are listed and `bookingsMissing` is empty; when Ben opens it, then he sees only his own booking and `bookingsMissing` is ["Anna"].
- [x] A3 (R1): Given Ben granted Anna `book` = view and then set it to `none`, when Anna opens the household plan again, then Ben's booking is gone and Ben is named in `bookingsMissing`.
- [x] A4 (R1): Given Ben granted Anna only `accounts` and `plan` but not `book`, when Anna opens the household plan, then Ben's bookings stay hidden.
- [x] A5 (R3): Given Ben has a booking and no grant, when Anna fetches the household plan and the list of months as text, then neither answer contains the id of Ben's booking.
- [x] A6 (R4): Given the same data with and without the grant, when Anna fetches the household plan, then income, pots, quotas, unplanned, fixed, free and the `booked` of every position are equal.
- [x] A7 (R2): Given a household plan with bookings of Anna and `bookingsMissing` ["Ben"], when the overview of the household month is shown, then "Letzte Buchungen" lists Anna's bookings and the text "Nicht freigegebene Buchungen von: Ben".
- [x] A8 (R2): Given a household plan without any visible booking and `bookingsMissing` ["Ben"], when the overview is shown, then it shows the notice naming Ben and not the text that the month has no bookings.
- [x] A9 (R2): Given a household plan with `bookingsMissing` empty, when the overview is shown, then no notice appears; given the overview of a single person, then no notice appears.
- [x] A10 (R3): Given Clara of another household, when she requests the household plan, then the answer is 404 and carries no booking.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1, A2, A3, A4 | `Tests/Duofy.Backend.Tests/HouseholdPlanTests.cs`: `Single_bookings_show_only_for_members_who_granted_them_and_the_others_are_named`, `The_booking_grant_counts_in_one_direction_and_the_own_bookings_always_show`, `Taking_the_booking_grant_back_hides_the_bookings_again`, `Grants_of_other_areas_do_not_show_the_bookings` |
| R2 | A7, A8, A9 | `Tests/Duofy.Shared.Tests/MonthPageTests.cs`: `The_household_overview_names_members_whose_recent_bookings_are_not_released`, `The_household_overview_does_not_claim_an_empty_month_while_bookings_are_not_released`, `Without_missing_names_and_for_a_single_person_the_overview_has_no_notice`, `The_household_bookings_tab_names_members_who_did_not_grant_their_bookings` |
| R3 | A5, A10 | `Tests/Duofy.Backend.Tests/HouseholdPlanTests.cs`: `No_answer_of_the_household_carries_the_id_of_a_hidden_booking`, `Someone_outside_the_household_gets_household_not_found` |
| R4 | A6 | `Tests/Duofy.Backend.Tests/HouseholdPlanTests.cs`: `The_figures_are_the_same_with_and_without_the_booking_grant` |

## Threats

- Can someone pretend to be someone else? Only with a stolen access token; the viewer comes from the token and the grant is looked up for that person.
- Can someone change data? No: the household plan has no write path and this change adds none.
- Can someone deny an action? Not applicable; nothing is written by this change.
- Can someone read what they should not? That is the point of this change: bookings of a member without grant stay on the server (A1 to A5); the sums remain visible by the human's decision.
- Can someone bring it to a halt? The grant is looked up once per member and month; no new load beyond that.
- Can someone gain rights they should not have? No: the check is the central one (`IAccess`), a grant counts only inside a shared household and only in its direction (A2).

## Non-goals

- Hiding sums, quotas or planned versus booked amounts (they stay as decided in the household plan specification).
- Private positions (#41).
- A way for a viewer to ask for a grant from the page.
- Changing the grant levels or the grant dialog.

## Risks

A future endpoint could add bookings to a household answer unfiltered; A5 searches the household answers for hidden ids and fails then.

## Open questions

None.

## Approval

Decision 1 of the check-in of 2026-10-30 (Tom). Approved by Tom in the wrap-up of 2026-10-30 (acceptance of the issue and release of the specification).
