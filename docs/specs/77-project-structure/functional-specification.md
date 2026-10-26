# Functional specification: Project structure

Format: 2

## Summary

Move and merge projects: Backend/Duofy.Backend (Api, Data, Domain as subfolders), Backend/Duofy.Schemas (former Contracts), Shared/Duofy.Shared and Shared/Duofy.ApiClient, Web/Duofy.Web, Mobile/Duofy, Tests/ with Duofy.Backend.Tests (former Api.Tests and Domain.Tests), Duofy.Shared.Tests, Duofy.ApiClient.Tests, Duofy.Web.Tests, Duofy.App.Tests. No behaviour change.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R5. R6 is out of scope.

## Components

None.

## Solution

- Move the files of Duofy.Api, Duofy.Data and Duofy.Domain into Backend/Duofy.Backend/Api, /Data and /Domain with git mv so history follows; one csproj with the union of their package references; existing namespaces stay.
- Rename Duofy.Contracts to Duofy.Schemas: folder, csproj, namespace and every using.
- Merge Duofy.Api.Tests and Duofy.Domain.Tests into Tests/Duofy.Backend.Tests; move the other test projects into Tests/.
- Duofy.slnx gets solution folders Backend, Shared, Web, Mobile, Tests; project references, InternalsVisibleTo, launch settings, user-secrets id, the EF migrations assembly, README, .workday/check-command and the scripts are updated to the new paths.
- The spec tracing tables that name moved test files are updated to the new paths.

## Behaviour

- R1–R3: the solution lists exactly 11 projects in the five folders.
- R4: the same number of tests passes as before the move (1173 or more); the API applies no new migration against a database that has all current ones.
- R5: test names stay; only their project and namespace may change.

## Data and interfaces

| Method and path | Body | Answers |
| --- | --- | --- |
| No change | – | All endpoints answer as before |

External input: no

## Acceptance criteria

- [ ] A1 (R1, R3): Given the solution, when `dotnet sln Duofy.slnx list` runs, then it lists Backend/Duofy.Backend, Backend/Duofy.Schemas, Shared/Duofy.Shared, Shared/Duofy.ApiClient, Web/Duofy.Web, Mobile/Duofy and the five test projects under Tests, and nothing else.
- [ ] A2 (R2): Given the code, when it is searched for "Duofy.Contracts", then nothing is found outside the git history.
- [ ] A3 (R4, R5): Given the restructured code, when the six former test projects' tests run in the five new ones, then all pass and their count is not lower than before the move.
- [ ] A4 (R4): Given a database with all current migrations, when the API starts, then it applies no migration, and `dotnet ef migrations list` in Duofy.Backend shows the same ids as before.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | `Tests/Duofy.Web.Tests/ProjectStructureTests.cs`: `The_solution_lists_exactly_the_eleven_projects_in_their_five_folders` (A1) |
| R2 | A2 | `Tests/Duofy.Web.Tests/ProjectStructureTests.cs`: `No_file_outside_git_mentions_the_old_name_of_the_schemas` (A2) |
| R3 | A1 | `Tests/Duofy.Web.Tests/ProjectStructureTests.cs`: `The_solution_lists_exactly_the_eleven_projects_in_their_five_folders` (A1, the solution folders) |
| R4 | A3, A4 | all five test projects pass with 1240 tests, no fewer than the 1173 before (A3); `dotnet ef migrations list` shows the same 14 ids and `dotnet ef migrations has-pending-model-changes` reports no change (A4) |
| R5 | A3 | `Tests/Duofy.Backend.Tests`: the tests of the former Api.Tests and Domain.Tests (the latter under `Domain/`) pass with unchanged names (A3) |

## Non-goals

- New namespaces for Api, Data, Domain; any change of behaviour, pages, API client or app.

## Risks

- Paths in scripts, launch settings and the user-secrets id; the full build and test run catch them.

## Open questions

None.

## Approval

Approved by the human on 2026-10-26

Decided in the check-in of 2026-10-26 after the lead explained the structure with the original Duofy as comparison.
