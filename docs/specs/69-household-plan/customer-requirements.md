# Customer requirements: Household plan

Format: 2

## Background

The original Duofy (github.com/tom-schorn/Duofy: `backend/app/api/v1/plans.py` with `_household_plan`, `_list_household_plans`, `_unplanned`, `backend/app/core/scope.py`, `backend/app/schemas/plan.py`) shows a household plan that is not stored but composed from the plans of the current members, with the household's own quotas. On 2026-10-24 the human decided to rebuild the rest of the original as it is, without the statement import, and to ask only real questions. Asked about privacy, the human chose on 2026-10-24 that every member sees every position of the others in the household plan for now; private positions stay in V2 (#41). This builds on the household (#67), the grants (#68) and the monthly plan (#66). The team builds the API and the app; the web page is the human's part.

## Goal

The members of a household see in one place what the household plans in a month, who carries which position, how far it has happened and how the household's quotas look.

## Users

Members of a household with more than one person, mostly when planning the next month together.

## Requirements

R1 (must): The household plan of a month is composed on every reading from the plans of the members who belonged to the household by the last day of that month; it is never stored — because the plan always belongs to one person, and the household only says who plans together.
R2 (must): Opening the household plan of a month creates the months of those members that do not exist yet, the same way as opening one's own month — because a household plan with somebody missing would be half a plan.
R3 (must): Every member sees every position of every counted member, with the first name of the person who carries it, planned and booked amount and whether it is ticked, independent of grants — because "who carries what" is the question a shared plan answers; private positions follow in V2.
R4 (must): The household plan shows income, the planned sums per pot, what is still to go out, the bookings of the plan month without a position per pot, and target and actual of the household's quotas, with pass-through positions and the carry-over counting in no quota and no income — because the household needs the same figures as a person, and the same rules.
R5 (must): Whoever leaves or is removed disappears from the household plan in every month, also in past ones; whoever joins counts from the month they joined in — because the shared view is made of the current members only.
R6 (must): The household plan is read-only; changing, ticking or booking on another member's position goes through that member's own plan and needs the grants of #68 — because the grants are the one place that decides what somebody may change.
R7 (must): A list of household months shows the months, newest first, for which a household plan exists — because the members want to look back.
R8 (must): The household's account balances are shown only for members who granted the viewer at least Ansehen on their accounts, and the page says by name whose accounts are missing — because accounts stay personal, and a total silently missing a person is worse than none.
R9 (must): In the app, the switch of #68 offers "Haushalt" while the household has more than one member; choosing it makes the plan page show the household plan — because the household view must be reachable the same way as another person's view.
R10 (wont): Private positions (#41), the curve over all accounts of the members (#32), hints derived from the plan, the web page — because they are in V2 or the human's part.

## Constraints

- Builds on #66 (`Plan`, positions, figures, carry-over), #67 (households, household quotas) and #68 (grants, switch, central check).
- No new table and no migration: the household plan is read from existing data.

## Open questions

None.
