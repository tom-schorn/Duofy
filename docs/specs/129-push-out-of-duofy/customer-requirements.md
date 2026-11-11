# Customer requirements: Push reminders out of Duofy, kept in Plagevo

Format: 2

## Background

Push reminders (spec 84) and notification settings (spec 118) were specified as Plagevo features: they need Firebase/WNS credentials, the hosted server and the Plagevo app, and do not work for self-hosters. They were merged into the public Duofy repository anyway and reached Plagevo through its regular merge of Duofy main. On 11.11.2026 the human decided they leave Duofy and stay in Plagevo. Mail and the hosted mode stay in Duofy (human, 11.11.2026).

## Goal

The public Duofy contains no push reminder or notification feature; Plagevo keeps it unchanged and keeps receiving Duofy's other changes by its usual merge.

## Users

Self-hosters of Duofy (a cleaner, honest feature set) and Plagevo users (no change).

## Requirements

R1 (must): Duofy main contains no push reminder or notification code: backend endpoints, job, push clients, the Duofy.Push project, the Android/Windows push parts, the API client, the settings page "Benachrichtigungen" and its entries, the household event log and postpone counter of spec 118, their tests and the Firebase package — because these are Plagevo features (human, 11.11.2026).
R2 (must): Duofy's database model and migrations stay consistent: a new migration drops the reminder tables and columns so a Duofy database that applied the earlier migrations ends without them, and the model has no pending changes — because self-hosted databases must still migrate cleanly.
R3 (must): The removal is one commit (or one merge) on Duofy main that Plagevo can revert exactly after merging Duofy main, so Plagevo keeps the feature, its migrations and tests unchanged — because Plagevo must not lose it (human, 11.11.2026).
R4 (must): Duofy's build, all test projects and its docs (README feature list, docs/betrieb.md section 5.1, CHANGELOG, component sheets) no longer mention push reminders as a Duofy feature — because the public description must be true.
R5 (wont): Moving mail confirmation or the hosted mode — because the human decided they stay in Duofy.

## Constraints

- Repository: Duofy for the removal; Plagevo keeps the feature (decided by the human 11.11.2026).
- The plan's display of overdue positions without the postpone counter stays as before spec 118 in Duofy.
- The public Duofy history keeps the old commits; nothing is rewritten.

## Open questions

None.
