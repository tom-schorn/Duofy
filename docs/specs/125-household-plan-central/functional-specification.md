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
- [ ] A5 (R5): Given a contract whose amount changes from next month, when the current month's plan is read, then its position shows the old amount and the next month the new one.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | pending |
| R2 | A2 | pending |
| R3 | A3 | pending |
| R4 | A4 | pending |
| R5 | A5 | pending |

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

In the lead session: the human gave the points on 11.11.2026 and ordered "spezifizieren und zur Arbeit schicken".
