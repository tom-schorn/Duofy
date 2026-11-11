# Functional specification: Person switch: act in the other person's name with the same views

Format: 2

## Summary

The profile menu loses 'Haushalt'; the person switch is a state every page listens to; the start page and evaluations render the same components for the chosen person.

## Customer requirements

[customer-requirements.md](customer-requirements.md)

## Components

- [Grant](docs/components/grant.md)
- [Household](docs/components/household.md)
- [User account](docs/components/user-account.md)

## Solution

Make the chosen person a shared state (one service) that every page and component reads and re-renders on change. Remove 'Haushalt' from the menu. The start page renders its block list independent of who is chosen; each block takes the person id. Check every evaluation endpoint call passes the chosen person's id.

## Behaviour

- R1: menu shows Ich, Jasmin.
- R2: on the plan page, choosing Jasmin shows Jasmin's plan at once.
- R3: 'Hallo Jasmin' and five blocks; a block without grant shows a hint.
- R4: the evaluation for Jasmin shows Jasmin's totals, which differ from Tom's in the test data.

## Data and interfaces

None.

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given Tom with a household and Jasmin's grants, when the profile menu opens, then it lists Ich and Jasmin and no entry Haushalt.
- [ ] A2 (R2): Given Tom on the plan, bookings, accounts or contracts page, when he chooses Jasmin, then the page shows Jasmin's data without navigation, and the choice stays when changing pages.
- [ ] A3 (R3): Given Jasmin granted Tom all areas, when Tom chooses Jasmin on the start page, then it greets 'Hallo Jasmin' and shows the same blocks as Tom's own start page, filled with Jasmin's data.
- [ ] A4 (R4): Given test data where Tom's and Jasmin's totals differ, when the evaluations are shown for Jasmin, then they show Jasmin's totals and the same components render as for Tom.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | pending |
| R2 | A2 | pending |
| R3 | A3 | pending |
| R4 | A4 | pending |

## Non-goals

- No change to grants.
- No new blocks.

## Risks

Showing another person's data in more places: contained by using the existing grant checks for every block.

## Open questions

None.

## Approval

Approved by the human on 2026-11-11

In the lead session: the human gave the points on 11.11.2026 and ordered "spezifizieren und zur Arbeit schicken".
