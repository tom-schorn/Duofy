# Functional specification: Grants

Format: 2

## Summary

A table of grants (granter, grantee, area, level), a central access check that every endpoint of accounts, bookings, contracts, plans and categories calls, an optional `owner` on every one of these endpoints, a grants page under Haushalt with presets and hints, and a member switch in the app layout. Without a grant everything behaves as today.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R9. R10 is out of scope.

## Components

- [Grant](docs/components/grant.md)
- [Household](docs/components/household.md)
- [Account](docs/components/account.md)
- [Booking](docs/components/booking.md)
- [Commitment](docs/components/commitment.md)
- [Plan](docs/components/plan.md)

## Solution

- **Domain**: `Area` (`plan`, `book`, `accounts`, `commitments`) and `AccessLevel` (`none` 0, `view` 1, `create` 2, `edit` 3, `delete` 4), compared only by rank. `Grant` (granter id, grantee id, area, level), no row means `none`.
- **Data**: table `grants` with key (granter, grantee, area), check granter ≠ grantee, cascade on user delete. Setting `none` deletes the row. Leaving and removal of #67 delete every grant from and to the person in the same transaction.
- **Central check** (`Duofy.Api`): one service `IAccess` with `LevelAsync(ownerId, area)` and `RequireAsync(ownerId, area, needed)`, the caller taken from the token. Own data answers `delete`; another person answers the stored level only while both are members of the same household, otherwise `none`. Endpoints never compare owners themselves: they resolve the owner (from `owner` in the query or body, or from the record), then ask `IAccess`. Below `view` the endpoint answers 404 as today; with `view` but a missing step 403 with `no_create_granted`, `no_edit_granted` or `no_delete_granted`. The service keeps one place where #56 can later add a database-side filter.
- **Areas per endpoint**: accounts → `accounts`; bookings → `book` (the account of a booking must also be visible with `accounts` ≥ `view`); contracts → `commitments`; plans and positions → `plan`; ticking and assigning bookings to positions → `book`; categories are read with any grant ≥ `view` of the owner and written only by the owner.
- **API for grants**: `GET /households/mine` (from #67) gains in its member entries per member `grantsToMe` and `myGrants` with all four areas; `PUT /households/{id}/grants/{granteeId}` sets the caller's levels for the areas sent.
- **Client and pages** (`Duofy.Shared`, app): every API client method of these areas takes an optional owner; a scoped state "active member" in the layout holds the chosen person and is passed to every page; the switch appears in the app's top bar and the web navigation only when at least one member granted the person `view` somewhere; a page shows its add, edit and delete buttons greyed out with "Dafür hat dir <Name> keine Freigabe gegeben." when the level is missing. Page `/haushalt/freigaben` with one card per other member, presets, a choice per area that saves at once and locks while saving, and the hints.

## Behaviour

- R1: Levels per area are independent; presets: Partner = `delete` in all four; Mitlesen = `view` in all four; Kind = `view` in Plan, `none` elsewhere; Nichts = `none` everywhere.
- R2: `PUT /households/{id}/grants/{granteeId}` takes the granter from the token only; granting oneself answers `400 cannot_grant_self`; a grantee who is not a member answers `404 member_not_found`; a caller who is not a member answers `404 household_not_found`.
- R3: Leaving, removal and account deletion delete every row with the person as granter or grantee; a stored row of two people in different households grants nothing.
- R4, R5: Reading another owner's list needs `view` (`?owner=<id>`); creating for another owner needs `create` (owner in the body); changing needs `edit`, deleting and ending needs `delete`; refusals as described in the solution.
- R6: Every choice sends only the changed area; the card shows "eigene Auswahl" when the four levels match no preset.
- R7: Hints: `book` ≥ `create` while `accounts` < `view` → "Buchen bei dir geht nur, wenn <Name> auch deine Konten ansehen darf."; `book` ≥ `view` while `plan` < `view` → "<Name> sieht deine Buchungen, aber ohne Planung: Ansehen nicht deinen Plan."
- R8: The switch lists "Ich" and every member who granted at least `view` in some area; the chosen member stays when moving between pages and is reset at sign-out; a page whose area the member did not grant shows "Diese Person teilt diesen Bereich nicht mit dir."
- R9: Record ids of an owner who granted nothing answer 404 exactly as before; no endpoint lists people outside the caller's household.

## Data and interfaces

| Method and path | Body | Answers |
| --- | --- | --- |
| `GET /api/v1/households/mine` | – | members gain `grantsToMe` and `myGrants` (`{ plan, book, accounts, commitments }`) |
| `PUT /api/v1/households/{id}/grants/{granteeId}` | `{ plan?, book?, accounts?, commitments? }` | `200 MemberResponse`; `400`; `404` |
| `GET /api/v1/accounts?owner=` · `/bookings?owner=` · `/commitments?owner=` · `/plans/{y}/{m}?owner=` · `/categories?owner=` | – | `200`; `404` without `view` |
| `POST` of accounts, bookings, commitments, positions | `ownerId?` in the body | `201`; `403 no_create_granted`; `404` |
| `PATCH`/`PUT` of these records, tick, changes/apply, carry-over | – | `200`; `403 no_edit_granted`; `404` |
| `DELETE` of these records, ending a contract, removing a carry-over | – | `204`; `403 no_delete_granted`; `404` |

External input: yes

## Acceptance criteria

- [x] A1 (R1, R2): Given Anna and Ben in one household, when Anna sets Ben's levels plan view and book create, then Anna's member entry for Ben shows myGrants plan view, book create, accounts none, commitments none, and Ben's entry for Anna shows grantsToMe with the same values.
- [x] A2 (R2): Given Anna, when she sets grants for herself, then the answer is 400 cannot_grant_self; when she sets grants for Clara of another household, then the answer is 404 member_not_found.
- [x] A3 (R3): Given Anna granted Ben delete in all areas, when Ben leaves the household, then no grant from or to Ben exists, and after Ben joins again Anna's entry for him shows none everywhere.
- [x] A4 (R4, R5): Given Anna granted Ben commitments view, when Ben lists /commitments?owner=Anna, then he gets Anna's contracts; when he creates a contract for Anna, then the answer is 403 no_create_granted; with create he can create it and it belongs to Anna; changing it then answers 403 no_edit_granted.
- [x] A5 (R4, R5): Given Anna granted Ben book edit and accounts view, when Ben changes the amount of Anna's booking, then it is changed; when he deletes it, then the answer is 403 no_delete_granted and the booking still exists.
- [x] A6 (R4): Given Anna granted Ben book create but accounts none, when Ben creates a booking on Anna's account, then the answer is 404 and no booking exists.
- [x] A7 (R4, R5): Given Anna granted Ben plan edit, when Ben opens /plans/2026/11?owner=Anna and adds a one-off position, then Anna's November plan contains it; when he ticks it with book none, then the answer is 404.
- [x] A8 (R3, R5): Given a grant row from Anna to Ben while they are in different households, when Ben lists /accounts?owner=Anna, then the answer is 404.
- [x] A9 (R9): Given Clara who granted nothing, when Ben requests Clara's account, booking, contract or plan position by id, then every answer is 404 and nothing of Clara changes.
- [x] A10 (R5): Given the central check, when a test walks every mapped endpoint of accounts, bookings, contracts, plans and categories with a foreign owner and no grant, then none of them answers 2xx.
- [x] A11 (R6): Given the grants page with Ben's card, when Anna chooses the preset Mitlesen, then all four areas show Ansehen and the API received the four levels; when she then sets Verträge to Nichts, then the card shows "eigene Auswahl" and only the commitments area was sent.
- [x] A12 (R7): Given Ben's card with Buchungen Anlegen and Konten Nichts, when the card is shown, then the hint "Buchen bei dir geht nur, wenn Ben auch deine Konten ansehen darf." is visible and Konten is still Nichts.
- [x] A13 (R8): Given Anna granted Ben plan view only, when Ben opens the app, then the switch offers Ich and Anna; after choosing Anna the plan page shows Anna's month with the add and tick buttons greyed out and the reason "Dafür hat dir Anna keine Freigabe gegeben.", and the contracts page says "Diese Person teilt diesen Bereich nicht mit dir."
- [x] A14 (R8): Given nobody granted Ben anything, when Ben opens the app, then no switch is shown.
- [x] A15 (R8): Given Ben has chosen Anna in the switch, when his access token is renewed in the background, then Anna stays chosen; only signing out resets the switch.
- [x] A16 (R7): Given Ben's card with Buchungen Ansehen and Planung Nichts, when the card is shown, then the hint reads "<Name> sieht deine Buchungen, aber ohne Planung: Ansehen nicht deinen Plan." and no hint claims the bookings are hidden.
- [x] A17 (R4): Given Anna granted Ben commitments edit, when Ben sets or clears the last payment date of Anna's contract by PATCH, then the answer is 403 no_delete_granted and the contract is unchanged; with delete it works.
- [x] A18 (R4): Given Anna granted Ben plan edit, when Ben applies changes that include removing a position, then the answer is 403 no_delete_granted and nothing changes; with plan delete it works.
- [x] A19 (R4): Given Anna granted Ben plan edit and book edit but accounts none, when Ben sends a carry-over for Anna, then the answer is 404 like any other step below view.
- [x] A20 (R6): Given the grants page, when saving a level fails, then the select shows the stored level again.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | `Tests/Duofy.Backend.Tests/GrantTests.cs`: `Anna_sets_bens_levels_and_both_see_them_from_their_side`, `Only_the_areas_sent_change_and_none_deletes_the_row`; `Tests/Duofy.Backend.Tests/Domain/GrantTests.cs`: `A_level_includes_every_level_before_it`, `The_levels_rank_from_none_to_delete`, `There_are_exactly_the_four_areas_plan_book_accounts_and_commitments`, `Missing_areas_read_as_none`; `Tests/Duofy.ApiClient.Tests/GrantClientTests.cs`: `Setting_grants_puts_the_levels_for_the_member` |
| R2 | A1, A2 | `Tests/Duofy.Backend.Tests/GrantTests.cs`: `Anna_sets_bens_levels_and_both_see_them_from_their_side`, `Each_side_sets_only_its_own_grants`, `Anna_cannot_grant_herself_nor_clara_of_another_household`, `Clara_cannot_set_grants_in_annas_household`, `An_unknown_level_is_refused_and_changes_nothing`; `Tests/Duofy.Backend.Tests/GrantedCategoryTests.cs`: `Even_with_everything_granted_ben_cannot_change_or_delete_annas_category` |
| R3 | A3, A8 | `Tests/Duofy.Backend.Tests/GrantTests.cs`: `Leaving_deletes_every_grant_from_and_to_ben_and_joining_again_starts_at_none`, `Removing_ben_deletes_every_grant_from_and_to_him`, `Deleting_bens_account_deletes_every_grant_from_and_to_him`; `Tests/Duofy.Backend.Tests/GrantedAccountTests.cs`: `A_grant_row_between_two_households_grants_nothing`; `Tests/Duofy.Shared.Tests/MemberSwitchTests.cs`: `When_anna_takes_her_grants_back_the_choice_falls_back_to_me` |
| R4 | A4, A5, A6, A7, A17, A18, A19 | `Tests/Duofy.Backend.Tests/GrantedCommitmentTests.cs`: `Ben_sees_annas_contracts_with_view_creates_them_only_with_create_and_cannot_change_them`, `Ben_changes_annas_contract_with_edit_and_ends_it_only_with_delete`, `Setting_or_clearing_the_last_payment_of_annas_contract_needs_delete`; `Tests/Duofy.Backend.Tests/GrantedBookingTests.cs`: `With_edit_ben_changes_annas_booking_but_cannot_delete_it`, `Without_seeing_annas_accounts_ben_cannot_book_on_them`, `With_create_and_accounts_view_ben_books_for_anna_and_she_and_he_see_it`, `Booking_for_anna_with_view_only_is_refused_and_without_any_grant_not_found`; `Tests/Duofy.Backend.Tests/GrantedPlanTests.cs`: `With_plan_edit_ben_opens_annas_november_and_adds_a_position_but_cannot_tick_without_the_book`, `With_plan_view_ben_sees_annas_month_but_adds_changes_and_deletes_nothing`, `With_the_book_ben_ticks_annas_position_and_takes_the_tick_back_only_with_delete`, `With_plan_view_only_annas_month_shows_no_bookings_balances_or_contracts`, `The_carry_over_of_annas_month_needs_the_book_as_well`, `Applying_changes_that_remove_a_position_of_anna_needs_plan_delete`, `Applying_only_a_new_contract_of_anna_works_with_plan_edit`, `A_carry_over_for_anna_without_seeing_her_accounts_is_not_found`; `Tests/Duofy.Backend.Tests/GrantedAccountTests.cs`: `Ben_lists_annas_accounts_only_with_view`, `Ben_creates_and_changes_annas_accounts_step_by_step`, `Making_a_new_account_annas_default_needs_edit`; `Tests/Duofy.ApiClient.Tests/GrantClientTests.cs`: `A_refused_step_gives_its_code`; `Tests/Duofy.Shared.Tests/GrantedPagesTests.cs`: `Without_plan_delete_removing_changes_and_apply_all_are_greyed_out`, `With_plan_delete_removing_changes_and_apply_all_are_free`, `Without_commitments_delete_the_last_payment_of_annas_contract_is_read_only`, `When_creating_or_with_commitments_delete_the_last_payment_is_free` |
| R5 | A4, A5, A7, A8, A10 | `Tests/Duofy.Backend.Tests/AccessWalkTests.cs`: `No_endpoint_answers_with_success_for_a_foreign_owner_without_a_grant`; `Tests/Duofy.Backend.Tests/GrantedCategoryTests.cs`: `Ben_reads_annas_categories_with_any_grant_and_only_then`; `Tests/Duofy.Backend.Tests/GrantedAccountTests.cs`: `A_grant_row_between_two_households_grants_nothing`; `Tests/Duofy.ApiClient.Tests/GrantClientTests.cs`: `Every_list_names_the_owner_only_when_one_is_given`, `Every_plan_address_carries_the_owner` |
| R6 | A11, A20 | `Tests/Duofy.Shared.Tests/GrantsPageTests.cs`: `The_page_is_reachable_under_household_and_needs_a_sign_in`, `Choosing_mitlesen_sends_all_four_levels_and_changing_one_area_sends_only_that_one`, `The_card_locks_while_saving`, `The_presets_set_their_levels`, `Alone_in_the_household_the_page_explains_that_there_is_nobody_to_grant`, `The_household_page_leads_to_the_grants`, `After_a_failed_save_the_select_shows_the_stored_level_again`, `A_choice_while_the_card_is_locked_shows_the_stored_level_again` |
| R7 | A12, A16 | `Tests/Duofy.Shared.Tests/GrantsPageTests.cs`: `Booking_without_seeing_the_accounts_shows_a_hint_and_raises_nothing`, `Seeing_the_book_without_the_plan_shows_the_second_hint` |
| R8 | A13, A14, A15 | `Tests/Duofy.Shared.Tests/MemberSwitchTests.cs`: `Without_any_grant_the_switch_offers_no_person`, `Without_a_household_there_is_no_switch`, `With_a_grant_the_switch_offers_me_and_anna_and_choosing_her_makes_her_the_owner`, `Signing_out_resets_the_chosen_member`, `Renewing_the_tokens_keeps_the_chosen_member`, `When_another_person_is_signed_in_afterwards_the_choice_falls_back_to_me`; `Tests/Duofy.Shared.Tests/GrantedPagesTests.cs`: `With_plan_view_annas_month_shows_with_add_and_tick_greyed_out_and_the_reason`, `A_page_whose_area_anna_did_not_share_says_so_and_asks_nothing`, `With_plan_edit_editing_works_while_ticking_and_deleting_stay_greyed_out`, `With_book_create_ben_books_for_anna_but_cannot_change_or_delete`, `With_accounts_view_annas_accounts_show_with_every_change_greyed_out`, `With_commitments_create_the_contracts_page_lists_annas_and_ending_stays_greyed_out`, `Switching_back_to_me_reloads_my_own_month_with_every_button_free`, `A_position_dialog_opened_for_anna_saves_for_anna_after_switching_to_me`, `A_tick_dialog_opened_for_anna_ticks_for_anna_after_switching_to_me`, `A_carry_over_dialog_opened_for_anna_books_for_anna_after_switching_to_me`, `A_booking_dialog_opened_for_anna_saves_for_anna_after_switching_to_me`, `A_contract_dialog_opened_for_anna_saves_for_anna_after_switching_to_me`, `An_account_dialog_opened_for_anna_saves_for_anna_after_switching_to_me`; `Tests/Duofy.Shared.Tests/AppLayoutTests.cs`: `The_member_switch_sits_in_the_top_bar_only_while_somebody_shares`, `The_start_plus_is_greyed_out_with_the_reason_while_anna_does_not_let_ben_book`; `Tests/Duofy.Shared.Tests/NavMenuTests.cs`: `The_member_switch_sits_at_the_top_of_the_navigation_when_somebody_shares` |
| R9 | A9 | `Tests/Duofy.Backend.Tests/GrantedAccountTests.cs`: `Claras_account_is_not_found_for_ben_and_stays_as_it_is`; `Tests/Duofy.Backend.Tests/GrantedBookingTests.cs`: `Claras_booking_is_not_found_for_ben_and_stays_as_it_is`; `Tests/Duofy.Backend.Tests/GrantedCommitmentTests.cs`: `Claras_contract_is_not_found_for_ben_and_stays_as_it_is`; `Tests/Duofy.Backend.Tests/GrantedPlanTests.cs`: `Claras_plan_and_positions_are_not_found_for_ben_and_stay_as_they_are`; `Tests/Duofy.Backend.Tests/AccessWalkTests.cs`: `No_endpoint_answers_with_success_for_a_foreign_owner_without_a_grant` |

## Threats

- Can someone pretend to be someone else? Only with a stolen access token; the caller and the granter always come from the token, never from the request.
- Can someone change data? Another person's data only at the level its owner granted, checked centrally; a test walks every endpoint without a grant.
- Can someone deny an action? Records keep their owner and timestamps; who of two partners changed something is not recorded (history is a non-goal).
- Can someone read what they should not? Without `view` foreign records answer 404 as before; grants count only inside the same household; members see only the grants between themselves and others, not between two other members.
- Can someone bring it to a halt? Four rows per pair at most; the level lookup is one indexed query per request.
- Can someone gain rights they should not have? Nobody grants themselves or sets rights on somebody else's data; the founder gets no extra rights; leaving deletes all grants.

## Non-goals

- The household plan and totals over several members (#69).
- Private positions (#41), a history of changes by others, notifications to the owner.
- The area import, grants to people outside the household.
- The database-side owner filter of #56 (the central check prepares for it).
- The web page of the grants (the human's part); the shared pages are reachable in the web host as they are.

## Risks

- Every endpoint of four areas is touched; A10 walks them all so none is forgotten.
- A switch that forgets the chosen member on one page would show the wrong person's figures; the active member lives in one scoped state and every page test covers it.
- The migration is applied to the human's database only after the yes.

## Open questions

None.

## Approval

Approved by the human on 2026-10-24

Presented in the lead session on Saturday 2026-10-24, together with the migration. The central check instead of per-endpoint checks and answering 404 instead of 403 without any grant were presented as the lead's changes to the original and accepted.

Amendment 2026-10-24 (second, by the human): links use the server's public address from the environment (`Duofy:PublicUrl`), not the address a client is connected to; a button a person may not use is either not shown or shown greyed out with a short reason, never offered and then refused.

Amendment 2026-10-25 (final review round 4, decided by the human): the switch survives token renewal (M1); the book hint is corrected to a true sentence because reading bookings needs only book view, as in the original's server (M2, option B); ending or un-ending a contract by PATCH and removing positions through changes/apply need delete (S1); a carry-over without accounts view answers 404 (N1); a failed grant save shows the stored level (N2).
