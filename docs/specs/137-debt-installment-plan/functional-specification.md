# Functional specification: Debts and loans with an installment plan

Format: 2

## Summary

A debt or loan can carry an installment plan. Duofy turns it into a contract of the kind debt that belongs to the debt, sits in Sparen and is linked to it, with a smaller last installment where the amount does not divide evenly. Ticking an installment pays the debt down. The debt shows paid, open, next installment and expected end, and gets an Aktenzeichen. The Plagevo MCP learns the same.

## Customer requirements

[customer-requirements.md](customer-requirements.md): R1, R2, R3, R4, R5, R6, R7, R8.

## Components

- [Commitment](docs/components/commitment.md)
- [Plan](docs/components/plan.md)

## Solution

- Debt (R5): new fields `reference` (Aktenzeichen, up to 50 characters) and `note` extended to 1000 characters; `adjusted_open` (optional manual open amount with date, R1 constraint "correct by hand").
- Contract (R1, R3): new field `last_amount` (optional; the amount of the last due date when it differs). The validator allows a `debt_id` on contracts of the kind debt (pot fixed to Sparen) as well as on normal contracts; `contract_id` of a plan contract is stored on the debt as `plan_commitment_id`.
- Plan (R1, R2, R6, R7): the debt dialog gets a step "Ratenplan" (step dialog): installment amount, rhythm (months), first due date, account, payment method, category (default the debt category), optional last installment. Duofy computes the number of installments from the open amount at the first due date: n = ceil(open / rate), last = open − (n−1) × rate unless set by hand, last due date = first + (n−1) × rhythm. It creates the contract (kind debt, Sparen, `debt_id`, `last_amount`, `last_due_date`, name = debt name). Changing the plan asks "ab welchem Monat": the old contract ends before that month, a new one starts (contract rule), past months untouched. Removing the plan ends the contract at the end of the current month. Such contracts show "gehört zu <Schuld>" in the contract list and open the debt for changes.
- Positions (R2): positions created from a contract with `debt_id` carry the link; a booking line created by ticking such a position gets `debt_id`, so paid = sum of those lines as today.
- Overview (R4): debt list and detail show paid, open (or the manual open with its date), next installment (date, amount, from the plan contract), expected end (last due date), and "Ratenplan" or "verknüpfter Vertrag".
- Existing data: existing debts and contracts stay unchanged; the migration adds the columns only.
- MCP (R8, Plagevo, after merging Duofy main): debt tools accept and return the plan fields and reference; concept_and_guide gets "Vertragsarten und feste Töpfe" and "Schulden und Kredite mit Ratenplan"; the REST error bodies for commitment_pot_invalid and commitment_debt_invalid carry a German explanation that the MCP passes on.

## Behaviour

- R1: debt 786,94 € with 400 € paid (April to November), rate 50 €, monthly from 01.12.2026 → open 386,94 € gives 8 installments: 7 × 50 € and a last one of 36,94 € on 01.07.2027 (the numbers follow from the open amount at the first due date); with the last installment set by hand, that value is used and the count follows from it.
- R2: December shows a position "Ausgleich Zusammenveranlagung" in Sparen; ticking it books 50 € with the debt link, the debt's open amount drops by 50 €.
- R3: a contract of the kind debt with a debt link saves without error; linking an existing contract to a debt works from the debt.
- R4: the debt shows bezahlt, offen, nächste Rate 01.01.2027 50,00 €, voraussichtliches Ende 01.07.2027.
- R5: Aktenzeichen saved and shown; a note of 1000 characters saved.
- R6: changing the rate from March keeps December to February as they were.
- R7: the contract list shows "gehört zu Ausgleich Zusammenveranlagung"; its edit opens the debt.
- R8: the MCP returns plan fields and explaining errors.
- Grants: a household member with debts edit for the owner can create and change the plan for the owner; without it, refused like other debt changes.

## Data and interfaces

Migration: `debts.reference`, `debts.plan_commitment_id` (FK, set null), `debts.adjusted_open`, `debts.adjusted_open_on`, `debts.note` length 1000, `commitments.last_amount`. API: debt create/update accept `reference`, `plan` (rate, intervalMonths, firstDueDate, accountId, paymentMethod, categoryId, lastAmount?, fromMonth for changes); debt responses add `paid`, `open`, `nextInstallment`, `expectedEnd`, `plan`. Contract responses add `lastAmount` and `debt` (id, name) for plan contracts. Error bodies for commitment_pot_invalid and commitment_debt_invalid get German messages.

External input: yes

## Acceptance criteria

