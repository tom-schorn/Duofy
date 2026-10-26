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
| Interval | Every how many months it falls due, counted from the first payment. |
| Last payment | The date money flows for the last time; empty means it runs on. |
| Paying account | The account it is paid from; empty means the default account. |
| Target account | An own account the money moves to; makes the payment a transfer. |
| Pass-through | Moves the balance but counts in no budget and no quota. |
| Payment method | Lastschrift, Dauerauftrag, Überweisung, Abhebung or Sonstiges. |

## Data

- `type`, `name` (1–100), `amount` (> 0, two decimals), `interval_months` (1–120), `first_due_date`, `last_due_date` (optional), `category_id`, `pot`, `account_id` (optional), `counter_account_id` (optional), `pass_through`, `payment_method`, `target_amount` and `target_date` (optional, Sparziel only), `owner_id`.

## Rules

- Due in a month when the month is not before the first payment, not after the last payment and on the interval; the due day falls back to the last day of the month.
- Sparziel and Schuld always pot Sparen, Einnahme always pot Einnahmen.
- Contracts are ended, never deleted; "Beenden" sets the last payment to the latest one on or before today.
- Only the owner sees and changes a contract.

## Interfaces

`/api/v1/commitments` (GET, POST, PATCH, POST `/{id}/end`), see `docs/specs/64-contracts`.

## Open questions

None.
