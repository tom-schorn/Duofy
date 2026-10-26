# Functional specification: Contracts

Format: 2

## Summary

People record their recurring payments and income as contracts on a new app page "Verträge": create, change and end them. The API offers the contracts of the signed-in person only. The existing domain class `Commitment` gets the fields the human decided on 2026-10-21 and 2026-10-23; the plan that is generated from contracts is a later feature.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R10. R11 is out of scope.

## Components

- [Commitment](docs/components/commitment.md)
- [Account](docs/components/account.md)
- [Category](docs/components/category.md)

## Solution

- **Domain** (`Duofy.Domain/Commitment.cs`): add category id, pot, paying account id, target account id, pass-through, payment method, target amount, target date, timestamps. A `PaymentMethod` enum with `DirectDebit`, `StandingOrder`, `Transfer`, `Withdrawal`, `Other`. A method `LastPaymentOnOrBefore(DateOnly day)` that returns the latest due date on or before the day, or null when there is none; the end button uses it. `IsDueIn`, `EffectiveDueDay` and `RunsOn` stay unchanged.
- **Data**: migration on `commitments` adding the new columns with foreign keys to `categories` and `accounts` (no cascade).
- **API** (`Duofy.Api`): endpoints below, every query filtered by the owner from the token.
- **Client and page**: `ICommitmentApi` in `Duofy.ApiClient`; page `/vertraege` in `Duofy.Shared` with a list (name, type, amount, interval in words, next due date, ended ones folded away) and a dialog; nav entry "Verträge" in the app.

## Behaviour

- R1: Name 1–100 characters after trimming; type required; amount greater than 0 with at most two decimals; interval 1–120; first payment date required; a last payment date before the first one is allowed and means the contract never fell due.
- R2: The category is a built-in one or an own one of the person; the pot is required. For Sparziel and Schuld the API stores pot Sparen and for Einnahme pot Einnahmen, whatever was sent; the dialog shows the pot fixed for these types. For Vertrag the pot is Bedürfnisse or Wünsche (Einnahmen and Sparen answer `400 commitment_pot_invalid`).
- R3: The paying account is optional; when given it must be an active account of the person, otherwise `400 account_not_found` or `400 account_closed`. Empty means the default account at the time of paying; the response sends it as null.
- R4: The target account is optional; it must be an active account of the person and differ from the paying account (or from the default account when the paying account is empty), otherwise `400 commitment_target_same_account`. A contract of type Einnahme cannot have a target account (`400 commitment_target_invalid`).
- R5: `passThrough` is a plain switch, default off.
- R6: Payment method required, one of the five keys; missing or unknown answers `400 payment_method_invalid`.
- R7: Target amount (greater than 0, two decimals) and target date are optional and only allowed for Sparziel; for other types they answer `400 commitment_target_only_for_savings_goal`.
- R8: There is no `DELETE`. `POST /commitments/{id}/end` sets the last payment date to `LastPaymentOnOrBefore(today)` in Europe/Berlin; when there is none, to the day before the first payment date. The last payment date can also be set or cleared in the dialog.
- R9: A contract id of another person answers `404 commitment_not_found`, never `403`.
- R10: The list shows running contracts (`RunsOn(today)`); a switch "Beendete zeigen" adds the ended ones. Sorted by next due date, then name.

## Data and interfaces

Table `commitments` gains: `category_id` (FK categories), `pot` (text), `account_id` (FK accounts, null), `counter_account_id` (FK accounts, null), `pass_through` bool, `payment_method` (text), `target_amount` numeric(12,2) null, `target_date` date null, `created_at`, `updated_at`. Existing rows in a developer database get the pot from their type, a built-in category of that pot (the builder names it as an assumption) and payment method `other`.

