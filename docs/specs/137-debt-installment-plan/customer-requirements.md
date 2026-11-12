# Customer requirements: Debts and loans with an installment plan

Format: 2

## Background

The human owes Jasmin's insolvency administrator 786,94 € ("Ausgleich Zusammenveranlagung"), paid as 15 × 50 € plus a last installment of 36,94 €, monthly from 01.04.2026 by direct debit (Duofy#137, 12.11.2026). Duofy cannot record this cleanly: a contract of the kind "debt" sits fixed in the pot Sparen but may not be linked to the debt (`commitment_debt_invalid`), a normal contract may be linked but never sits in Sparen; a different last installment needs a second contract; the debt itself has only name, creditor, amount, due date and a 200-character note. The old contract was ended on 01.11.2026; from December there is none.

## Goal

A debt or loan is managed as one thing: its installment plan lives in the debt, creates the monthly positions in Sparen linked to it, and the debt shows what is paid, what is open, the next installment and the expected end (human, 12.11.2026: "gilt auch für Kredite, so kann man zukünftig so Sachen besser verwalten").

## Users

Every Duofy user with debts or loans; household members with grants on debts.

## Requirements

R1 (must): A debt or loan can have an installment plan: installment amount, rhythm, first due date, account and payment method; the number of installments and a smaller last installment follow from the open amount, and the last installment can be set by hand — because 15 × 50 € plus 36,94 € must not need two contracts (human, 12.11.2026).
R2 (must): The plan creates the monthly positions in the pot Sparen, linked to the debt, through a contract that belongs to the debt; ticking an installment pays the debt down — because the debt list must count down by itself.
R3 (must): A contract of the kind debt can be linked to a debt, and a debt can be linked to an existing contract instead of its own plan — because both ways must work and the pot and the link must not exclude each other.
R4 (must): Each debt shows paid, open, the next installment (date and amount) and the expected end — because the human wants the overview per debt.
R5 (must): A debt gets a reference number (Aktenzeichen) and a longer note (up to 1000 characters) — because the creditor's file number belongs to the debt.
R6 (must): Changing the plan never changes past months: from the chosen month on, the old plan contract ends and a new one starts — because past months must stay as they were (human decision 11.11.2026 for contracts).
R7 (must): The contract of a debt appears in the contract list as "gehört zu <Schuld>" and is changed only through the debt — because two places to edit one plan would drift apart.
R8 (must): The Plagevo MCP covers it: debt tools carry the plan, concept_and_guide explains the contract kinds, their fixed pots and the way for debts, and the errors commitment_pot_invalid and commitment_debt_invalid come with an explaining text — because the assistant failed on exactly these (Duofy#137).

## Constraints

Repository: Duofy for app and backend (R1-R7); Plagevo for the MCP part (R8) after Duofy main is merged into plagevo. Human, 12.11.2026: a new feature built alongside, also for loans; the installment counts fully against the open amount (no interest calculation; the open amount can be corrected by hand); only money the person owes (receivables are a later feature); no documents or history now (later feature); the human waits for this feature instead of a bridging contract.
A database migration is needed; it is merged only with the human's yes. Grants on debts as today (area Schulden).

## Open questions

None.
