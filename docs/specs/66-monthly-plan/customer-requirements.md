# Customer requirements: Monthly plan

Format: 2

## Background

The original Duofy plans every month from the person's commitments: each due commitment becomes a position, bookings fill the positions, and the 50/30/20 quotas show how the month goes. The .NET rebuild has `Plan.CreateFrom` and `PlanPosition` in `Duofy.Domain`, written by the human while learning, and since spec 64 contracts with category, pot, accounts and payment method. On 2026-10-21, 2026-10-23 and 2026-10-24 the human went through the rules one at a time. The team builds the API and the app; the web page is the human's part.

## Goal

A person opens a month in the app and sees what is planned and what has actually happened, can tick positions off, assign bookings to positions and carry what is left into the next month.

## Users

Every Duofy user, daily when booking and once a month when planning the next month.

## Requirements

R1 (must): A month's plan is created automatically the first time it is opened, with one position per contract due in that month, copied from the contract (name, amount, due day, category, pot, accounts, pass-through, payment method) — because planning should need no extra step, and later contract changes must not rewrite existing months.
R2 (must): When a contract is changed, added or ended after a month exists, the month shows a notice with before and after for each difference, and the person takes all of them or one at a time; ticked positions and positions changed by hand in the month are never overwritten silently — because the plan must follow the contracts without losing the month's own corrections.
R3 (must): The person can add one-off positions to a month by hand (name, amount, day, category, pot, optional account); the plus button on the plan page creates one — because not everything is a contract.
R4 (must): Every position shows the planned amount and the amount booked on it; a position is ticked off by hand, never automatically — because a fill level and "done" are different things.
R5 (must): Ticking a position without bookings creates a booking prefilled from the position (a transfer when it has a target account); ticking a position with bookings offers a balancing booking for the difference, where 0 just closes it; a difference between planned and actual may stay; removing the tick removes a booking created by the tick — because ticking is the fastest way to record a planned payment.
R6 (must): A booking line may be assigned to a position of the same person; the booking dialog offers the open positions of the plan month that match the line's kind — because bookings fill positions, and a split booking can fill several.
R7 (must): Every booking has a plan month: with a position it is the position's month; without one, the month of the date is preselected and the previous or next month can be chosen; the balance always follows the real date — because a salary paid on 25 September can be October's money.
R8 (must): Each person has quota targets for Bedürfnisse, Wünsche and Sparen, 50/30/20 by default and changeable in the settings; a new month copies them; the plan shows target and actual per pot, where pass-through positions count in no quota — because the quotas are a guideline and old months must keep the targets they had.
R9 (must): A month offers a button "Übertrag": the person states what is really left, which may also be zero or negative; differences to the computed balances become balancing bookings per account where a real balance is entered, and the carry-over is booked as a pair that cancels out in the balance: an expense "Übertrag nach <Monat>" in the old plan month and an income "Übertrag aus <Monat>" in the new one; the dialog suggests income minus expenses of the previous plan month — because the plan should start from the money that is really there. The free amount of the new month contains the carry-over (amended 2026-11-01).
R10 (must): A position can be deleted only while it has no bookings; the contract runs on — because a booked payment must keep its plan link.
R11 (must): A person sees and changes only their own plans, positions and quota settings — because plans are private.
R12 (wont): Savings goal progress, rate recommendations, the question when a goal is reached and the automatic pause of a rate, the account protection for earmarked savings, the household plan and private positions, the web page — because they follow in their own specification or belong to the household or the human's web part.

## Constraints

- Builds on `Plan.CreateFrom` and `PlanPosition` of the human; their tests stay green, new fields are added.
- Needs migrations (plans, positions, quota columns on users, position link and plan month on bookings); they are applied only after the human's yes.
- The app layout of spec 65 already has the tab Plan with a placeholder; this feature replaces the placeholder.

## Open questions

None.
