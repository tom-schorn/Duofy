# Functional specification: Transfers move money between pots

Format: 2

## Summary

A transfer whose source and target pot differ now moves its amount between the pots: in the plan month, the booked sum and the quota actual of the source pot go down and those of the target pot go up. Same-pot transfers and older transfers without pots change nothing. The free amount, the fixed sum and the balances are not touched.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R5; R6 is a non-goal.

## Components

- [Booking](docs/components/booking.md)
- [Plan](docs/components/plan.md)

## Solution

The figures of a plan month (PlanFigures, HouseholdPlanFigures) add a pot shift for every transfer of the plan month that has a source pot and a target pot that differ: minus the amount at the source pot, plus the amount at the target pot. The shift is applied to the booked sum per pot and to the actual of the quotas. The component sheets booking.md (rule "The pots are only remembered") and plan.md (booked per pot and quota actual "without transfers") are updated to the new rule, and the help text that explains the pots says that a transfer moves money between pots.

## Behaviour

- R1: transfer of 200 € from needs to savings in November: booked needs −200 €, booked savings +200 € in November; nothing in other months.
- R2: the same transfer: quota actual of needs −200 €, of savings +200 €; the share of income shown at each quota follows.
- R3: transfer from savings to savings, or a transfer stored before spec 72 without pots: all pot sums and quota actuals unchanged.
- R1 edge cases: changing the amount, the date (month) or a pot of a transfer, or deleting it, changes the shift accordingly; a source pot may become negative and is shown as negative.
- R4: the household month adds the shifts of the counted members; a member whose bookings the viewer may not see is treated as for all other bookings of that member.
- R5: free amount, fixed sum and account balances are the same before and after the change.

## Data and interfaces

No new data: the existing source pot (the pot of the transfer's line) and `target_pot` are used. The plan endpoints `/api/v1/plans/{year}/{month}` and the household plan return the changed figures in the existing fields.

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given a plan month with no bookings and a transfer of 200 € from needs to savings dated in that month, when the plan month is read, then booked needs is −200 € and booked savings is +200 €.
- [ ] A2 (R2): Given the same transfer, when the quotas of that month are read, then the actual of needs is 200 € lower and the actual of savings 200 € higher than without the transfer.
- [ ] A3 (R3): Given a transfer from savings to savings, and a transfer without pots stored as before spec 72, when the plan month is read, then booked sums and quota actuals equal those of the month without these transfers.
- [ ] A4 (R1): Given the transfer of A1, when its amount is changed to 50 €, then needs is −50 € and savings +50 €; when it is moved to the next month, then the first month shows no shift and the next month shows it; when it is deleted, no month shows it.
- [ ] A5 (R4): Given a household of two members who see each other's bookings and each books a transfer of 100 € from wants to savings, when the household month is read, then booked wants is 200 € lower and booked savings 200 € higher than without the transfers.
- [ ] A6 (R5): Given the transfer of A1, when the plan month and the accounts are read, then the free amount, the fixed sum and both account balances equal those of the same month with the transfer under spec 72 rules.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1, A4 | Tests/Duofy.Backend.Tests/Domain/PlanPotShiftTests.cs: A_transfer_from_needs_to_savings_lowers_booked_needs_and_raises_booked_savings, The_shift_follows_the_amount_and_only_the_bookings_of_the_month |
| R2 | A2 | Tests/Duofy.Backend.Tests/Domain/PlanPotShiftTests.cs: The_same_transfer_moves_the_quota_actual |
| R3 | A3 | Tests/Duofy.Backend.Tests/Domain/PlanPotShiftTests.cs: A_transfer_within_one_pot_and_an_older_transfer_without_pots_change_nothing |
| R4 | A5 | Tests/Duofy.Backend.Tests/Domain/PlanPotShiftTests.cs: The_household_adds_the_shifts_of_its_members |
| R5 | A6 | Tests/Duofy.Backend.Tests/Domain/PlanPotShiftTests.cs: Free_and_fixed_do_not_depend_on_the_shift |

## Non-goals

- No change to the booking dialog or to the requirement of both pots for new transfers.
- No change to how ticking a position with a target account books its transfer.
- No migration that fills the pots of older transfers.
- No change to the free amount (Verplanbar), the fixed sum or the balances.
- No new pot, no new chart.

## Risks

- The free amount must stay untouched while pot sums change; contained by A6.
- Household sums and visibility: contained by A5 and by reusing the existing visibility rules.

## Open questions

None.

## Approval

Approved by the human on 2026-11-10

In the lead session, outside a meeting, after the late kickoff.