- [x] A1 (R1): Given a debt with open 386,94 € and a plan of 50 € monthly from 01.12.2026, when the plan is saved, then a contract of the kind debt in Sparen with debt link, 8 due dates, last amount 36,94 € on 01.07.2027 exists; with a last installment set by hand the count and dates follow from it.
- [x] A2 (R2): Given that plan, when December is opened and its position ticked, then the booking line carries the debt link and the debt's open amount is 50 € lower.
- [x] A3 (R1): Given a contract with last_amount, when its last due month is opened, then the position has the last amount; earlier months have the normal amount.
- [x] A4 (R3): Given a contract of the kind debt, when it is saved with a debt link, then it is accepted; given an existing contract, when it is linked from the debt, then the debt shows it as its plan.
- [x] A5 (R4): Given a debt with plan and one paid installment, when it is read, then paid, open, next installment and expected end are correct; with a manual open amount, that amount and its date are shown.
- [x] A6 (R5): Given a debt, when Aktenzeichen and a 1000-character note are saved, then both are returned; 1001 characters are refused.
- [x] A7 (R6): Given a plan running since December, when the rate is changed from March, then December to February keep their positions and amounts and the old contract ends before March.
- [x] A8 (R7): Given a plan contract, when the contract list is shown, then it says "gehört zu <Schuld>" and editing leads to the debt; a direct change through the contract endpoint is refused with a German message.
- [ ] A9 (R1, R4): Given the debt dialog at 412, 800 and 1024 px, when a plan is created and the debt is viewed, then the step "Ratenplan" and the overview fit and work in the personal and the household view.
- [ ] A10 (R8): Given the Plagevo MCP, when debt tools are called with a plan and concept_and_guide is read, then plan fields work and the guide explains contract kinds, fixed pots and debts; the two error codes come with their explanation.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | `DebtPlanTests.The_real_example_creates_one_debt_contract_in_savings_with_a_last_installment_of_36_94`, `DebtPlanTests.A_last_installment_set_by_hand_is_used_and_the_dates_follow_from_it`, `DebtPlanTests.More_than_600_installments_are_refused`, `InstallmentPlanTests` |
| R1 | A3 | `DebtPlanTests.The_last_month_has_the_last_amount_and_earlier_months_the_normal_amount`, `InstallmentPlanTests.The_amount_in_a_month_is_the_last_amount_only_in_the_last_due_month` |
| R1 | A9 | `SavingsPageTests.The_plan_step_shows_the_preview_and_sends_the_plan_with_the_debt`, `DebtPlanDraftTests`; widths 412/800/1024 px via the shared step dialog, browser check by the reviewer |
| R2 | A2 | `DebtPlanTests.Ticking_the_december_position_books_50_with_the_debt_link_and_lowers_the_open_amount` |
| R3 | A4 | `DebtPlanTests.A_contract_of_the_kind_debt_is_accepted_with_a_debt_link_and_a_contract_can_be_linked_from_the_debt` |
| R4 | A5 | `DebtPlanTests.The_debt_shows_paid_open_next_installment_and_expected_end`, `DebtPlanTests.A_manual_open_amount_is_shown_with_its_date_and_counts_payments_after_that_date`, `SavingsPageTests.A_debt_with_a_plan_shows_paid_next_installment_expected_end_and_reference` |
| R4 | A9 | `SavingsPageTests.A_debt_with_a_plan_shows_paid_next_installment_expected_end_and_reference`; widths as above |
| R5 | A6 | `DebtPlanTests.A_reference_and_a_note_of_1000_characters_are_saved_and_1001_are_refused` |
| R6 | A7 | `DebtPlanTests.Changing_the_rate_from_march_keeps_december_to_february_and_starts_a_new_contract`, `DebtPlanTests.Changing_an_existing_plan_without_a_month_is_refused`, `DebtPlanTests.Removing_the_plan_ends_the_contract_at_the_end_of_the_current_month` |
| R7 | A8 | `DebtPlanTests.The_contract_list_says_which_debt_a_plan_contract_belongs_to_and_the_contract_endpoint_refuses_changes`, `CommitmentsPageTests.A_plan_contract_says_which_debt_it_belongs_to_and_its_edit_opens_the_debt` |
| R8 | A10 | Plagevo part after merge (not in this branch); the Duofy side, German error messages, is `DebtPlanTests.The_error_bodies_for_pot_and_debt_carry_a_german_explanation` |

## Threats

- Can someone pretend to be someone else? Owner from the token or the checked ?owner= grant, as for debts today.
- Can someone change data? Only with the debts grant for the owner; the plan contract only through the debt.
- Can someone deny an action? Changes keep updated_at; MCP calls stay audited.
- Can someone read what they should not? Debt responses only to those who may see the owner's debts.
- Can someone bring it to a halt? The number of installments is bounded (at most 600).
- Can someone gain rights they should not have? No new rights; contract rights do not bypass the debt.

## Non-goals

- No interest calculation, no amortisation table.
- No receivables (money owed to the person).
- No document links, files or history entries.
- No automatic conversion of existing contracts.

## Risks

- Rounding of the last installment: computed in cents and tested at the edges (exact division, one cent rest).
- Plan changes touching past months: contained by the contract rule and A7.

## Open questions

None.

## Approval

Approved by the human on 2026-11-12 (session answers: new feature alongside, also loans; Duofy with MCP part in Plagevo; installment counts fully; only money owed; no documents now; wait instead of bridging; "Ja, freigeben"; migration merges only with the human's yes).
