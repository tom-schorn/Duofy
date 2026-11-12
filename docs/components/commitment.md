# Component: Commitment

## Purpose

A recurring payment or income of one person — a contract, a salary, a savings rate or a loan repayment — from which the monthly plan will take its positions. It is not a booking (nothing moves when it is saved) and not a plan position.

## Terms

| Term | Meaning |
| --- | --- |
| Vertrag | A recurring expense, such as rent or a subscription; pot Bedürfnisse or Wünsche. |
| Einnahme | Recurring money coming in, such as salary; pot Einnahmen. |
| Sparziel | Money put aside regularly, optionally towards a target amount and date; pot Sparen. |
| Schuld | A regular repayment of a debt; pot Sparen. |
| Ratenplan | The contract of the kind Schuld that Duofy creates for a debt or loan; it belongs to the debt and is changed only there (spec 137). |
| Letzte Rate | The amount of the last due date when it differs from the others (`last_amount`). |
| Interval | Every how many months it falls due, counted from the first payment. |
| Last payment | The date money flows for the last time; empty means it runs on. |
| Paying account | The account it is paid from; empty means the default account. |
| Target account | An own account the money moves to; makes the payment a transfer. |
| Pass-through | Moves the balance but counts in no budget and no quota. |
| Payment method | Lastschrift, Dauerauftrag, Überweisung, Abhebung or Sonstiges. |

## Data

- `type`, `name` (1–100), `amount` (> 0, two decimals), `interval_months` (1–120), `first_due_date`, `last_due_date` (optional), `category_id`, `pot`, `account_id` (optional), `counter_account_id` (optional), `pass_through`, `payment_method`, `target_amount` and `target_date` (optional, Sparziel only), `last_amount` (optional, the amount in the month of the last payment, spec 137), `debt_id` and `savings_goal_id` (optional links), `owner_id`.

## Rules

- Due in a month when the month is not before the first payment, not after the last payment and on the interval; the due day falls back to the last day of the month.
- Sparziel and Schuld always pot Sparen, Einnahme always pot Einnahmen.
- A `debt_id` is allowed on a contract of the kind Schuld (pot Sparen) and on a normal contract, both without target account and without savings goal; otherwise `commitment_debt_invalid`. The error bodies of `commitment_pot_invalid` and `commitment_debt_invalid` carry a German `message` that explains the rule (spec 137).
- The amount of a month is the `last_amount` in the month of the last payment when set, otherwise the amount (`AmountIn`); positions copy it, so the last month shows the smaller last installment.
- The installment plan of a debt is the contract the debt points to (`debts.plan_commitment_id`). It is created, changed and ended only through the debt; the contract endpoint refuses changing or ending it with `commitment_is_debt_plan` and a German message, and the contract list shows "gehört zu <Schuld>" with `debt` (id, name) in the response (spec 137, R7).
- Changing a plan from a month: the old contract ends before that month (its last amount is dropped), a new contract starts; earlier months stay as they were (spec 137, R6). Removing the plan ends the contract at the end of the current month.
- Contracts are ended, never deleted; "Beenden" sets the last payment to the latest one on or before today.
- Only the owner sees and changes a contract.

## Interfaces

`/api/v1/commitments` (GET, POST, PATCH, POST `/{id}/end`), see `docs/specs/64-contracts`; the plan of a debt through `/api/v1/debts` (`plan`, `clearPlan`, `linkedCommitmentId`), see `docs/specs/137-debt-installment-plan`.

## Open questions

None.
