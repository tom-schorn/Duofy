# Functional specification: Bookings

Format: 2

## Summary

Duofy gets a page "Buchungen": one month at a time, filterable by account, with sums at the top and a plus button for quick entry. People record expenses, income and transfers with a pot and a category, may add own categories, and change or delete bookings with a confirmation. Account balances follow at once.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R11 and R13. R12 is out of scope.

## Components

- [Booking](docs/components/booking.md)
- [Category](docs/components/category.md)
- [Account](docs/components/account.md)

## Solution

- **Domain** (`Duofy.Domain`): `Booking` gets account, optional counter account, date, positive amount, optional note and a list of `BookingLine` (amount, pot `Pot`: Income, Needs, Wants, Savings, category); a transfer has no line, or exactly one line with pot savings and no category. A method gives the signed effect on a given account. `Category` as data (key, group, German name via resource key, suggested pot, owner null for the built-in ones).
- **Data**: tables `categories` (built-in rows seeded by migration from the original's list; own rows with `owner_id`) and `bookings` with check constraints as in the original (purpose unless transfer, two different accounts, amount > 0). Account foreign keys use RESTRICT; accounts are never deleted anyway.
- **API** (`Duofy.Api`): endpoints below, owner from the token.
- **Client and pages**: `IBookingApi`, `ICategoryApi` in `Duofy.ApiClient`; page `/buchungen` and the booking dialog in `Duofy.Shared`, readable as an example for the human.

## Behaviour

- R1: The dialog has the choice Ausgabe / Einnahme / Umbuchung and a positive amount field; Einnahme sets pot income, Ausgabe offers needs, wants and savings. Effect on the account: income +amount, other pots −amount; transfer −amount on the from-account, +amount on the to-account.
- R2: An expense or income without a pot answers `400 pot_required`; a transfer with a pot other than savings answers `400 transfer_pot_invalid`.
- R3: Categories are listed grouped; choosing one preselects its suggested pot, which the person may change. Income bookings only offer income categories, expenses only the others.
- R4: An own category has a name (1–60 characters, unique per person without case among own and built-in names), a group (one of the built-in groups) and a pot; it can be renamed and, while no booking uses it, deleted.
- R5: From- and to-account must differ (`400 transfer_same_account`), both own and active; no category.
- R6: Date on or before the opening date of a touched account answers `400 date_before_opening`; a date after today (Europe/Berlin) answers `400 date_in_future`.
- R7: Any field may change, with the same rules as on creation; the page asks "Buchung löschen?" before deleting.
- R8: `/buchungen` opens the current month; the arrows change the month; the filter shows one account (bookings where it is from- or to-account) or all; the sums count income and expenses of the shown bookings, transfers in neither sum unless the filter shows one account, where they count as in or out.
- R9: The plus button opens the dialog with type Ausgabe, today's date and the default account; without a default account the account field is empty and required.
- R10: After saving, the account list shows the new balance (computed, spec 58).
- R11: Foreign ids answer `404`.
- R13: An expense or income has one to 20 lines; each line amount > 0; the lines add up exactly to the booking's amount (`400 lines_sum_mismatch`). All lines of an income have pot income; all lines of an expense have needs, wants or savings (`400 line_pot_invalid`). The dialog starts with one line holding the whole amount; "Aufteilen" adds a line, and the first line always takes the rest, so the sum fits while typing. The month list shows a split booking once with its total and the note "aufgeteilt"; the account balance uses the total.

## Data and interfaces

Tables: `categories` (`id`, `key` text unique for built-ins, `owner_id` null for built-ins, `group` text, `name` text for own ones, `suggested_pot` text); `bookings` (`id`, `owner_id`, `account_id`, `counter_account_id` null, `date`, `amount` numeric(12,2) > 0, `note` (200) null, timestamps); `booking_lines` (`id`, `booking_id` cascade, `amount` numeric(12,2) > 0, `pot` text, `category_id` null, `position` order 0..19).

| Method and path | Body | Answers |
| --- | --- | --- |
| `GET /api/v1/bookings?year=&month=&accountId=` | – | `200 BookingResponse[]` and sums |
| `POST /api/v1/bookings` | `SaveBookingRequest { kind, accountId, counterAccountId?, date, amount, note?, lines: [{ amount, pot, categoryId? }] }` | `201`; `400 { code }` |
| `PUT /api/v1/bookings/{id}` | `SaveBookingRequest` | `200`; `400`; `404` |
| `DELETE /api/v1/bookings/{id}` | – | `204`; `404` |
| `GET /api/v1/categories` | – | `200` built-in and own, grouped |
| `POST /api/v1/categories` | `SaveCategoryRequest { name, group, pot }` | `201`; `400` |
| `PUT /api/v1/categories/{id}` | `SaveCategoryRequest` | `200`; `400`; `404` |
| `DELETE /api/v1/categories/{id}` | – | `204`; `400 category_in_use`; `404` |

External input: yes

## Acceptance criteria

- [ ] A1 (R1, R10): Given a Girokonto opened on 2026-10-01 with 1000.00, when an expense of 42.10 dated 2026-10-21 with pot needs and category groceries is saved, then the answer is 201 and the account's balance is 957.90.
- [ ] A2 (R1, R10): Given the same account, when an income of 2450.00 with category salary is saved, then the balance rises by 2450.00; when a booking with amount 0 or -5 is sent, then the answer is 400.
- [ ] A3 (R2): Given an expense without pot, when it is sent, then the answer is 400 with code pot_required.
- [ ] A4 (R3): Given GET /api/v1/categories, when it is read, then it contains the original's built-in categories grouped, each with its suggested pot, and groceries suggests needs.
- [ ] A5 (R3): Given the booking dialog with type Ausgabe, when the category Restaurant is chosen, then the pot wants is preselected and can be changed to needs.
- [ ] A6 (R4): Given a signed-in person, when they POST a category "Friseur Kinder" in group children with pot needs, then it is listed for them and not for another person; a second category with the same name in other capitals answers 400.
- [ ] A7 (R4): Given an own category used by a booking, when it is deleted, then the answer is 400 with code category_in_use; when it is unused, then 204.
- [ ] A8 (R5, R10): Given a Girokonto with 1000.00 and a cash account with 0.00, when a transfer of 100.00 from the Girokonto to the cash account is saved, then the balances are 900.00 and 100.00.
- [ ] A9 (R5): Given a transfer with the same from- and to-account, or with pot needs, or with a category, when it is sent, then the answer is 400.
- [ ] A10 (R5): Given a transfer to a Sparkonto with pot savings, when it is saved, then it is stored with pot savings.
- [ ] A11 (R6): Given an account opened on 2026-10-01, when a booking dated 2026-10-01 is sent, then the answer is 400 with code date_before_opening; dated tomorrow, then 400 with code date_in_future; dated 2026-10-02, then 201.
- [ ] A12 (R7): Given a saved booking, when PUT changes its amount, then the balance follows; when DELETE is sent, then the answer is 204 and the balance is as before the booking.
- [ ] A13 (R7): Given the bookings page, when the delete action of a booking is clicked, then a confirmation dialog appears and the booking is deleted only after confirming.
- [ ] A14 (R8): Given bookings in September and October, when the page opens in October and the left arrow is clicked, then only September's bookings show, newest first, with September's sums.
- [ ] A15 (R8): Given bookings on two accounts, when the filter selects one account, then only bookings touching that account show and transfers count as income or expense in the sums.
- [ ] A16 (R9): Given a default account, when the plus button is clicked, then the dialog opens with type Ausgabe, today's date and the default account selected.
- [ ] A17 (R11): Given two people, when person B requests GET, PUT or DELETE on a booking or own category of person A, then the answer is 404 and nothing changes; B's month list does not contain A's bookings.
- [ ] A19 (R13, R10): Given a Girokonto with 1000.00, when an expense of 80.00 is saved with the lines 60.00 groceries/needs and 20.00 personal care/needs, then the answer is 201, the booking has two lines and the balance is 920.00.
- [ ] A20 (R13): Given an expense of 80.00 whose lines add up to 70.00, or a line of 0.00, or an income with a line in pot needs, when it is sent, then the answer is 400 with code lines_sum_mismatch, line_amount_invalid or line_pot_invalid.
- [ ] A21 (R13): Given the booking dialog with amount 80.00, when "Aufteilen" is clicked and the new line gets 20.00, then the first line shows 60.00.
- [ ] A18 (R6): Given a closed account, when a new booking on it is sent, then the answer is 400 with code account_closed.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1, A2 | `BookingTests.An_expense_lowers_the_balance`, `BookingTests.An_income_raises_the_balance_and_amounts_of_zero_or_below_are_refused` |
| R2 | A3 | `BookingRulesTests.Expense_line_without_a_pot_answers_pot_required` |
| R3 | A4, A5 | `CategoryTests.List_contains_the_45_built_in_categories_grouped_with_suggested_pots`, `BookingsPageTests.Choosing_the_category_restaurant_preselects_the_pot_wants_which_can_be_changed` |
| R4 | A6, A7 | `CategoryTests.Own_category_is_listed_for_its_owner_and_not_for_another_person`, `CategoryTests.Name_must_be_unique_without_case_among_own_categories`, `BookingTests.An_own_category_in_use_cannot_be_deleted_and_an_unused_one_can` |
| R5 | A8, A9, A10 | `BookingTests.A_transfer_moves_money_between_two_accounts`, `BookingRulesTests.Transfer_to_the_same_account_answers_transfer_same_account` (and the two other transfer rule tests), `BookingTests.A_savings_transfer_is_stored_with_pot_savings` |
| R6 | A11, A18 | `BookingRulesTests.Date_on_the_opening_date_or_before_answers_date_before_opening`, `BookingRulesTests.Date_after_today_answers_date_in_future`, `BookingTests.A_closed_account_takes_no_new_bookings` |
| R7 | A12, A13 | `BookingTests.Changing_the_amount_moves_the_balance_and_deleting_restores_it`, `BookingsPageTests.Delete_asks_for_confirmation_and_deletes_only_after_confirming` |
| R8 | A14, A15 | `BookingsPageTests.The_left_arrow_shows_the_previous_month_with_its_own_sums`, `BookingsPageTests.The_account_filter_shows_only_that_accounts_bookings_and_counts_transfers_in_the_sums`, `BookingTests.The_month_list_shows_one_month_newest_first_with_its_sums` |
| R9 | A16 | `BookingsPageTests.The_plus_button_opens_the_dialog_with_an_expense_dated_today_on_the_default_account` |
| R10 | A1, A2, A8 | see R1 and R5 (the balance is read from GET /api/v1/accounts) |
| R11 | A17 | `BookingTests.Another_persons_booking_answers_404_and_stays_unchanged`, `CategoryTests.Another_persons_category_answers_404_and_stays_unchanged` |
| R13 | A19, A20, A21 | `BookingTests.A_split_expense_keeps_its_lines_and_uses_the_total_for_the_balance`, `BookingRulesTests.Lines_that_do_not_add_up_answer_lines_sum_mismatch` (and the line amount and line pot tests), `BookingsPageTests.Splitting_adds_a_line_and_the_first_line_shows_the_rest` |

## Threats

- Can someone pretend to be someone else? Only with a stolen token; the owner comes from the token, never from the request.
- Can someone change data? Only their own bookings and categories; account ids in a booking must belong to the caller, otherwise 404.
- Can someone deny an action? Bookings carry creation and change timestamps; deletion needs an explicit confirmation in the UI.
- Can someone read what they should not? No: all queries filter by owner, foreign ids answer 404.
- Can someone bring it to a halt? Field lengths and amounts are limited; the month list is bounded to one month.
- Can someone gain rights they should not have? No roles involved; owner and built-in flags cannot be set by the request.

## Non-goals

- Assigning booking lines to plan positions (comes with the plan), plan month, ticking off positions, separate budgets (rejected 2026-10-21 in favour of positions with assigned lines), carry-over, the 50/30/20 evaluation, the balance curve.
- Import of statements, counterparty name and IBAN, duplicate recognition.
- Search, export, attachments, recurring bookings, households and shared bookings.
- The final fixed category schema (issue 40) and mapping own categories to it.

## Risks

- Bookings need the human's accounts table (spec 58). The team builds categories, domain rules, page and API against an agreed account shape first and connects to the human's table once it is merged; the team never edits the human's account files.
- Seeding 45 built-in categories by migration means renaming them later needs a migration; contained by stable keys.

## Open questions

None.

## Approval

Approved by the human on 2026-10-21

Rules decided one at a time in the dialog of 2026-10-21; the lead's additions (amount > 0, income shows only income categories, own category rules, no new bookings on closed accounts, transfer sums by filter) were presented separately and accepted.

Amended on 2026-10-21 by the human in the dialog: split bookings (R13, A19–A21); the lead's details (1–20 lines, first line takes the rest, all lines of one direction) presented with the amendment.
