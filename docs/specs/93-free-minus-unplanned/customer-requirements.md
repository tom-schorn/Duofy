# Customer requirements: "Verplanbar" subtracts booked expenses without a position

Format: 2

## Background

"Verplanbar" (Free) is computed as planned income minus planned expenses plus the carry-over (`Backend/Duofy.Backend/Domain/PlanFigures.Free`, help text Help_month_2_Body). Expenses booked without a plan position — groceries, tobacco, cash, online shopping, which many people book only with a category — do not lower it. On 04.11.2026 Tom's November showed about 523 to 678 euros "verplanbar" while only about 70 euros were on his accounts.

The human, 04.11.2026: "haut net hin wie kann ich verplanbar 600 haben wenn kein geld da is" — and on the choice: "Verplanbar zieht sie ab". And: "den fix will ich heute noch live haben".

## Goal

"Verplanbar" tells how much of the month's plan is still free to spend: it falls with every booked expense that has no position.

## Users

Everyone, daily, on the start page, the month page, the plan overview and the household plan.

## Requirements

R1 (must): Verplanbar = planned income minus planned expenses plus carry-over minus booked expenses of the plan month that belong to no position — because the human chose "Verplanbar zieht sie ab".
R2 (must): The same figure is shown everywhere "Verplanbar" appears (start page, month page, plan overview list, household plan, print) — because one name must mean one number.
R3 (must): Expense lines on a position, transfers between own accounts, and pass-through positions do not count twice or at all as before — because the plan already counts positions.
R4 (should): Booked income of the plan month without a position (for example interest or a back payment) raises Verplanbar — because it is money the plan did not know about (lead's assumption for symmetry; the human can object at approval).
R7 (must): Every position counts with its planned amount, or with its booked amount when more was booked than planned; this holds for expense and income positions, open or ticked — because the human said: "entweder den Planposten nehmen oder wenn der ist höher ist den ist wert" (a budget like Lebensmittel is ticked early and still spent later, so a lower booked amount must not free money).
R5 (must): The help text explains the new rule in one sentence — because the old text describes the old rule.
R6 (must): It goes live on Plagevo today — because the human asked for it.

## Constraints

- One booking can have several lines; only lines without a position count for R1 and R4.
- No database change.

## Open questions

None.
