# Functional specification: "Jetzt verfügbar" next to "Verplanbar"

Format: 2

## Summary

The start page, the month plan and the household view show "Verplanbar im Monat" (unchanged figure, new label and hint) and a new "Jetzt verfügbar": balances of the available accounts (per-account flag) minus the open expenses still due this month, with a short breakdown.

## Customer requirements

[customer-requirements.md](customer-requirements.md): R1, R2, R3, R4.

## Components

- [Plan](docs/components/plan.md)
- [Account](docs/components/account.md)

## Solution

- Figure (R1): a new computation next to PlanFigures.Free, `PlanFigures.AvailableNow`, for the current month only: sum of today's balances of the person's accounts with the per-account flag `Account.CountsAsAvailable` (the set `PlanStore.AvailableAccountIdsAsync` also gives Verplanbar), minus the open expenses of the current month: for every expense position (pots needs, wants, savings; not income, not pass-through, not moved away per #136) that is not ticked and whose source account counts, the planned amount minus what is already booked on it, not below 0. The source account of a position without an account is the person's default account (as when ticking). A transfer position counts only when money leaves the counted set (source counts, target does not); between two counted accounts, or from a not counted account, it lowers nothing. For a past or future month the number is not shown.
- Labels (R2): "Verplanbar" becomes "Verplanbar im Monat" with the hint "inklusive geplanter Einnahmen dieses Monats"; its value does not change.
- Display (R3): wherever the free amount is shown for the current month (start page block, month plan header, household month), "Jetzt verfügbar" is shown next to it with the breakdown "Konten <Summe> − offen bis Monatsende <Summe>"; reused component in all places; tablet and phone widths per spec 101.
- Grants (R4): the household sum and the person switch include a member's "Jetzt verfügbar" only when the viewer may see that member's account balances; otherwise the member is named as not included, like the existing not-shared hints.

## Behaviour

- R1: accounts Giro 1.000 €, Bargeld 50 € (both flagged available), Tagesgeld 3.000 € (not flagged); open expenses this month: Miete 800 € unticked, Strom 100 € with 40 € booked, a ticked Internet 30 € → Jetzt verfügbar = 1.050 − (800 + 60) = 190 €. Planned salary of 2.000 € on the 30th is not counted.
- R1: a negative result is shown as negative.
- R2: Verplanbar im Monat shows the same value as before the change.
- R3: start page, month plan and household month show both with breakdown at 412, 800 and 1024 px.
- R4: Tom without a grant on Jasmin's accounts sees the household "Jetzt verfügbar" without Jasmin and the hint naming her.

## Data and interfaces

No database change. Plan month and start page responses get `availableNow` with `balances` and `openExpenses` (current month only, else null).

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given the accounts and positions of the example, when the current month is read, then availableNow is 190,00 € with balances 1.050,00 € and open expenses 860,00 €.
- [ ] A2 (R1): Given a transfer position between two available accounts (also with no account on the position, meaning the default), a transfer from a not available account, an expense paid from a not available account and a moved-away position, when availableNow is computed, then none lowers it; given a transfer to a not available account, then it counts as open expense until ticked.
- [ ] A3 (R2): Given any month, when the free amount is read, then its value equals the value before the change and the label reads "Verplanbar im Monat" with the hint.
- [ ] A4 (R3): Given the start page, the month plan and the household month at 412, 800 and 1024 px, when the current month is shown, then both numbers and the breakdown fit; for another month only Verplanbar im Monat is shown.
- [ ] A5 (R4): Given a member without granted account balances, when the household view is shown, then their figures are left out of Jetzt verfügbar and the hint names them.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | `PlanAvailableNowTests.Example_of_the_spec_gives_190_euro`, `AvailableNowTests.A1_the_current_month_shows_balances_minus_open_expenses` |
| R1 | A2 | `PlanAvailableNowTests.A_transfer_between_own_non_savings_accounts_is_no_expense`, `A_transfer_to_a_savings_account_is_an_open_expense_until_ticked`, `Moved_and_pass_through_positions_are_not_open_expenses` |
| R2 | A3 | `AvailableNowPageTests.Allocatable_is_labelled_per_month_with_the_hint_about_planned_income` (value unchanged: `PlanFreeTests`, unchanged) |
| R3 | A4 | `AvailableNowPageTests.The_month_shows_available_now_with_its_breakdown_next_to_allocatable`, `Without_a_value_there_is_no_available_now_block`, `AvailableNowTests.A4_another_month_has_no_available_now` (widths 412/800/1024 px: browser check by the reviewer, not run) |
| R4 | A5 | `AvailableNowTests.R4_a_viewer_without_the_accounts_grant_gets_no_number_and_with_it_the_same`, `AvailableNowTests.A5_the_household_leaves_out_a_member_without_grant_and_names_them`, `AvailableNowPageTests.A_household_value_names_the_members_that_are_not_included` |

## Non-goals

- No "until the next income" window, no forecast per day.
- No change to Verplanbar's computation, quotas or pots.

## Risks

Balances that are not up to date make the number wrong: the breakdown shows the balance sum so the person sees what it is based on.

## Open questions

None.

## Approval

Amended 2026-11-12: accounts by the per-account flag (human).

Approved by the human on 2026-11-12 (session answers: two numbers, all accounts except savings, open expenses until month end, "Ja, freigeben").
