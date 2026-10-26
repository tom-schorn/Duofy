# Functional specification: Owner filter in the database layer

Format: 2

## Summary

A request-scoped "visible owners" set (the signed-in person plus household members who granted anything) feeds EF Core global query filters on every person-owned entity and a SaveChanges check on every added or changed one. Places that need more switch the filter off through one named helper. The central check of #68 still decides areas and levels.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R5. R6 is out of scope.

## Components

- [Grant](docs/components/grant.md)
- [User account](docs/components/user-account.md)

## Solution

- **Owner context** (`Duofy.Data`): a scoped `IOwnerContext` with `CurrentUserId` (null without sign-in), `VisibleOwners` (current user plus members of the same household with any grant ≥ `view` to them), `WritableOwners` (current user plus members with any grant ≥ `create`) and a `Bypass` flag. The API fills it once per request from the token and one grants query.
- **Query filters**: `DuofyDbContext` applies `HasQueryFilter(e => ctx.Bypass || ctx.VisibleOwners.Contains(e.OwnerId))` to accounts, bookings, commitments, plans and owner-bound categories; dependent rows (booking lines, positions) are filtered through their parent with the same rule. Shared categories without owner stay visible to everybody signed in.
- **Save check**: a `SaveChanges` interceptor refuses added or modified owner-bound entities whose owner is not in `WritableOwners` with `OwnerViolationException`, which the API maps to 404 and logs with entity type and ids, never with field values.
- **Explicit bypass**: `using (ownerContext.Bypass("<reason>"))` opens a scope and logs the reason at debug level; used only in account deletion, household plan composition and member-month creation (#69), migrations and startup checks. A test lists every call site and fails when a new one appears without being added to the list.

## Behaviour

- R1: A query without any `Where` on the owner returns only rows of visible owners; the existing explicit owner conditions stay and narrow further.
- R2: Without sign-in `VisibleOwners` is empty and every filtered query returns nothing.
- R3: Adding a booking with `OwnerId` of a stranger throws before anything is written; the transaction rolls back.
- R4: Every bypass names its reason; outside a bypass scope the filter is always on.
- R5: The proof test seeds two people without grants and queries every filtered set without an owner condition.

## Data and interfaces

| Method and path | Body | Answers |
| --- | --- | --- |
| No new endpoints | – | Existing endpoints answer as before; a refused save answers `404` |

External input: no

## Acceptance criteria

- [x] A1 (R1, R5): Given Anna and Ben without any grant, each with an account, a booking, a contract and a plan, when Ben's request context runs a query on each of these sets without an owner condition, then it returns only Ben's rows.
- [x] A2 (R1): Given Anna granted Ben plan view in the same household, when Ben's request context lists plans without an owner condition, then Anna's and Ben's plans are returned; given Clara of another household, then none of Clara's.
- [x] A3 (R2): Given no signed-in person, when the accounts, bookings, contracts and plans are queried, then every result is empty.
- [x] A4 (R3): Given Ben's request context, when a booking with Anna's owner id is added without any grant and saved, then OwnerViolationException is raised and the booking does not exist; with Anna's book create grant it is saved.
- [x] A5 (R4): Given the solution, when a test collects every call of the bypass helper, then the list equals the approved list (account deletion, household plan, member months, migrations, startup checks).
- [x] A6 (R1, R3): Given the whole API test suite, when it runs with the filter and the save check on, then all existing tests stay green.
- [x] A7 (R3): Given Ben with only plan view on Anna, when his request context adds, changes or removes a position of Anna's plan or a line of her booking, or removes her account, then OwnerViolationException is raised and nothing changes; a built-in category (no owner) cannot be given an owner by anyone.
- [x] A8 (R4): Given the solution, when a test collects every ExecuteUpdate and ExecuteDelete call in the product code, then the list equals the approved list, and each listed call sits behind IAccess.
- [x] A9 (R1): Given a grant row from Clara of another household to Ben, when Ben's request context lists plans without an owner condition, then none of Clara's plans are returned.
- [x] A10 (R1): Given one request with ?owner=, when it is served, then the grants are read once and both the owner context and IAccess use that one result.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1, A2, A6, A9, A10 | `Tests/Duofy.Backend.Tests/OwnerFilterTests.cs`: `Without_any_grant_bens_context_sees_only_his_own_rows_in_every_set`, `With_annas_plan_view_ben_lists_annas_and_his_plans_but_none_of_claras`, `A_grant_row_from_clara_of_another_household_shows_ben_none_of_her_plans`, `One_request_for_annas_month_reads_the_grants_once`; the whole `Duofy.Api.Tests` project with filter and save check on |
| R2 | A3 | `Tests/Duofy.Backend.Tests/OwnerFilterTests.cs`: `Without_a_signed_in_person_every_filtered_set_is_empty` |
| R3 | A4, A6, A7 | `Tests/Duofy.Backend.Tests/OwnerFilterTests.cs`: `Without_a_grant_a_booking_with_annas_owner_is_refused_and_not_stored`, `With_annas_book_create_grant_the_same_booking_is_stored`, `With_only_view_ben_reads_annas_account_but_cannot_save_a_change_to_it`, `With_only_view_ben_cannot_take_annas_account_over_by_changing_its_owner`, `With_only_plan_view_ben_changes_nothing_of_anna`, `Nobody_can_give_a_built_in_category_an_owner`; `Tests/Duofy.Backend.Tests/OwnerViolationAnswerTests.cs`: `A_refused_save_answers_404_owner_not_found_and_logs_type_and_ids`; the whole `Duofy.Api.Tests` project with filter and save check on |
| R4 | A5, A8 | `Tests/Duofy.Backend.Tests/OwnerBypassListTests.cs`: `Every_bypass_in_the_product_code_is_on_the_approved_list`; `Tests/Duofy.Backend.Tests/OwnerFilterTests.cs`: `Inside_a_bypass_everything_is_visible_and_afterwards_the_filter_is_back`, `With_plan_view_ben_opens_a_month_of_anna_that_does_not_exist_yet`; `Tests/Duofy.Backend.Tests/OwnerBulkListTests.cs`: `Every_bulk_update_and_delete_in_the_product_code_is_on_the_approved_list`, `Every_approved_bulk_write_on_owned_data_sits_behind_the_central_check` |
| R5 | A1 | `Tests/Duofy.Backend.Tests/OwnerFilterTests.cs`: `Without_any_grant_bens_context_sees_only_his_own_rows_in_every_set` |

## Non-goals

- PostgreSQL row-level security.
- Filters per area or level (the central check of #68 stays responsible).
- New endpoints or answers.

## Risks

- Performance: one grants query per request and an `IN` list per filtered query; a household has few members.
- A legitimate path forgotten in the bypass list breaks visibly in tests instead of leaking silently.

## Open questions

None.

## Approval

Approved by the human on 2026-10-24

Presented in the lead session on Saturday 2026-10-24; builds on #68 and comes right after it.

Amendment 2026-10-25 (final review round 4 and card P-016, decided by the human, option A): the save check also covers Deleted entries and child rows (positions, booking lines) through their parent, and treats a missing original owner as not writable (S2); bulk ExecuteUpdate/ExecuteDelete call sites are listed in a test (S2); the household condition gets its own test (S3); the grants are read once per request for both the owner context and IAccess (S4).
