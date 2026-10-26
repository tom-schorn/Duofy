# Customer requirements: Accounts

Format: 2

## Background

The original Duofy has accounts with six types, a computed balance, a default account, closing, an IBAN and household privacy. The .NET rebuild has a domain class `Account` with name, start amount, start date, active flag and a balance method that also counts bookings on the start date. On 2026-10-21 the human went through the original's rules one at a time and decided what the rebuild keeps. The human builds this feature completely (API part and page) to learn; the team accompanies.

## Goal

A person can create their accounts, see each account's current balance, mark one as default and close accounts they no longer use, in the browser and in the app.

## Users

Every Duofy user, when starting with Duofy and whenever an account is opened or closed at the bank; the balance is looked at several times a week.

## Requirements

R1 (must): An account has a name, one of three types (Girokonto, Sparkonto, Bargeld), an opening balance and an opening date — because the original's six types were pure labels; only these three mean something.
R2 (must): An account has the switch "counts as available"; the type presets it (Girokonto and Bargeld on, Sparkonto off) and the person may change it — because only this switch decides whether money is spendable.
R3 (must): The balance is the opening balance plus every booking after the opening date; bookings on the opening date are contained in the opening balance; the balance is never stored — because a stored balance drifts apart from the bookings (issue 42).
R4 (must): At most one account per person is the default account; marking another one replaces the old mark; the first Girokonto a person creates becomes the default automatically — because the default is preselected when booking.
R5 (must): Accounts are never deleted, only closed and reopened; a closed account disappears from pickers but keeps its bookings; closing the default account removes the default mark — because old bookings and months must stay correct.
R6 (must): An account may carry an IBAN, stored without spaces in capital letters and checked by its check digits — because the import will recognise own transfers by it, and a typo should show at once.
R7 (must): A person sees and changes only their own accounts — because accounts are private.
R8 (should): The opening balance and the opening date stay correctable — because they are easy to mistype when creating the account.
R9 (wont): Household visibility of accounts, the balance curve, other currencies than euro — because they come with the household and booking features or are not needed now.

## Constraints

- The human builds it in `Duofy.Api` (endpoints), `Duofy.Data` (table), `Duofy.Domain` (rules), `Duofy.Schemas`, `Duofy.ApiClient` and one shared page in `Duofy.Shared`, on top of the API foundation (spec 57).
- The existing `Account.Balance` in `Duofy.Domain` counts bookings from the opening date on (`>=`) and must change to after it (`>`).

## Open questions

None.

## Amendments

- 2026-10-21, late afternoon: the human decided that the team builds the accounts (backend and app are the team's part); the human's part stays the web version. The requirements are unchanged.
