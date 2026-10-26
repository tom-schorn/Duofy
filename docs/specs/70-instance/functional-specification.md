# Functional specification: Instance

Format: 2

## Summary

A one-time setup code makes the first administrator, the registration mode comes from the environment and is announced by `/instance`, administrators create, list and revoke registration invitations shown as link and QR code, the last administrator cannot delete their account, and imprint, privacy policy and terms are served from configured files to everybody and linked in the app.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R8. R9 is out of scope.

## Components

- [User account](docs/components/user-account.md)
- [Instance](docs/components/instance.md)

## Solution

- **Configuration** (`Duofy:` section, environment variables documented in `.env.example`): `SetupCode` (empty by default), `RegistrationMode` (`open`, `invite`, `closed`; default `invite`), `ImprintFile`, `PrivacyFile`, `TermsFile` (empty by default).
- **Data**: column `is_instance_admin` on users (default false); table `registration_invitations` (id, token 32 random bytes URL-safe unique, created by, optional email in lower case, created at, expires at = created + 7 days, used at, used by). Revoking deletes the row.
- **API** (`Duofy.Api`): `RegisterRequest` gains optional `setupCode` and `invitationToken`. Registration order: (1) while no user has `is_instance_admin`, a `setupCode` equal to the configured one (compared in constant time) makes the new user administrator and skips the mode check; a wrong code answers `400 setup_code_invalid` without telling whether a code is configured; (2) otherwise the mode decides: `open` passes, `invite` needs an open invitation (`403 invitation_required` without, `403 invitation_invalid` for an unknown, used, expired or other-address one), `closed` answers `403 registration_closed`; a used invitation gets used at and used by in the same transaction. `POST /users/me/claim-admin` with `{ setupCode }` makes an existing signed-in user administrator under the same rule (no administrator yet, right code), otherwise `400 setup_code_invalid`. `/instance` answers the sign-in methods, `registrationMode`, `setupPending` (true while no administrator exists and a code is configured) and the list of configured legal documents. Admin endpoints need `is_instance_admin` (`403 admin_required`). `DELETE /users/me` of the only administrator answers `409 last_admin`. Legal: `GET /legal` lists the readable documents, `GET /legal/{name}` answers the text; a file larger than 200 KB, unreadable or empty counts as missing and is logged without its path.
- **Startup**: no `SetupCode` and no administrator → one warning in the log: "Kein Einrichtungscode gesetzt: Es kann kein Admin entstehen."
- **Client and pages**: the registration page shows the field Einrichtungscode while `setupPending`, and so does the page Benutzerkonto with a button Admin werden, a notice per mode, and takes an invitation token from `?einladung=`; page `/admin` under "Mehr" only for administrators: create invitation (optional email), open invitations with link, QR code, share and revoke; page `/rechtliches/{impressum|datenschutz|nutzungsbedingungen}` readable without sign-in, plain text with line breaks, no markup executed; links on sign-in, registration and "Mehr" only for configured documents.

## Behaviour

- R1: The first registration with the right code becomes administrator; a second registration with the same code after that is an ordinary registration under the mode and gets no administrator flag.
- R2: The warning appears once per start.
- R3: In `invite` mode the registration page without `?einladung=` says "Registrieren geht auf diesem Server nur mit Einladung."; in `closed` mode "Auf diesem Server ist keine Registrierung möglich."
- R4: The link is `<PublicUrl>/registrieren?einladung=<token>` with `Duofy:PublicUrl` from the environment; without it creating an invitation answers `409 public_url_missing`; listing shows only unused, unexpired invitations, newest first.
- R5: With a second administrator, deletion would pass; since R9 offers no way to make one, the only administrator always gets `409 last_admin`.
- R6: No admin endpoint answers data of plans, bookings, accounts or contracts.
- R7: Names in the API are `imprint`, `privacy`, `terms`; any other name answers `404 legal_not_found`.
- R8: The links read Impressum, Datenschutz, Nutzungsbedingungen.

## Data and interfaces

| Method and path | Body | Answers |
| --- | --- | --- |
| `POST /api/v1/auth/register` | + `setupCode?`, `invitationToken?` | `201`; `400 setup_code_invalid`; `403 invitation_required`, `invitation_invalid`, `registration_closed` |
| `POST /api/v1/users/me/claim-admin` | `{ setupCode }` | `200`; `400 setup_code_invalid` |
| `GET /api/v1/instance` | – | `200 { signInMethods, registrationMode, setupPending, legalDocuments }` |
| `GET /api/v1/admin/invitations` | – | `200 [ { id, token, email?, expiresAt } ]`; `403 admin_required` |
| `POST /api/v1/admin/invitations` | `{ email? }` | `201`; `400`; `403` |
| `DELETE /api/v1/admin/invitations/{id}` | – | `204`; `403`; `404 invitation_not_found` |
| `DELETE /api/v1/users/me` | as before | + `409 last_admin` |
| `GET /api/v1/legal` (no sign-in) | – | `200 { documents: [...] }` |
| `GET /api/v1/legal/{name}` (no sign-in) | – | `200 { text }`; `404 legal_not_found` |

