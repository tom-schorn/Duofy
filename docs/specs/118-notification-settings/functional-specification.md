# Functional specification: Notification settings

Format: 2

## Summary

Notifications get their own settings section with four switches (due today, overdue, advance warning, household), a morning and an evening time and the number of warning days. Postponing is limited to three times and shown at the position. Household events arrive as one evening summary. Notifications show name and amount. The time dialog works on Android and is readable in dark mode.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R11; R12 is a non-goal.

## Components

- [Plan](docs/components/plan.md)
- [Grant](docs/components/grant.md)
- [Household](docs/components/household.md)
- [User account](docs/components/user-account.md)

## Solution

- Settings per person replace the reminder settings of spec 84: `due_today`, `overdue`, `advance_warning`, `household` (booleans, default true), `morning_time` (default 09:00), `evening_enabled` (default true), `evening_time` (default 19:00), `warning_days` (1 to 7, default 2). A migration fills them from the old settings (R11). The API endpoint for the settings returns and accepts all fields; times are accepted as "HH:mm" and "HH:mm:ss" (seconds ignored); an empty or impossible time is refused with the existing error.
- The reminder job runs every minute as today. At a person's morning time it selects: positions due today (due today), positions due in exactly `warning_days` days (advance warning), and positions past their due day and not ticked (overdue), each only with a manual payment method and only when the matching switch is on. At the evening time (if on) it selects positions due today and not ticked (due today), and builds the household summary (household). One notification per position, recipient, day and slot (morning, evening).
- Postponing: a counter per position and due date; "Später" increments it and brings the notification back the next morning; at 3 the notification is built without "Später", and the plan marks the position overdue. The plan and the position dialog show "n× verschoben" from 1 on.
- Household summary: a log of household events (tick of a shared position, invitation, joining, grant change to the recipient, booking by a member) since the last summary, filtered by what the recipient may see under the existing grants; one notification "Haushalt heute: …" with a short list; none when empty.
- The visible text is built in the app after fetching the reminder (spec 84 flow) and now includes the amount in the user's currency format; the push payload is unchanged (id and type only).
- UI: "Benachrichtigungen" becomes its own page reached from the settings ("Mehr" lists it as an entry); the four switches, the two times, the evening switch and the warning days; the time inputs use the platform time dialog; the colour scheme of the page and of native dialogs follows the theme so that dialog buttons and the confirmation are readable in dark mode.

## Behaviour

- R1: "Mehr" shows an entry "Benachrichtigungen"; the old block under "Mehr" is gone; the portal shows the same page.
- R2: a new person sees all four switches on.
- R3: choosing 04:40 in the Android time dialog and confirming stores 04:40; the page shows "Gespeichert"; a value with seconds is stored without seconds; "Löschen" in the dialog leaves the stored time unchanged and shows a short hint instead of an error.
- R4: a position due today with transfer, unticked: notification at 09:00; still unticked at 19:00: another one; ticked before 19:00: none in the evening; evening off: none in the evening.
- R5: warning days 2, position due on the 15th: notification on the 13th at the morning time.
- R6: three times "Später": count 3 stored and shown; the next notification has only "Bezahlt"; the plan shows the position as overdue; ticking it resets nothing but ends the notifications.
- R7: due on the 10th, unticked on the 11th morning: "Überfällig" notification; daily until ticked or the switch is off.
- R8: Jasmin books two bookings and ticks "Miete" during the day: at 19:00 Tom gets one summary naming both; nothing happened: no summary; a booking Tom may not see under the grants does not appear.
- R9: the text reads "Miete 850 € ist heute fällig"; the provider payload contains only id and type.
- R10: in dark mode the dialog buttons and the confirmation meet a contrast of at least 4.5:1.
- R11: a person with reminders on at 07:30 before the update has morning 07:30 and all switches on after it; a person with reminders off has all switches off.

## Data and interfaces

Changed table of reminder settings (new columns, migration from the old ones), a postpone counter per position and due date, a household event log, changed settings endpoint (all fields), reminder fetch returns the amount. Push payload unchanged.

External input: yes

## Acceptance criteria

- [ ] A1 (R1, R2): Given a new person, when the notification settings are read, then all four switches are on, morning 09:00, evening on at 19:00, warning days 2, and "Mehr" lists an entry "Benachrichtigungen" instead of the old block.
- [ ] A2 (R3): Given the settings endpoint, when the time "04:40" or "04:40:00" is sent, then 04:40 is stored; when "" or "25:00" is sent, then the request is refused and the stored time is unchanged.
- [ ] A3 (R3, R10): Given the notifications page in dark mode, when a time is chosen in the time input and confirmed, then the value sent is "HH:mm", the confirmation "Gespeichert" is shown, and the page declares a dark colour scheme for native controls.
- [ ] A4 (R4): Given a transfer position due today and unticked, when the job runs at the morning time and at the evening time, then one notification is sent at each; given it is ticked before the evening, then none is sent in the evening; given evening off, then none in the evening.
- [ ] A5 (R5): Given warning days 2 and a transfer position due in two days, when the job runs at the morning time, then one advance warning is sent; with the switch off, none.
- [ ] A6 (R6): Given a reminder postponed three times, when it comes again, then its fetch result offers no "Später", the postpone count 3 is returned with the position, and the plan marks the position overdue.
- [ ] A7 (R7): Given a transfer position past its due day and unticked, when the job runs at the morning time, then one overdue notification is sent each day until it is ticked.
- [ ] A8 (R8): Given a household where a member booked twice and ticked a shared position since the last summary, when the job runs at the recipient's evening time, then exactly one summary is sent naming those events the recipient may see; with no events, none is sent.
- [ ] A9 (R9): Given any notification, when the reminder is fetched, then the text contains the position name and the formatted amount, and the provider payload contains only the id and the type.
- [ ] A10 (R11): Given old settings "on, 07:30" and "off", when the migration runs, then the first person has morning 07:30 and all switches on, the second all switches off.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | pending |
| R2 | A1 | pending |
| R3 | A2, A3 | pending |
| R4 | A4 | pending |
| R5 | A5 | pending |
| R6 | A6 | pending |
| R7 | A7 | pending |
| R8 | A8 | pending |
| R9 | A9 | pending |
| R10 | A3 | pending |
| R11 | A10 | pending |

## Threats

- Can someone pretend to be someone else? No: settings and reminder fetch stay behind the existing authentication; a reminder id is served only to its recipient (spec 84).
- Can someone change data? Only their own settings; "Bezahlt" ticks only with the existing tick right; the postpone counter changes only through the recipient's own "Später".
- Can someone deny an action? Postpones, ticks and settings changes are logged with person and time as today.
- Can someone read what they should not? The household summary is filtered by the recipient's grants; the amount is fetched only by the authenticated recipient; providers get only id and type.
- Can someone bring it to a halt? The job sends at most one notification per position, recipient, day and slot and one summary per evening; the settings endpoint keeps the existing rate limit.
- Can someone gain rights they should not have? No new rights; the household summary and position notifications reuse the existing grants.

## Non-goals

- No e-mail or SMS notifications, no quiet hours, no per-event times.
- No background service for the closed Windows app (V2 later).
- No change to who may tick or see what (grants stay as they are).

## Risks

- Many notifications for large households: contained by the single evening summary and one notification per slot.
- Amount visible on the lock screen: the human decided it; Android's own setting to hide sensitive content still applies; named in the data protection text.

## Open questions

None.

## Approval

Approved by the human on 2026-11-11

In the lead session at night, outside a meeting, including R11 (carry-over of existing settings) and the bugs of #117.
