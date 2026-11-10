# Functional specification: Publish duofy-dotnet as the public repository Duofy

Format: 2

## Summary

duofy-dotnet becomes the public repository "Duofy". Before that, a security engineer scans the whole history for secrets, a developer updates the README and adds SECURITY.md, and the reviewer checks both. The lead then renames the repositories and switches the visibility with the human's yes.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R5 and R7; R6 is a non-goal.

## Components

None.

## Solution

1. The security engineer runs gitleaks (pinned version) over all commits of all branches of duofy-dotnet and writes the findings into a report: each with file, commit, kind of secret and whether it still works or is a placeholder or test value. The engineer changes nothing.
2. A developer rewrites the README: what Duofy is (a finance app that plans instead of tracks), what works today (web, Android and Windows app, API, household, plan, bookings, reminders, legal pages), the project layout, how to start it locally with Docker and `.env.example`, and a short note that the old Python version is archived under Duofy-python. The developer adds SECURITY.md with the instruction to use GitHub's private vulnerability reporting and the supported version (main).
3. The reviewer checks the README against the code, SECURITY.md, and the scan report, and runs the gate.
4. A developer replaces the sender mail address and domain in all files (README.md, docs/betrieb.md, deploy/.env.example, deploy/docker-compose.yml and any other) by placeholders; the compose file gets no built-in sender address. After the review, the lead saves a backup bundle, rewrites the history of main and the branch with git filter-repo (pinned version) so that the address is replaced in every file of every commit and author and committer addresses become the GitHub noreply address, and force-pushes main with lease after the human's yes. plagevo is reconnected once with a merge of the new history that keeps plagevo's content.
5. After the human's yes on the merge card, the branch is merged to main. The lead switches on private vulnerability reporting, renames the archived "Duofy" to "Duofy-python", renames duofy-dotnet to "Duofy", makes it public and updates the local origin.

## Behaviour

- R1: a finding that is a real secret stops the publication; the lead brings it to the human. A finding that is a placeholder, a test value or a configuration key name is listed with that reason and does not stop it.
- R2: the README contains no sentence that says the project is at the very beginning and no link that presents the old Python repository as the current Duofy.
- R3: SECURITY.md exists in the repository root and names private vulnerability reporting; the repository setting is on before it is public.
- R4: the visibility changes only after R1 to R3 are met and the human said yes.
- R7: after the rewrite, no file in any commit of main and no author or committer address contains the sender domain; the backup bundle of the old main stays on the server.
- R5: the old repository keeps its content and stays archived; GitHub forwards its old address.

## Data and interfaces

New file SECURITY.md, changed README.md, a scan report in the security engineer's report message. GitHub repository settings: name, visibility, private vulnerability reporting.

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given the complete history of all branches, when gitleaks runs over it, then the report lists zero findings or each finding is marked as placeholder, test value or key name, and none as a working secret.
- [ ] A2 (R2): Given README.md on the branch, when it is searched, then it does not contain "ganz am Anfang" and does not link tom-schorn/Duofy as the current project, and it names how to start Duofy with Docker and `.env.example`.
- [ ] A3 (R3): Given the repository root on the branch, when SECURITY.md is read, then it exists and points to GitHub private vulnerability reporting; and the GitHub API reports private vulnerability reporting as enabled before the visibility changes.
- [ ] A4 (R4): Given A1 to A3 are met and the human said yes, when the GitHub API is asked for tom-schorn/Duofy, then it is the former duofy-dotnet and its visibility is public.
- [ ] A6 (R7): Given the rewritten main on GitHub, when `git log --all -p` and `git log --all --format='%ae %ce'` of a fresh clone are searched for the sender domain, then there is no match, and the working tree of the branch has no match either.
- [ ] A5 (R5): Given the renaming is done, when the GitHub API is asked for tom-schorn/Duofy-python, then it exists, is archived and holds the old Python version.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | pending |
| R2 | A2 | pending |
| R3 | A3 | pending |
| R4 | A4 | pending |
| R5 | A5 | pending |
| R7 | A6 | pending |

## Non-goals

- No version tag, no GitHub release and no store upload (that is v2.5 with the stores).
- No rewrite of the git history beyond the one decided for R7.
- No new CI pipeline, no new badges, no translation of the README.
- No change to plagevo; it keeps merging main of the renamed repository.

## Risks

- A working secret in the history: contained by the scan before publication and the human's decision.
- plagevo and other clones still point to the old address: GitHub forwards renamed repositories; the lead updates the local origin.

## Open questions

None.

## Approval

Approved by the human on 2026-11-09

In the lead session, outside a meeting, after the kickoff. Amended the same day with R7 and A6 (sender mail domain out of files and history), approved by the human in the lead session. Amended on 2026-11-10: A6 checks author and committer addresses on all refs (--all), approved by the human in the lead session.