External input: yes

## Acceptance criteria

- [x] A1 (R1): Given a fresh instance with setup code "s3cret-einrichtung" and mode closed, when Anna registers with setupCode "s3cret-einrichtung", then she is administrator and /instance answers setupPending false.
- [x] A2 (R1): Given a fresh instance with setup code "s3cret-einrichtung", when Ben registers with setupCode "wrong", then the answer is 400 setup_code_invalid and no user exists.
- [x] A3 (R1): Given Anna is administrator and the mode is open, when Ben registers with setupCode "s3cret-einrichtung", then Ben is registered and is not administrator.
- [x] A4 (R2): Given no setup code and no administrator, when the API starts, then the log holds the warning once and /instance answers setupPending false.
- [x] A5 (R3): Given mode invite, when Ben registers without an invitation, then the answer is 403 invitation_required; given mode closed, then 403 registration_closed; given mode open, then 201.
- [x] A6 (R3, R4): Given mode invite and an open invitation of administrator Anna for ben@example.de, when Clara registers with its token, then the answer is 403 invitation_invalid; when Ben registers with it, then 201, and a second registration with the same token answers 403 invitation_invalid.
- [x] A7 (R4): Given administrator Anna, when she creates an invitation without email, then it is listed with an expiry 7 days ahead; when she revokes it, then it is not listed and registering with it answers 403 invitation_invalid.
- [x] A8 (R4, R6): Given Ben who is not administrator, when he lists or creates registration invitations, then the answer is 403 admin_required.
- [x] A9 (R5): Given Anna as the only administrator, when she deletes her account with the right password, then the answer is 409 last_admin and her account still exists.
- [x] A10 (R6): Given a test that walks every admin endpoint as administrator, when their responses are read, then none contains a field of plans, bookings, accounts or contracts.
- [x] A11 (R7): Given ImprintFile pointing to a readable file "Max Muster, Musterweg 1" and no other documents, when /legal and /legal/imprint are requested without sign-in, then the list is ["imprint"] and the text is returned; /legal/privacy answers 404 legal_not_found.
- [x] A12 (R7): Given PrivacyFile pointing to a missing file or a file of 300 KB, when /legal is requested, then privacy is not listed and the log names the document without the path.
- [x] A13 (R7, R8): Given configured imprint and privacy, when the app's sign-in page and "Mehr" are shown, then they link Impressum and Datenschutz but not Nutzungsbedingungen; the page /rechtliches/impressum shows the text without signing in and renders "<b>x</b>" as literal text.
- [x] A14 (R3, R1): Given setupPending true, when the registration page is shown, then it has the field Einrichtungscode; given mode invite and no invitation in the address, then it shows "Registrieren geht auf diesem Server nur mit Einladung."
- [x] A16 (R1): Given an instance without administrator and setup code "s3cret-einrichtung", when the existing user Tom calls claim-admin with "s3cret-einrichtung", then Tom is administrator; a second claim by Ben with "s3cret-einrichtung" answers 400 setup_code_invalid.
- [x] A15 (R4): Given administrator Anna on the page Admin, when she creates an invitation, then the page shows the link "<PublicUrl>/registrieren?einladung=<token>", its QR code and Teilen; for Ben the entry Admin is missing under "Mehr".
- [x] A17 (R1, R2): Given a setup code shorter than 16 characters, when the API starts, then it logs that the code is too short and offers no setup path, exactly as without a code.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1, A2, A3, A14, A16, A17 | `Tests/Duofy.Backend.Tests/SetupCodeTests.cs`: `The_right_code_makes_the_first_person_administrator_even_when_registration_is_closed` (A1), `A_wrong_code_answers_setup_code_invalid_and_creates_nobody` (A2), `Once_there_is_an_administrator_the_code_is_an_ordinary_registration_under_the_mode` (A3), `An_existing_account_becomes_administrator_with_the_code_and_a_second_claim_is_refused` (A16), `Two_registrations_with_the_code_at_the_same_time_make_only_one_administrator`, `A_code_shorter_than_16_characters_counts_as_missing_and_the_start_says_so_once` (A17), `A_code_of_exactly_16_characters_makes_the_first_administrator` (A17); `Tests/Duofy.Shared.Tests/InstancePagesTests.cs`: `While_setup_is_pending_the_registration_page_has_the_field_einrichtungscode` (A14), `While_setup_is_pending_the_account_page_offers_admin_werden_and_claims_with_the_code`; `Tests/Duofy.ApiClient.Tests/InstanceClientTests.cs` |
| R2 | A4, A17 | `Tests/Duofy.Backend.Tests/SetupCodeTests.cs`: `Without_a_code_and_without_an_administrator_the_start_logs_the_warning_once` (A4), `With_a_code_the_start_logs_no_warning`, `A_code_shorter_than_16_characters_counts_as_missing_and_the_start_says_so_once` (A17) |
| R3 | A5, A6, A14 | `Tests/Duofy.Backend.Tests/RegistrationModeTests.cs`: `The_mode_decides_a_registration_without_invitation` (A5), `Instance_announces_the_mode`; `Tests/Duofy.Backend.Tests/AdminTests.cs`: `An_invitation_for_an_address_admits_only_that_address_and_only_once` (A6); `Tests/Duofy.Shared.Tests/InstancePagesTests.cs`: `In_invite_mode_without_invitation_the_page_says_so_and_greys_out_the_button` (A14), `In_closed_mode_the_page_says_that_nobody_can_register` |
| R4 | A6, A7, A8, A15 | `Tests/Duofy.Backend.Tests/AdminTests.cs`: `An_invitation_for_an_address_admits_only_that_address_and_only_once` (A6), `An_invitation_without_address_is_listed_for_seven_days_and_revoking_deletes_it` (A7), `Somebody_who_is_not_administrator_gets_admin_required` (A8); `Tests/Duofy.Shared.Tests/InstancePagesTests.cs`: `Creating_an_invitation_shows_its_link_qr_code_and_teilen`, `Mehr_has_the_entry_admin_only_for_administrators` (A15); `Tests/Duofy.Backend.Tests/Domain/RegistrationInvitationTests.cs` |
| R5 | A9 | `Tests/Duofy.Backend.Tests/AdminTests.cs`: `The_only_administrator_cannot_delete_the_account` (A9); `Tests/Duofy.Shared.Tests/InstancePagesTests.cs`: `For_the_administrator_deleting_the_account_is_greyed_out_with_the_reason` |
| R6 | A8, A10 | `Tests/Duofy.Backend.Tests/AdminTests.cs`: `Somebody_who_is_not_administrator_gets_admin_required` (A8), `No_admin_endpoint_answers_data_of_plans_bookings_accounts_or_contracts` (A10) |
| R7 | A11, A12, A13 | `Tests/Duofy.Backend.Tests/LegalTests.cs`: `A_configured_document_is_listed_and_served_without_sign_in_and_the_others_are_not_found`, `A_missing_file_and_a_file_over_200_kb_do_not_exist_and_the_log_names_the_document_not_the_path`; `Tests/Duofy.ApiClient.Tests/LegalClientTests.cs`; `Tests/Duofy.Shared.Tests/LegalPagesTests.cs`: `The_imprint_page_shows_the_text_and_renders_markup_as_literal_text`, `Mehr_links_only_the_configured_documents_with_their_german_names` |
| R8 | A13 | `Tests/Duofy.Shared.Tests/AuthLayoutTests.cs`: `The_footer_links_only_the_configured_legal_documents`, `The_footer_shows_the_terms_as_nutzungsbedingungen_not_agb`; `Tests/Duofy.Shared.Tests/LegalPagesTests.cs`: `Mehr_links_the_terms_as_nutzungsbedingungen` |

