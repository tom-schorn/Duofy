# Functional specification: Email confirmation and password reset

Format: 2

## Summary

A setting `Hosted` switches mail on: then registration needs a confirmed address, forgotten passwords are reset by mail, email changes are confirmed by mail, and invitations can be mailed to existing confirmed accounts. Without it there is no mail at all; the administrator creates reset links instead. Mail is sent with MailKit 4.18.1 through SMTP.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R6. R7 is out of scope.

## Components

- [User account](docs/components/user-account.md)
- [Instance](docs/components/instance.md)
- [Household](docs/components/household.md)

## Solution

- **Configuration**: `Duofy:Hosted` (bool, default false); `Duofy:Mail:Host`, `Port`, `User`, `Password`, `From`, `UseTls` (default true). With `Hosted` true and a missing `Host`, `Port`, `From` or `Duofy:PublicUrl`, the API stops at start with the log line naming the missing setting.
- **Mail**: one service `IMailSender` with an SMTP implementation (MailKit 4.18.1, MimeKit with it) and a test double that records mails; texts in German, plain text plus a simple HTML part with the link; the link base is `Duofy:PublicUrl`, which a hosted server needs at start like the mail settings.
- **Data**: Identity's `email_confirmed` is used; the migration sets it to true for every existing user. Self-hosted reset links: table `password_reset_links` (id, user id, token hash, created by, expires at, used at).
- **API** (`Duofy.Api`): endpoints below; Identity's data protection token lifetimes: confirmation 24 hours, reset 1 hour; `/instance` gains `hosted`. Sign-in of an unconfirmed account on a hosted server answers `403 email_not_confirmed`. Requests for mails are limited per address to one every five minutes; a limited request answers as if it had been sent.
- **Client and pages**: sign-in page with "Passwort vergessen" (hosted: form for the address; self-hosted: the note "Bitte wende dich an die Person, die diesen Server betreibt."), the hint for unconfirmed accounts with "Link erneut senden", pages `/bestaetigen`, `/passwort-neu` and `/email-bestaetigen` reading their token from the address; page Benutzerkonto with "E-Mail ändern"; admin page (self-hosted) with "Passwort zurücksetzen" (email field, link, QR code, share); invitation dialogs (hosted) with an optional email field and the message "Wenn zu dieser Adresse ein bestätigtes Konto gehört, ist die Einladung unterwegs."; the licenses page lists MailKit and MimeKit.

## Behaviour

- R1: `Hosted` false → `IMailSender` is never called and the mail endpoints answer 404.
- R2: Registration on a hosted server creates the user unconfirmed, sends the mail and answers 201 without tokens; `POST /auth/confirm-email` with user id and token confirms; a used or expired token answers `400 token_invalid`.
- R3: `POST /auth/password/forgot` answers 202 always; `POST /auth/password/reset` with email, token and new password (rules of spec 57) answers 204 and invalidates every refresh token.
- R4: `POST /admin/password-reset-links` with `{ email }` answers `201 { url, expiresAt }` or `404 user_not_found`; `POST /auth/password/reset-by-link` with token and new password answers 204 and marks the link used; a used or expired link answers `400 token_invalid`.
- R5: `POST /users/me/email` with new email and current password: wrong password `400 password_wrong`, taken address `409 email_taken`; hosted → 202 and a mail to the new address, `POST /auth/confirm-email-change` applies it and mails the old address; self-hosted → 200 and the address is changed.
- R6: Household invitations of #67 and registration invitations of #70 accept an optional `email` on hosted servers; the mail goes only to a confirmed account with that address (registration invitations: to any address, since the person has no account yet, but at most 20 per administrator and day); the answer is identical whether a mail was sent or not.

## Data and interfaces

| Method and path | Body | Answers |
| --- | --- | --- |
| `POST /api/v1/auth/confirm-email` | `{ userId, token }` | `204`; `400 token_invalid` |
| `POST /api/v1/auth/confirm-email/resend` | `{ email }` | `202` |
| `POST /api/v1/auth/password/forgot` | `{ email }` | `202` |
| `POST /api/v1/auth/password/reset` | `{ email, token, newPassword }` | `204`; `400` |
| `POST /api/v1/auth/password/reset-by-link` | `{ token, newPassword }` | `204`; `400 token_invalid` |
| `POST /api/v1/admin/password-reset-links` | `{ email }` | `201 { url, expiresAt }`; `403`; `404 user_not_found` |
| `POST /api/v1/users/me/email` | `{ newEmail, password }` | `200` or `202`; `400`; `409 email_taken` |
| `POST /api/v1/auth/confirm-email-change` | `{ userId, newEmail, token }` | `204`; `400 token_invalid` |