| Method and path | Body | Answers |
| --- | --- | --- |
| `GET /api/v1/commitments?includeEnded=true` | – | `200 CommitmentResponse[]` with `nextDueDate` |
| `POST /api/v1/commitments` | `CreateCommitmentRequest { type, name, amount, intervalMonths, firstDueDate, lastDueDate?, categoryId, pot, accountId?, counterAccountId?, passThrough, paymentMethod, targetAmount?, targetDate? }` | `201 CommitmentResponse`; `400 { code }` |
| `PATCH /api/v1/commitments/{id}` | `UpdateCommitmentRequest` (all optional) | `200`; `400`; `404` |
| `POST /api/v1/commitments/{id}/end` | – | `200 CommitmentResponse`; `404` |

External input: yes

## Acceptance criteria

- [ ] A1 (R1): Given a signed-in person, when they POST a Vertrag "Miete" of 890.00, interval 1, first payment 2026-11-01, category Miete, pot Bedürfnisse, payment method Dauerauftrag, then the answer is 201 and GET /api/v1/commitments lists it with nextDueDate 2026-11-01.
- [ ] A2 (R1): Given a contract request with an empty name, a name of 101 characters, amount 0, amount 1.005, interval 0 or interval 121, when it is sent, then the answer is 400 and no contract is created.
- [ ] A3 (R2): Given a Sparziel sent with pot Wünsche, when it is created, then it is stored with pot Sparen; given an Einnahme sent with pot Bedürfnisse, then it is stored with pot Einnahmen.
- [ ] A4 (R2): Given a Vertrag with pot Sparen, when it is sent, then the answer is 400 with code commitment_pot_invalid.
- [ ] A5 (R3): Given a person with a default account and a closed account, when a contract names the closed account as paying account, then the answer is 400 with code account_closed; when it names none, then accountId in the answer is null.
- [ ] A6 (R4): Given a Sparziel paid from Giro with target account Sparkonto, when it is created, then the answer is 201 with counterAccountId of the Sparkonto; when the target account equals the paying account, then the answer is 400 with code commitment_target_same_account.
- [ ] A7 (R5): Given a contract created with passThrough true, when it is listed, then passThrough is true; without the field it is false.
- [ ] A8 (R6): Given a contract request without payment method or with "cheque", when it is sent, then the answer is 400 with code payment_method_invalid.
- [ ] A9 (R7): Given a Sparziel with target amount 3000.00 and target date 2027-07-31, when it is created, then both are returned; given a Vertrag with a target amount, then the answer is 400 with code commitment_target_only_for_savings_goal.
- [ ] A10 (R8): Given a monthly contract with first payment 2026-01-01 and today 2026-10-23, when POST /end is called, then lastDueDate is 2026-10-01 and the contract is not listed without includeEnded after that day.
- [ ] A11 (R8): Given a contract with first payment 2026-12-15 and today 2026-10-23, when POST /end is called, then lastDueDate is 2026-12-14, the answer says running false, and the contract is not listed without includeEnded.
- [ ] A16 (R8): Given the page Verträge with a running contract, when Beenden is tapped, then a confirmation is shown and the contract ends only after it is confirmed; cancelling leaves it running.
- [ ] A12 (R8): Given any contract, when DELETE /api/v1/commitments/{id} is sent, then the answer is 404 or 405 and the contract still exists.
- [ ] A13 (R9): Given two people, when person B sends PATCH or POST /end on a contract of person A, then the answer is 404 with code commitment_not_found and the contract of A is unchanged; GET by B does not list it.
- [ ] A14 (R10): Given the page Verträge with one running and one ended contract, when it is opened, then only the running one is shown; when "Beendete zeigen" is switched on, then both are shown.
- [ ] A15 (R10): Given the contract dialog, when the type Sparziel is chosen, then the pot shows Sparen and cannot be changed, and the fields Zielbetrag and Zieldatum appear; for Vertrag they are hidden.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1, A2 | `Tests/Duofy.Backend.Tests/CommitmentTests.cs`: `A_new_contract_is_listed_with_its_next_due_date`, `Invalid_name_amount_or_interval_is_refused_and_nothing_is_created` |
| R2 | A3, A4 | `Tests/Duofy.Backend.Tests/CommitmentTests.cs`: `Savings_goals_are_stored_in_the_pot_savings_and_income_in_the_pot_income`, `A_contract_only_takes_the_pots_needs_or_wants` |
| R3 | A5 | `Tests/Duofy.Backend.Tests/CommitmentTests.cs`: `A_closed_paying_account_is_refused_and_no_account_means_the_default_one` |
| R4 | A6 | `Tests/Duofy.Backend.Tests/CommitmentTests.cs`: `A_savings_goal_can_move_money_to_an_own_target_account_but_not_to_the_paying_one`, `Without_paying_account_the_target_must_differ_from_the_default_account`, `An_income_cannot_have_a_target_account` |
| R5 | A7 | `Tests/Duofy.Backend.Tests/CommitmentTests.cs`: `Pass_through_is_kept_and_off_without_the_field` |
| R6 | A8 | `Tests/Duofy.Backend.Tests/CommitmentTests.cs`: `A_missing_or_unknown_payment_method_is_refused` |
| R7 | A9 | `Tests/Duofy.Backend.Tests/CommitmentTests.cs`: `A_savings_goal_keeps_target_amount_and_date_and_a_contract_refuses_them` |
| R8 | A10, A11, A12, A16 | `Tests/Duofy.Backend.Tests/CommitmentTests.cs`: `Ending_a_monthly_contract_sets_the_last_payment_of_this_month_and_hides_it_after_that_day` (A10), `Ending_before_the_first_payment_sets_the_day_before_it_and_ends_at_once` (A11), `A_contract_cannot_be_deleted` (A12); `Tests/Duofy.Shared.Tests/CommitmentsPageTests.cs`: `Ending_a_contract_asks_once_and_then_moves_it_out_of_the_list_of_running_ones` (A16), `Cancelling_the_question_leaves_the_contract_running` (A16) |
| R9 | A13 | `Tests/Duofy.Backend.Tests/CommitmentTests.cs`: `Another_person_can_neither_change_nor_end_nor_see_a_contract` |
| R10 | A14, A15 | `Tests/Duofy.Shared.Tests/CommitmentsPageTests.cs`: `Only_running_contracts_are_shown_until_ended_ones_are_switched_on`, `A_savings_goal_fixes_the_pot_savings_and_shows_target_amount_and_date`, `A_contract_lets_the_pot_be_chosen_and_hides_the_target_fields` |