## Threats

- Can someone pretend to be someone else? The setup code is the proof of being the operator and works only while no administrator exists; invitations bound to an address accept only that address.
- Can someone change data? Admin endpoints change only registration invitations; nobody but administrators reaches them.
- Can someone deny an action? Invitations keep who created and who used them; revoked ones are deleted on purpose.
- Can someone read what they should not? Administrators get no personal data of others; the legal endpoints serve only the configured files, never a path from the request; errors do not reveal whether a setup code is configured.
- Can someone bring it to a halt? Legal files are capped at 200 KB; registration and the setup code fall under the rate limit of the API foundation; tokens have 256 bits.
- Can someone gain rights they should not have? The administrator flag is set only by the setup code while no administrator exists; there is no endpoint that sets it.

## Non-goals

- Making or removing further administrators, changing the mode in the app.
- Sending invitations by mail (#53), Google sign-in (#62).
- Rich text, Markdown or HTML in legal texts.
- The web pages (the human's part); the shared pages are reachable in the web host as they are.

## Risks

- A self-hoster who forgets the setup code has no administrator; the startup warning and `.env.example` say how to set it.
- The migration is applied to the human's database only after the yes; existing users get no administrator flag and become administrator with claim-admin (A16).

## Open questions

None.

## Approval

Approved by the human on 2026-10-24

Presented in the lead session on Saturday 2026-10-24, together with the migration. The human chose the one-time setup code from the environment for the first administrator (replacing #33); claim-admin for existing accounts was added by the lead so the human's existing account can become administrator.

Amendment 2026-10-24 (second, by the human): links use the server's public address from the environment (`Duofy:PublicUrl`), not the address a client is connected to; a button a person may not use is either not shown or shown greyed out with a short reason, never offered and then refused.

Amendment 2026-10-25 (final review round 5, decided by the human): a setup code needs at least 16 characters, otherwise it counts as missing (F4); .env.example says so.