External input: yes

## Acceptance criteria

- [x] A1 (R1): Given Hosted true and no Mail:Host, when the API starts, then it stops and the log names Duofy:Mail:Host; given Hosted false, then it starts and POST /auth/password/forgot answers 404.
- [x] A2 (R2): Given a hosted server, when Anna registers, then one mail with a confirmation link goes to her address and signing in answers 403 email_not_confirmed; after confirming with the link, signing in succeeds.
- [x] A3 (R2): Given a hosted server and Anna unconfirmed, when she asks to resend twice within five minutes, then exactly one more mail is sent and both answers are 202.
- [x] A4 (R2): Given users that existed before the migration, when the migration has run on a hosted server, then they can sign in without confirming.
- [x] A5 (R3): Given a hosted server, when "Passwort vergessen" is sent for anna@example.de and for nobody@example.de, then both answers are 202 and only Anna gets a mail; resetting with her link sets the new password and her old refresh tokens no longer work.
- [x] A6 (R3): Given a reset link older than one hour, when it is used, then the answer is 400 token_invalid and the password is unchanged.
- [x] A7 (R4): Given a self-hosted server and administrator Tom, when he creates a reset link for anna@example.de, then he gets a URL valid 24 hours; Anna sets a new password with it; a second use answers 400 token_invalid; for an unknown address Tom gets 404 user_not_found; Ben who is not administrator gets 403.
- [x] A8 (R4): Given a self-hosted server, when the sign-in page shows "Passwort vergessen", then it says "Bitte wende dich an die Person, die diesen Server betreibt." and sends nothing.
- [x] A9 (R5): Given a hosted server, when Anna changes her email to anna@neu.de with her password, then her address is still the old one, anna@neu.de gets a confirmation mail; after confirming, she signs in with anna@neu.de and the old address got a notice.
- [x] A10 (R5): Given a self-hosted server, when Anna changes her email with her password, then it is changed at once; with a wrong password the answer is 400 password_wrong; to Ben's address 409 email_taken.
- [x] A11 (R6): Given a hosted server and household admin Anna, when she invites ben@example.de who has a confirmed account and clara@example.de who has none, then both answers are identical and only Ben gets a mail with the link.
- [x] A12 (R1): Given the mail server rejects a recipient with a message that contains the address, when the mail fails, then the log line and the logged exception contain neither the address nor the server's text, only the error and status codes.
- [x] A13 (R4): Given administrator Tom created reset links A and B for Anna, when Anna sets a new password with B, then link A answers 400 token_invalid; creating a new link also makes the older open links of that person invalid.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1, A12 | `Tests/Duofy.Backend.Tests/HostedStartTests.cs`: `A_hosted_server_without_a_mail_setting_stops_at_start_and_names_it` (A1), `The_stop_at_start_is_logged_with_the_missing_setting` (A1), `A_port_that_is_no_port_counts_as_missing`, `A_self_hosted_server_starts_without_mail_settings_and_has_no_mail_endpoints` (A1), `A_self_hosted_server_never_sends_mail`, `The_instance_says_whether_it_is_hosted`; `Tests/Duofy.Backend.Tests/MailDeliveryTests.cs`: `Forgot_answers_before_the_mail_is_sent`, `A_failing_mail_server_is_logged_and_the_next_mail_still_goes_out`, `With_encryption_set_the_connection_is_never_left_unencrypted`, `A_rejected_recipient_is_logged_with_the_codes_only_and_never_with_the_address_or_the_server_text` (A12), `Any_other_mail_failure_is_logged_with_its_type_only` (A12); `Tests/Duofy.Shared.Tests/LicensesPageTests.cs`: `The_page_shows_the_mit_notice_of_the_mail_packages`, `The_listed_mailkit_version_is_the_one_the_api_pins` |
| R2 | A2, A3, A4 | `Tests/Duofy.Backend.Tests/EmailConfirmationTests.cs`: `A_new_account_gets_one_confirmation_mail_and_signs_in_only_after_confirming` (A2), `An_unconfirmed_account_with_the_wrong_password_looks_like_any_failed_sign_in`, `A_used_or_changed_confirmation_token_is_invalid`, `A_confirmation_link_lives_24_hours_and_a_reset_link_one`, `An_expired_confirmation_link_is_invalid`, `Resending_twice_within_five_minutes_sends_exactly_one_more_mail` (A3), `Resending_to_an_unknown_or_confirmed_address_answers_the_same_and_sends_nothing`, `After_five_minutes_the_link_can_be_sent_again`, `Users_from_before_the_migration_sign_in_on_a_hosted_server_without_confirming` (A4); `Tests/Duofy.Backend.Tests/MailDeliveryTests.cs`: `Mails_to_unconfirmed_addresses_carry_no_name_typed_at_registration`; `Tests/Duofy.ApiClient.Tests/MailClientTests.cs`; `Tests/Duofy.Shared.Tests/MailPagesTests.cs`: `An_unconfirmed_account_gets_the_hint_and_can_send_the_link_once_more`, `After_registering_on_a_hosted_server_the_page_asks_to_open_the_mail`, `The_confirmation_page_confirms_with_id_and_token_from_the_address`, `A_used_link_says_so` |
| R3 | A5, A6 | `Tests/Duofy.Backend.Tests/PasswordResetTests.cs`: `Forgot_answers_202_for_everybody_and_only_an_account_gets_a_link_that_signs_out_everywhere` (A5), `A_reset_link_works_once_and_follows_the_password_rules`, `Resetting_through_the_mail_confirms_the_address_too`, `Asking_twice_within_five_minutes_sends_one_link`, `A_reset_link_past_its_lifetime_is_invalid_and_the_password_stays` (A6), `A_reset_for_an_unknown_address_is_invalid`; `Tests/Duofy.Backend.Tests/EmailConfirmationTests.cs`: `A_confirmation_link_lives_24_hours_and_a_reset_link_one` (A6, one hour); `Tests/Duofy.Shared.Tests/MailPagesTests.cs`: `On_a_hosted_server_passwort_vergessen_sends_the_address_and_says_a_link_may_be_on_its_way`, `The_new_password_page_resets_with_the_mail_token`, `A_too_short_new_password_is_named_before_sending` |
| R4 | A7, A8, A13 | `Tests/Duofy.Backend.Tests/ResetLinkTests.cs`: `The_administrator_gets_a_link_valid_24_hours_that_sets_a_new_password_once` (A7), `An_unknown_address_is_named_only_to_the_administrator` (A7), `An_expired_or_unknown_link_is_invalid_and_the_password_stays`, `The_database_keeps_only_the_hash_and_who_created_and_used_the_link`, `A_reset_link_needs_a_valid_address_and_a_long_enough_password`, `A_hosted_server_has_no_reset_links_of_the_administrator`, `Creating_a_new_link_makes_the_older_open_links_of_that_person_invalid` (A13), `Setting_a_password_with_one_link_makes_the_other_open_links_of_that_person_invalid` (A13), `Two_open_links_used_at_the_same_time_set_the_password_once` (A13); `Tests/Duofy.Backend.Tests/AdminTests.cs`: `No_admin_endpoint_answers_data_of_plans_bookings_accounts_or_contracts`; `Tests/Duofy.Backend.Tests/Domain/PasswordResetLinkTests.cs`: `Closing_marks_only_the_open_links_as_used_and_leaves_used_and_expired_ones_as_they_are` (A13); `Tests/Duofy.Shared.Tests/MailPagesTests.cs`: `On_a_self_hosted_server_passwort_vergessen_names_the_operator_and_sends_nothing` (A8), `On_a_self_hosted_server_the_administrator_creates_a_reset_link_with_qr_code_and_teilen`, `An_unknown_address_is_named_to_the_administrator`, `The_new_password_page_resets_with_the_link_of_the_administrator`, `The_new_password_page_without_a_token_says_the_link_is_incomplete_and_offers_no_button`; `Tests/Duofy.Web.Tests/WebHostTests.cs`: `The_pages_behind_mailed_links_open_without_sign_in` |
| R5 | A9, A10 | `Tests/Duofy.Backend.Tests/EmailChangeTests.cs`: `On_a_hosted_server_the_new_address_counts_only_after_confirming_and_the_old_one_is_told` (A9), `On_a_hosted_server_a_changed_link_or_a_meanwhile_taken_address_is_refused`, `On_a_self_hosted_server_the_address_changes_at_once_with_the_password` (A10; a wrong password answers 400 password_wrong, the existing code of spec 57), `On_a_self_hosted_server_the_change_signs_out_everywhere`; `Tests/Duofy.Shared.Tests/MailPagesTests.cs`: `On_a_self_hosted_server_the_account_page_changes_the_address_at_once`, `On_a_hosted_server_the_account_page_says_the_new_address_waits_for_the_link`, `A_refused_address_change_shows_the_reason`, `The_email_change_page_shows_the_new_address_and_applies_it` |
| R6 | A11 | `Tests/Duofy.Backend.Tests/InvitationMailTests.cs`: `A_household_invitation_goes_by_mail_only_to_a_confirmed_account_and_answers_the_same_either_way` (A11), `A_household_invitation_without_an_address_sends_nothing_and_a_wrong_address_is_refused`, `A_registration_invitation_with_an_address_goes_by_mail_to_any_address_at_most_20_a_day`; `Tests/Duofy.Backend.Tests/MailDeliveryTests.cs`: `Names_in_mails_lose_everything_that_makes_a_link`, `Names_in_mails_are_cut_to_60_characters`, `An_invitation_mail_shows_the_neutralised_names`; `Tests/Duofy.Shared.Tests/MailPagesTests.cs`: `On_a_hosted_server_the_invitation_can_name_an_address_and_the_dialog_says_it_may_be_on_its_way`, `On_a_self_hosted_server_the_invitation_has_no_address_field`, `On_a_hosted_server_the_admin_page_offers_no_reset_links_and_says_invitations_go_by_mail` |

