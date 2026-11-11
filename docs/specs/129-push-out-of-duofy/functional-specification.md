# Functional specification: Push reminders out of Duofy, kept in Plagevo

Format: 2

## Summary

All push reminder and notification code of specs 84 and 118 leaves the public Duofy in one removal commit with a migration that drops their tables; Plagevo merges Duofy main and reverts that commit, so it keeps the feature.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R4; R5 is a non-goal.

## Components

- [Plan](docs/components/plan.md)
- [Household](docs/components/household.md)

## Solution

Delete the files and registrations of specs 84 and 118 from Duofy (backend Api/Reminder*, PushClients, Domain/Reminders, Schemas/Reminders, Duofy.Push and its tests, Android/Windows push folders, Mobile push services, ApiClient reminder client, Notifications page, ReminderSettings block, ReminderTime, Mehr/Account entries, household event logging and postpone counts in plan endpoints/stores/views, the Firebase package and google-services handling in the csproj). Keep shared rules that are not push specific (PositionOpen if the plan uses it). Add a migration `RemovePushReminders` that drops the reminder, device, notification settings, postpone count and household event tables/columns. Update README, docs/betrieb.md 5.1 (removed; point to Plagevo), CHANGELOG, component sheets. One commit "remove push reminders from Duofy".

## Behaviour

- R1: searching Duofy main for Reminder, Push, Fcm, Wns, HouseholdEvent, PostponeCount finds nothing outside docs/specs and the migration history.
- R2: a database migrated to the head of today's main migrates to the new head without errors and without the tables; EF reports no pending model changes.
- R3: in a clone of Plagevo, merging the new Duofy main and reverting the removal commit gives a tree in which the reminder tests pass.
- R4: README and betrieb.md describe no push reminders for Duofy.

## Data and interfaces

Migration RemovePushReminders (drop). Endpoints /api/v1/reminders/*, /api/v1/me/devices/*, notification settings endpoints disappear from Duofy.

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given Duofy main after the change, when the source tree (excluding docs/specs and Migrations) is searched for "Reminder", "Push", "Fcm", "Wns", "HouseholdEvent" and "PostponeCount", then there is no match, and the solution builds without the Duofy.Push projects.
- [ ] A2 (R2): Given a test database migrated to the head of origin/main before the change, when the new migrations run, then they succeed, the reminder tables are gone, and EF reports no pending model changes.
- [ ] A3 (R3): Given a scratch clone of tom-schorn/plagevo, when Duofy main is merged and the removal commit is reverted, then the build succeeds and the reminder and notification tests pass.
- [ ] A4 (R4): Given README.md, docs/betrieb.md and CHANGELOG.md, when they are searched, then push reminders are not described as a Duofy feature and the CHANGELOG names the removal.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | pending |
| R2 | A2 | pending |
| R3 | A3 | pending |
| R4 | A4 | pending |

## Non-goals

- No change to mail, hosted mode, MCP.
- No history rewrite of Duofy.
- No change inside Plagevo beyond merge and revert (done by the lead).

## Risks

- Self-hosters who never used reminders lose nothing; their database drops empty tables.
- Plagevo's merge could conflict with Plagevo-only changes: the lead does the merge and revert in a clone and checks the build first.

## Open questions

None.

## Approval

Approved by the human on 2026-11-11

In the lead session, outside a meeting; repository decision: removal in Duofy, the feature stays in Plagevo.
