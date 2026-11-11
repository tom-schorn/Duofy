# Functional specification: Household plan as the central workplace; print and contract timing

Format: 2

## Summary

The household plan gets the personal plan's toolbar; actions ask for the person first; printing works in both plans; contract changes respect their start month.

## Customer requirements

[customer-requirements.md](customer-requirements.md)

## Components

- [Household](docs/components/household.md)
- [Plan](docs/components/plan.md)
- [Grant](docs/components/grant.md)
- [Commitment](docs/components/commitment.md)

## Solution

One toolbar component for personal and household plan. In the household plan a person picker (only members with the needed grant, plus oneself) precedes the existing dialogs. Ticking/changing other members' entries calls the existing endpoints with that owner. Print: a print layout of the month (browser print in web, share/print in the app) for one person or the household. Find why a future contract change shows in the current month and fix the month logic.

## Behaviour

- R1: the toolbar sits at the same place in both plans.
- R2: a member without Anlegen for Tom does not appear in Tom's picker for that area.
- R3: with Ändern from Jasmin, Tom ticks Jasmin's position in the household plan and the booking belongs to Jasmin.
- R4: print shows the month's positions and bookings; in the household plan after choosing.
- R5: a change from December keeps November's positions unchanged.

## Data and interfaces

New picker step in the household plan; print layout; no new endpoints expected (existing ones with the owner).

External input: yes

## Acceptance criteria

- [ ] A1 (R1): Given the personal and the household plan, when they render, then both show Drucken, Posten hinzufügen and Buchung hinzufügen in the same toolbar component at the same place.
- [ ] A2 (R2): Given Tom in a household with Jasmin who granted Anlegen on Plan but not on Buchungen, when Tom chooses Posten hinzufügen, then Tom and Jasmin are offered; for Buchung hinzufügen only Tom; the created position belongs to the chosen person.
- [ ] A3 (R3): Given Jasmin granted Tom Ändern on Plan, when Tom ticks Jasmin's position in the household plan, then the booking belongs to Jasmin; without the grant the tick is refused with the missing step.
- [ ] A4 (R4): Given the personal plan, when Drucken is pressed, then a print view of the month opens; in the household plan, when Drucken is pressed, then a choice person/household appears and the chosen view opens.

- [ ] A6 (R6): Given the app, when Drucken is chosen in the personal or household plan, then a PDF of the chosen month view is created and handed to the platform share dialog (test of the app's print service with a fake share).

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | `Tests/Duofy.Shared.Tests/HouseholdPlanWorkTests.cs`: `Both_plans_show_print_booking_and_position_in_the_same_toolbar_at_the_same_place` |
| R2 | A2 | `Tests/Duofy.Shared.Tests/HouseholdPlanWorkTests.cs`: `Adding_a_position_asks_for_the_person_first_and_the_position_belongs_to_the_chosen_one`, `Adding_a_booking_offers_only_those_who_granted_creating_bookings`; `Tests/Duofy.Shared.Tests/ActiveMemberActingTests.cs`: `People_with_a_level_are_me_first_and_then_the_members_whose_grant_reaches_it` |
| R3 | A3 | `Tests/Duofy.Shared.Tests/HouseholdPlanWorkTests.cs`: `Ticking_a_position_of_another_member_runs_as_that_member_under_their_grant`, `Ticking_without_the_grant_is_refused_and_says_what_is_missing`; `Tests/Duofy.Shared.Tests/ActiveMemberActingTests.cs`: `What_a_member_may_do_for_another_follows_that_members_grants_also_in_the_household_view` |
| R6 | A6 | pending |
| R4 | A4 | `Tests/Duofy.Shared.Tests/HouseholdPlanWorkTests.cs`: `Print_in_the_personal_plan_prints_the_month_at_once`, `Print_in_the_household_plan_asks_for_one_person_or_the_whole_household_and_prints_the_household`, `Print_for_one_person_prints_that_persons_plan_from_their_grant` |

## Threats

- Can someone pretend to be someone else? No: actions run as the signed-in person; the chosen member is only the owner of the data.
- Can someone change data? Only with the grant of spec 68 for that owner and area, checked by the existing central check.
- Can someone deny an action? Changes are stored with the acting person as today.
- Can someone read what they should not? The picker lists only members with the needed grant; data stays behind the existing checks.
- Can someone bring it to a halt? No new endpoints; existing limits apply.
- Can someone gain rights they should not have? No; grants are unchanged.

## Non-goals

- No change to the grant model (see the separate issue on simplifying grants).
- No new print product; a plain print view only.

## Risks

Acting on another member's data from the household plan must never bypass the grants: contained by A2, A3 and the central check.

## Open questions

None.

## Approval

Approved by the human on 2026-11-11

Amended on 2026-11-11: R6/A6 app print as PDF via the share dialog (human); R5 is wont (human: ending the old contract and starting a new one already covers it; card P-065 rejected).

In the lead session: the human gave the points on 11.11.2026 and ordered "spezifizieren und zur Arbeit schicken".