## Threats

- Can someone pretend to be someone else? On the hosted version an account needs a confirmed address; reset and change links are short-lived tokens bound to the user; a self-hosted reset link is created only by the administrator and works once.
- Can someone change data? Changing the email needs the current password; a reset invalidates every refresh token.
- Can someone deny an action? Self-hosted reset links keep who created and used them; email changes notify the old address.
- Can someone read what they should not? Forgot, resend and invitation answers are the same for known and unknown addresses; only the administrator learns whether an address exists on a self-hosted server.
- Can someone bring it to a halt? One mail per address every five minutes, at most 20 registration invitation mails per administrator and day, plus the API rate limit.
- Can someone gain rights they should not have? No token raises rights; on a self-hosted server the administrator could use a reset link himself and so take over an account, which is accepted because the operator holds the database anyway; otherwise the administrator can set no password, only hand over a link the person uses.

## Non-goals

- Two-factor sign-in (#55), Google sign-in (#62).
- Mail on self-hosted servers, mail templates per instance, newsletters, reminders.
- A user list for administrators.
- The web pages (the human's part); the shared pages are reachable in the web host as they are.

## Risks

- Mail delivery depends on the provider chosen with the hosting decision (P-001); tests use the recording double, a real send is checked once by hand on the hosted server.
- Marking existing users confirmed happens only after the human's yes for the migration.

## Open questions

None.

## Approval

Approved by the human on 2026-10-24

Presented in the lead session on Saturday 2026-10-24, together with the migration. The human chose mail only on the hosted version and approved MailKit 4.18.1 (MIT, with MimeKit). The lead read "per Mail sofern User existiert" as mail only to confirmed accounts and added, for registration invitations, mail to any address with a limit of 20 per administrator and day; both were presented and accepted.

Amendment 2026-10-24 (second, by the human): links use the server's public address from the environment (`Duofy:PublicUrl`), not the address a client is connected to; a button a person may not use is either not shown or shown greyed out with a short reason, never offered and then refused.

Amendment 2026-10-25 (final review round 5, decided by the human): SMTP errors are logged without the recipient address (F1, confirms P-018 point 1); using or creating a reset link invalidates the person's other open links (F2); the wrong-password code is password_wrong as in spec 57; the threat line now says that a self-hosted administrator can take over an account through a reset link, which is accepted because the operator holds the database anyway (F11). P-018 points 2 and 3 accepted: the in-memory queue goes with the hosting decision, the test-first deviation is a lesson.
