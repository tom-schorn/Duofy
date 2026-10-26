# Functional specification: Accounts

Format: 2

## Summary

People manage their accounts on a new page "Konten": create, rename, close and reopen them, mark a default account and see each balance. The API offers the accounts of the signed-in person only. The human builds this feature to learn; this specification is the guide.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R8. R9 is out of scope.

## Components

- [Account](docs/components/account.md)

## Solution

- **Domain** (`Duofy.Domain/Account.cs`): add type, counts-as-available, IBAN, is-default, owner; `Balance(day)` counts bookings strictly after the opening date. An `AccountType` enum with `Checking`, `Savings`, `Cash`. IBAN normalisation and check (ISO 13616, mod 97) as a small domain function.
- **Data**: table `accounts` with a partial unique index "one default per owner" (`is_default` true), as in the original. Migration in `Duofy.Data`.
- **API** (`Duofy.Api`): endpoints below, every query filtered by the owner from the token.
- **Client and page**: `IAccountApi` in `Duofy.ApiClient`, page `/konten` in `Duofy.Shared` with a list (name, type, balance, default star, closed ones folded away) and a dialog to create or edit.

## Behaviour

- R1: Name 1–100 characters after trimming; type required; opening balance with at most two decimals, may be negative; opening date not in the future.
- R2: When the type is chosen in the create dialog, the switch takes the type's preset; a later type change does not touch a switch the person changed.
- R3: Balance on a day = opening balance + sum of bookings of that account dated after the opening date up to that day (incoming transfers add, outgoing subtract; until the bookings feature exists, the sum is zero).
- R4: Setting `isDefault` on one account clears it on the others of the same person in the same transaction. Creating the first Girokonto of a person sets it as default when no default exists.
- R5: `DELETE` does not exist. Closing sets `active` false and clears `isDefault`; reopening sets `active` true. The list endpoint returns closed accounts too, marked; pickers ask with `active=true`.
- R6: Spaces removed, letters upper-cased, check digits verified; an invalid IBAN answers `400 iban_invalid`; an empty field means no IBAN.
- R7: Another person's account id answers `404 account_not_found`, never `403`, so ids do not reveal existence.
- R8: Opening balance and date can be changed at any time; the opening date may only move to a day before every booking of that account, otherwise `400 opening_date_after_booking` (bookings on or before the opening date are not allowed, see spec 59).

## Data and interfaces

Table `accounts`: `id`, `owner_id` (FK users, cascade), `name` (100), `type` (text: checking, savings, cash), `opening_balance` numeric(12,2), `opening_date` date, `counts_as_available` bool, `iban` (34, null), `is_default` bool, `active` bool, timestamps.

| Method and path | Body | Answers |
| --- | --- | --- |
| `GET /api/v1/accounts?active=true` | – | `200 AccountResponse[]` with `balance` |
| `POST /api/v1/accounts` | `CreateAccountRequest { name, type, openingBalance, openingDate, countsAsAvailable, iban? , isDefault }` | `201 AccountResponse`; `400 { code }` |
| `PATCH /api/v1/accounts/{id}` | `UpdateAccountRequest` (all optional, incl. `active`, `isDefault`) | `200`; `400`; `404` |

External input: yes

## Acceptance criteria

- [ ] A1 (R1): Given a signed-in person, when they POST an account with name "Giro", type checking, opening balance 1250.00 and opening date 2026-10-01, then the answer is 201 and GET /api/v1/accounts lists it with balance 1250.00.
- [ ] A2 (R1): Given an account request with an empty name, a name of 101 characters, an unknown type or an opening date tomorrow, when it is sent, then the answer is 400 and no account is created.
- [ ] A3 (R2): Given the create dialog, when the type Sparkonto is chosen, then the switch "zählt als verfügbar" is off, and for Girokonto and Bargeld it is on.
- [ ] A4 (R3): Given an account opened on 2026-10-01 with 1250.00 and bookings of -40.00 on 2026-10-01 and -60.00 on 2026-10-02, when the balance for 2026-10-02 is computed, then it is 1190.00.
- [ ] A5 (R4): Given a person without accounts, when they create a Girokonto, then it is the default account; when they mark a second account as default, then only the second one is default.
- [ ] A6 (R5): Given the default account, when it is closed, then it is not active, not default, still listed by GET /api/v1/accounts and not listed by GET /api/v1/accounts?active=true.
- [ ] A7 (R5): Given a closed account, when it is reopened, then it is active again.
- [ ] A8 (R6): Given the IBAN "de89 3704 0044 0532 0130 00", when an account is created with it, then it is stored as DE89370400440532013000; given DE89370400440532013001, then the answer is 400 with code iban_invalid.
- [ ] A9 (R7): Given two people, when person B requests PATCH on person A's account, then the answer is 404 with code account_not_found and A's account is unchanged; GET by B does not list it.
- [ ] A10 (R8): Given an account with a booking dated 2026-10-05, when the opening date is changed to 2026-10-04, then the answer is 200; when it is changed to 2026-10-05, then the answer is 400 with code opening_date_after_booking.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1, A2 | `AccountTests.Created_account_is_listed_with_its_opening_balance`, `AccountTests.Invalid_account_answers_400_and_creates_nothing`, `AccountTests.Name_may_have_100_characters_but_not_101` |
| R2 | A3 | `AccountDraftTests.Choosing_a_type_presets_the_switch`, `AccountsPageTests.In_the_create_dialog_savings_turns_the_switch_off_and_checking_and_cash_turn_it_on` |
| R3 | A4 | `AccountBalanceTests.Booking_on_the_opening_date_is_contained_in_the_opening_balance` |
| R4 | A5 | `AccountTests.First_checking_account_becomes_default_and_marking_another_replaces_it`, `AccountsPageTests.The_star_marks_another_account_as_default` |
| R5 | A6, A7 | `AccountTests.Closing_the_default_account_clears_the_mark_and_hides_it_from_pickers`, `AccountTests.Closed_account_can_be_reopened` |
| R6 | A8 | `IbanTests.Spaces_are_removed_and_letters_upper_cased`, `IbanTests.Wrong_check_digits_are_rejected`, `AccountTests.Iban_is_stored_without_spaces_in_capitals_and_checked` |
| R7 | A9 | `AccountTests.Another_persons_account_answers_404_and_stays_unchanged` |
| R8 | A10 | `BookingTests.With_a_booking_on_the_fifth_the_fourth_is_allowed_and_the_fifth_is_not` |

## Threats

- Can someone pretend to be someone else? Only with a stolen token; the owner always comes from the token (spec 57), never from the request.
- Can someone change data? Only their own accounts; foreign ids answer 404.
- Can someone deny an action? Changes carry timestamps; closing is reversible, nothing is deleted.
- Can someone read what they should not? No: every query filters by owner, and foreign ids answer 404 instead of 403.
- Can someone bring it to a halt? Request size and field lengths are limited; endpoints need a valid token.
- Can someone gain rights they should not have? No roles involved; the owner field cannot be set by the request.

## Non-goals

- Deleting accounts (only closing), household visibility and grants, the balance curve, carry-over, other currencies, bank connection or import.
- More account types or a credit card with monthly statement.

## Risks

- A10 needs bookings, which come with spec 59; until then A10 is tested with a booking row written directly in the test database.
- The human is learning: the team names concepts instead of writing the code.

## Open questions

None.

## Approval

Approved by the human on 2026-10-21

Rules decided one at a time in the dialog of 2026-10-21; the lead's additions (negative opening balance allowed, opening date not in the future, opening date only movable before the first booking, 404 for foreign ids) were presented separately and accepted.
