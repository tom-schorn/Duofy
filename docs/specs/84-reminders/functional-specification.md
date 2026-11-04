# Functional specification: Push reminders for due positions with tick-off from the notification

Format: 2

## Summary

On the hosted version, the backend sends a silent push signal on the morning of a due day for every open position that needs a manual payment. The Android and Windows apps fetch the details from Duofy's server and show "GEZ ist heute fällig" with "Bezahlt" and "Später". Owners set the time or switch reminders off; they can grant a household member the reminder permission so that member is reminded too and can tick it.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R9.

## Components

- [Plan](docs/components/plan.md)
- [Grant](docs/components/grant.md)
- [User account](docs/components/user-account.md)
- [Instance](docs/components/instance.md)

## Solution

- **Switch**: reminders exist only when `Duofy:Hosted` is true and `Duofy:Reminders:Enabled` is true (default false); otherwise the endpoints answer 404 and the settings hide the section.
- **Devices**: the app registers its push token (FCM on Android, WNS channel on Windows) with `PUT /api/v1/me/devices/{installationId}` after sign-in and removes it at sign-out; tokens of a deleted account are deleted.
- **Settings per person**: "Erinnerungen" on or off (default on once a device is registered) and a time (default 09:00, Europe/Berlin), in the settings page of portal and app.
- **Permission**: a new grant area `reminders` with the levels `none` and `view`; "Ansehen" means "is reminded of my due positions". The preset Partner includes it; Mitlesen, Kind and Nichts do not. Ticking from the notification still needs the grant level the app needs for ticking.
- **Due selection**: a background job of the backend runs every minute and, for each person whose time is now, selects positions of the current plan month with due day today, payment method `Transfer`, `Withdrawal` or `Other`, not ticked, plus positions postponed with "Später" to today; recipients are the owner and every member with `reminders` at `view` from that owner, each with reminders on. One reminder per position, recipient and day.
- **Silent signal**: the push payload holds only a random reminder id and a type, never a name, amount or category; FCM data message with high priority, WNS raw notification. The app fetches `GET /api/v1/reminders/{id}` (authenticated) and builds a local notification "<Name> ist heute fällig" with the buttons.
- **Actions**: "Bezahlt" calls the existing tick endpoint with the planned amount; "Später" calls `POST /api/v1/reminders/{id}/later`, which moves that reminder to the next day; tapping opens the app at the month with the tick dialog of that position. When a position is ticked by anyone, the backend sends a silent "withdraw" signal so other devices remove the notification.
- **Data protection**: the data protection text (part of #88) names Firebase Cloud Messaging and Windows Push Notification Services as processors, what they receive (device token, random id) and the agreements; the operations guide (`docs/betrieb.md`, section 5.1) names the settings (`Duofy:Reminders:*`, FCM service account file, WNS credentials) without secrets in the repository.

## Behaviour

- R1: A device registered on Android gets the signal through FCM, on Windows through WNS; the notification appears even when the app is closed.
- R2: At the person's chosen time on the due day exactly one notification per due position appears; with reminders off nothing comes; changing the time takes effect the next minute.
- R3: Positions with Lastschrift or Dauerauftrag never produce a reminder; positions with Überweisung, Abhebung and Sonstige do.
- R4: The provider payload contains only the reminder id and the type; the visible text names the position and the word "fällig"; if fetching fails (offline), the app shows "Ein Posten ist heute fällig" and opens the month on tap.
- R5: "Bezahlt" ticks with the planned amount and the notification disappears; if the tick is refused (no right, already ticked), the notification is replaced by a short message and the app opens. "Später" removes the notification and brings it back tomorrow at the person's time. A tap opens the tick dialog prefilled as in the app.
- R6: The owner and members with the reminder grant are reminded; a member without it is not; removing the grant or leaving the household stops the reminders for that member from the next run.
- R7: After a tick, no recipient gets further reminders for that position; open notifications on other devices are withdrawn.
- R8: On a self-hosted instance the settings show no "Erinnerungen", the grant area `reminders` is hidden, and the endpoints answer 404.
- R9: The data protection text names both providers, the data they receive and the agreements.

## Data and interfaces

- New tables: devices (person, installation id, platform, push token, last seen), reminder settings (person, on, time), reminders (id, position, recipient, day, state: sent, later, done).
- New grant area `reminders`.
- Endpoints: `PUT/DELETE /api/v1/me/devices/{installationId}`, `GET/PUT /api/v1/me/reminder-settings`, `GET /api/v1/reminders/{id}`, `POST /api/v1/reminders/{id}/later`.
- Settings: `Duofy:Reminders:Enabled`, FCM service account path, WNS client id and secret, all from environment or files outside the repository.
- One migration for the new tables, rolled out by the human.

External input: yes

## Acceptance criteria

- [ ] A1 (R1): Given a person with an Android and a Windows device registered, when a reminder is due, then the sender calls the FCM client for the Android token and the WNS client for the Windows channel (fake clients in tests).
- [ ] A2 (R2): Given a time of 07:30, when the job runs at 07:29, 07:30 and 07:31 Berlin time on the due day, then exactly one reminder per due position is sent, at 07:30; with reminders off none is sent.
- [ ] A3 (R3): Given five positions due today, one per payment method, when the job runs, then reminders are sent only for Überweisung, Abhebung and Sonstige.
- [ ] A4 (R4): Given a sent reminder, when the payload given to the provider client is inspected, then it contains only the reminder id and the type, and `GET /api/v1/reminders/{id}` returns the name and due text only to its recipient (others get 404).
- [ ] A5 (R5): Given a reminder, when "Bezahlt" is chosen, then the position is ticked with the planned amount; when "Später" is chosen, then the reminder is sent again on the next day at the person's time and not today.
- [ ] A6 (R5): Given a recipient without the right to tick, when "Bezahlt" is chosen, then the tick is refused with the existing code and the app shows the short message.
- [ ] A7 (R6): Given owner A, member B with `reminders` at `view` and member C without, when A's position is due, then A and B get a reminder and C does not; after the grant is removed, B gets none on the next run.
- [ ] A8 (R7): Given a reminder sent to A and B, when B ticks the position, then a withdraw signal goes to A's devices and no further reminder for the position is sent.
- [ ] A9 (R8): Given `Duofy:Hosted` false or the switch off, when the reminder and device endpoints are called, then they answer 404, the job sends nothing, and the settings and grant pages show no reminders.
- [ ] A10 (R1, R5): Given the Android app and the Windows app with fake signals, when a signal arrives, then each shows a notification with the fetched text and the buttons "Bezahlt" and "Später" (app tests), and on a real device on Staging the notification appears (recorded with a screenshot).
- [ ] A11 (R9): Given the data protection text, when it is searched, then it names Firebase Cloud Messaging and Windows Push Notification Services and the data they receive.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1, A10 | ReminderJobTests (A_due_position_reaches_the_android_token_through_fcm_and_the_windows_channel_through_wns), PushClientTests, ReminderPushTests (A_due_signal_shows_the_fetched_text_with_the_buttons); real device: open |
| R2 | A2 | ReminderJobTests (At_730_exactly_one_reminder_is_sent_at_the_chosen_minute_and_not_before_or_after, With_reminders_off_nothing_is_sent) |
| R3 | A3 | ReminderJobTests (Only_transfer_withdrawal_and_other_produce_a_reminder_not_direct_debit_or_standing_order) |
| R4 | A4 | ReminderJobTests (The_signal_holds_only_the_reminder_id_and_the_type), ReminderEndpointTests (The_text_of_a_reminder_is_served_only_to_its_recipient), PushClientTests (the FCM and WNS messages) |
| R5 | A5, A6, A10 | ReminderEndpointTests (Later_*, A_recipient_without_the_right_to_tick_is_refused_with_the_existing_code), ReminderPushTests (Paid_*, Later_*) |
| R6 | A7 | ReminderJobTests (Owner_and_member_with_the_reminder_grant_are_reminded_...), GrantsPageTests (reminder area, Partner preset) |
| R7 | A8 | ReminderJobTests (After_a_tick_a_withdraw_signal_goes_to_everyone_...), ReminderPushTests (A_withdraw_signal_removes_the_notification) |
| R8 | A9 | ReminderEndpointTests (Without_the_switch_or_on_a_self_hosted_server_...), MehrPageTests and GrantsPageTests (hidden without reminders) |
| R9 | A11 | open, stays with #88 |

## Threats

- Can someone pretend to be someone else? Device registration needs the person's sign-in; a reminder's details and actions are only served to its recipient.
- Can someone change data? Only through the existing tick endpoint and its rights; "Später" only moves the caller's own reminder.
- Can someone deny an action? A tick from the notification is a normal booking of that person, visible as such.
- Can someone read what they should not? The push provider sees only a random id; details need authentication; reminder ids are random and unguessable.
- Can someone bring it to a halt? Device registrations are limited per person; the job sends at most one reminder per position, recipient and day.
- Can someone gain rights they should not have? The reminder grant gives no right beyond being reminded; ticking still checks the existing grants.

## Non-goals

- No e-mail or browser notifications, no reminders days ahead, no per-position settings.
- No reminders on self-hosted Duofy, no relay through Plagevo.
- No automatic detection of payments from bank data.
- No iOS.

## Risks

- Android may delay data messages under battery saving: contained by high-priority FCM data messages and by the app showing the fallback text if fetching fails.
- WNS needs the store identity of the Windows app: contained by testing on Staging with the test package and documenting the store step.
- Time zones and daylight saving: contained by computing in Europe/Berlin and testing the change days.

## Open questions

None.

## Approval

Approved by the human on 2026-11-04, after the kickoff of 2026-11-04 (preset Partner includes the reminder grant). The human added: reminders must be live before the store release.