## Threats

- Can someone pretend to be someone else? Only with a stolen token; the owner always comes from the token, never from the request.
- Can someone change data? Only their own contracts; foreign contract, account and category ids answer 404 or 400.
- Can someone deny an action? Changes carry timestamps; nothing is deleted, ending is reversible by clearing the last payment date.
- Can someone read what they should not? No: every query filters by owner, and foreign ids answer 404 instead of 403.
- Can someone bring it to a halt? Field lengths and number ranges are limited; endpoints need a valid token.
- Can someone gain rights they should not have? No roles involved; the owner field cannot be set by the request.

## Non-goals

- The monthly plan, positions, ticking off, fill levels and the link from bookings to positions.
- Savings progress, rate recommendations, the question when a goal is reached, the automatic pause and restart of a rate, the account protection for earmarked savings.
- Deleting contracts, household privacy, the web page, a limit switch on the contract (decided: every position shows planned and booked, ticking is manual).

## Risks

- The existing tests of the human for `IsDueIn`, `EffectiveDueDay` and `RunsOn` must stay green unchanged.
- Rows the human created while learning live in the local database; the migration fills them with defaults instead of dropping them.

## Open questions

None.

## Approval

Approved by the human on 2026-10-23

Rules decided one at a time in the dialogs of 2026-10-21 and 2026-10-23. The lead's additions (Vertrag only Bedürfnisse or Wünsche, no target account for Einnahme, ending before the first payment sets the day before it, list sorted by next due date with ended ones on request, existing rows kept with defaults) were presented separately and accepted, together with the migration on `commitments`.

Amendment 2026-10-25 (final review round 1, decided by the human): a contract ended before its first payment counts as ended at once (finding M1 of rea), and Beenden asks once before it acts (m2). The web navigation entry "Verträge" that was added beyond this specification stays: the human decided on 2026-10-25 that web and app share all pages.
