# Customer requirements: Contracts

Format: 2

## Background

The original Duofy has commitments (contracts, income, savings goals, debts) from which each month's plan is generated. The .NET rebuild already has a domain class `Commitment` with type, name, amount, interval, first and last due date and the rules `IsDueIn`, `EffectiveDueDay` and `RunsOn`, written by the human while learning, and a table `commitments`. On 2026-10-21 and 2026-10-23 the human went through the original's rules one at a time. The team builds the API and the Android app; the human builds the web page later.

## Goal

A person can record their recurring payments and income as contracts in the app, change them and end them, so that the monthly plan can later be generated from them.

## Users

Every Duofy user, when starting with Duofy and whenever a contract, a salary, a savings rate or a loan starts, changes or ends.

## Requirements

R1 (must): A contract has one of four types (Vertrag, Einnahme, Sparziel, Schuld), a name, an amount per payment, an interval in months, the date of the first payment and optionally the date of the last payment — because these decide in which months it falls due.
R2 (must): A contract has a category and a pot like a booking, including own categories; a savings goal and a debt always use the pot Sparen, an income always the pot Einnahmen — because the plan sorts positions by pot and category.
R3 (must): A contract may name the account it is paid from; empty means the default account — because some contracts run over another account, for example a card.
R4 (must): A contract may name a target account of the same person; then each payment is a transfer between own accounts — because savings rates and money moved between several accounts never leave the household.
R5 (must): A contract can be marked as pass-through: it moves the account balance but counts neither in the budget nor in the quotas — because money only passed on would otherwise look like money spent or saved.
R6 (must): Every contract has a payment method: Lastschrift, Dauerauftrag, Überweisung, Abhebung or Sonstiges — because the plan will remind the person of payments they must start themselves.
R7 (should): A savings goal may carry a target amount and a target date, both optional — because later the app will recommend a higher rate and ask what happens once the goal is reached.
R8 (must): Contracts are never deleted, only ended by their last payment date; a button "Beenden" asks once and then sets the last payment date to the latest payment on or before today; a contract whose last payment date lies before its first payment counts as ended at once — because ending is the normal way out and old months must stay explainable.
R9 (must): A person sees and changes only their own contracts — because contracts are private.
R10 (must): The Android app has a page "Verträge" listing the running contracts, with ended ones on request, and a dialog to create and change one — because the app is where the person records them first.
R11 (wont): The monthly plan, positions, ticking off, savings progress, rate recommendations, the reached-goal question, the account protection for earmarked savings, household privacy and the web page — because they belong to the plan feature or to the human's web part.

## Constraints

- Builds on the existing `Duofy.Domain/Commitment.cs` and the table `commitments`; its rules `IsDueIn`, `EffectiveDueDay` and `RunsOn` stay as the human wrote them.
- Category and pot follow the bookings (spec 59), accounts follow spec 58.
- A migration changes the table `commitments`; it is applied only after the human's yes.

## Open questions

None.
