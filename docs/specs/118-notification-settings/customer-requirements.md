# Customer requirements: Notification settings

Format: 2

## Background

Since spec 84 Plagevo sends one kind of push notification: a position with a manual payment (transfer, withdrawal, other) is due today, once a day at a time the person sets, with the buttons "Bezahlt" and "Später". "Später" brings it back the next day without limit and without being counted. The settings are a small block under "Mehr". On 11.11.2026 the human tested it on the phone: choosing a time in the Android app is always refused ("Diese Uhrzeit gibt es nicht"), and in dark mode the buttons of the time dialog are almost unreadable (#117). The human wants a real concept: which events, when, how often a reminder may be postponed, and what is shown.

## Goal

Each person decides in one settings section which events they are notified about and when; overdue positions do not slip away through endless postponing; household members learn in one evening summary what happened.

## Users

Every person on the hosted Plagevo with the Android or Windows app, especially people in tight situations who must not miss a manual payment, and couples or families sharing a household.

## Requirements

R1 (must): A settings section "Benachrichtigungen" of its own (not a block under "Mehr") holds all notification settings, in the app and in the portal — because the human wants one clear place (11.11.2026).
R2 (must): Four switches, each on by default for every person: "Heute fällig", "Überfällig", "Vorwarnung", "Haushalt" — because the human wants all events available and on (11.11.2026).
R3 (must): Two times per person: morning (default 09:00) and evening (default 19:00); the evening can be switched off; both are chosen with the time dialog and are accepted on Android, Windows and in the browser — because one notification a day is too little and the time dialog is broken today (#117).
R4 (must): "Heute fällig" comes in the morning for every position due today with a manual payment; in the evening it comes again only for those not ticked yet — because the evening is the last chance on the due day.
R5 (must): "Vorwarnung" comes in the morning a chosen number of days (1 to 7, default 2) before the due day of a position with a manual payment — because money may need to be moved in time.
R6 (must): "Später" can be used at most three times per position and due date; the count is stored and shown at the position ("2× verschoben"); after the third time the notification offers no "Später", the position is marked overdue in the plan, and the "Überfällig" notification takes over — because postponing must not hide an unpaid position forever (11.11.2026).
R7 (must): "Überfällig" comes in the morning for every position with a manual payment whose due day has passed and that is not ticked, as long as it stays unticked — because an overdue payment needs attention.
R8 (must): "Haushalt" sends one summary at the evening time when, since the last summary, a member ticked a shared position, someone was invited or joined, a grant to the person changed, or a member booked something; only for what the person may see under the existing grants; nothing is sent when nothing happened — because the human wants to know what happened without being disturbed during the day (11.11.2026).
R9 (must): The visible text names the position and the amount, for example "Miete 850 € ist heute fällig"; the push payload itself still holds only a random id and the type — because the human wants the amount visible (11.11.2026) and the push providers must still see no content.
R10 (must): In dark mode every part of the settings and of the time dialog, including its buttons and the save confirmation, is readable — because today the dialog buttons are dark blue on dark grey (#117).
R11 (should): Existing reminder settings carry over: whoever had reminders on keeps the morning time they chose and gets the new switches on; whoever had them off gets all switches off — because nobody should lose or suddenly gain notifications without having chosen it.
R12 (wont): Notifications by e-mail or SMS, quiet hours, per-event times, a background service for the closed Windows app — because they were not asked for now.

## Constraints

- Only on the hosted version with `Duofy:Reminders:Enabled` (spec 84 R8 stays).
- Recipients and visibility follow the existing grants (`reminders` grant for position notifications, the view grants for the household summary).
- The data protection text (#88) must name that the amount is shown in the notification on the device; the providers still get no content.

## Open questions

None.
