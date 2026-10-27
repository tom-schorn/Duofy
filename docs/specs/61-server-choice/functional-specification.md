# Functional specification: Server choice

Format: 2

## Summary

The sign-in and register pages of the app show the server at the bottom. A dialog lets the person choose the hosted service or an own address, which is checked against `/api/v1/health` and stored. The API gets a small endpoint that says which sign-in ways it offers.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R7. R8 is out of scope.

## Components

- [User account](docs/components/user-account.md)

## Solution

- **Client** (`Duofy.ApiClient`): a server store (`IServerStore`: current address, hosted address, set, reset) and a delegating handler or base-address provider so every request, including the token refresh, goes to the current address. The address is stored with `Preferences` in the app; tokens stay in `SecureStorage`.
- **App** (`Duofy/MauiProgram.cs`): the hosted address comes from `Api:BaseAddress`; on start the stored choice wins.
- **UI** (`Duofy.Shared`): a server line at the bottom of `AuthLayout` (app only), a dialog with "Plagevo" (the hosted service) and "Eigener Server" with an address field, check and error text.
- **API** (`Duofy.Api`): `GET /api/v1/instance` without sign-in, answering `{ "signInMethods": ["password"] }`; later issue 62 adds `google` by configuration.

## Behaviour

- R1: The line reads "Server: <host>" with the host of the current address; the hosted one shows "Plagevo (gehostet)".
- R2: The dialog preselects the current choice; "Plagevo" needs no address.
- R3: The app calls `GET <address>/api/v1/health` with a timeout of 10 seconds; anything other than 200 shows "Unter dieser Adresse antwortet kein Duofy-Server." and keeps the old server. A missing trailing slash is added; a path is allowed (for example a reverse proxy under /duofy/).
- R4: In release builds an `http://` address answers "Bitte eine https-Adresse eingeben."; in debug builds http is allowed.
- R5: The chosen address is stored and read on the next start; all API calls use it without restart.
- R6: After a server is taken the app calls `/api/v1/instance` and keeps the list; the sign-in page shows only the ways the server offers (today only e-mail and password, so nothing visible changes).
- R7: When the address changes, stored tokens are removed before the new address is used.

## Data and interfaces

| Method and path | Body | Answers |
| --- | --- | --- |
| `GET /api/v1/instance` | – | `200 { signInMethods: string[] }`, no sign-in needed |

External input: yes

## Acceptance criteria

- [x] A1 (R1): Given a fresh app with Api:BaseAddress https://api.plagevo.de/, when the sign-in page is shown, then its bottom line says the hosted server is used.
- [x] A2 (R2, R3): Given the server dialog, when the person enters an address whose /api/v1/health answers 200, then the address is taken and the bottom line shows its host.
- [x] A3 (R3): Given the server dialog, when the entered address answers 404 or does not answer within the timeout, then the text "Unter dieser Adresse antwortet kein Duofy-Server." is shown and the old server stays.
- [x] A4 (R4): Given a release build, when http://192.168.2.67:5080 is entered, then the answer is "Bitte eine https-Adresse eingeben." and nothing is checked; given a debug build, the same address is checked.
- [x] A5 (R5): Given an own server was taken, when the server store is created again from the same preferences, then it returns that address, and the next API request goes to it.
- [x] A6 (R6): Given the API, when GET /api/v1/instance is called without a token, then the answer is 200 with signInMethods ["password"].
- [x] A7 (R7): Given stored tokens and a different server is taken, when the change is saved, then the token store is empty.
- [x] A8 (R1): Given the web version, when the sign-in page is shown, then there is no server line.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1, A8 | Tests/Duofy.Shared.Tests/ServerChoiceTests.cs: A_fresh_app_says_at_the_bottom_that_the_hosted_service_is_used, The_web_version_has_no_server_line; Tests/Duofy.App.Tests/ServerAddressTests.cs: The_release_app_uses_plagevo_as_the_hosted_service, A_debug_build_keeps_the_local_api |
| R2 | A2 | Tests/Duofy.Shared.Tests/ServerChoiceTests.cs: An_own_address_that_is_taken_shows_its_host_in_the_line, The_dialog_preselects_the_hosted_service_and_needs_no_address_for_it, The_dialog_preselects_an_own_server_with_its_address |
| R3 | A2, A3 | Tests/Duofy.ApiClient.Tests/ServerSelectorTests.cs: An_address_whose_health_check_answers_200_is_taken, An_address_that_answers_404_is_refused_and_the_old_server_stays, An_address_that_does_not_answer_in_time_is_refused_and_the_old_server_stays; Tests/Duofy.Shared.Tests/ServerChoiceTests.cs: An_address_without_a_server_shows_the_text_and_the_old_server_stays |
| R4 | A4 | Tests/Duofy.ApiClient.Tests/ServerSelectorTests.cs: Without_https_the_answer_is_needs_https_and_nothing_is_checked, In_debug_builds_an_http_address_is_checked_and_taken; Tests/Duofy.Shared.Tests/ServerChoiceTests.cs: An_address_without_https_shows_the_https_text |
| R5 | A5 | Tests/Duofy.ApiClient.Tests/ServerStoreTests.cs: A_taken_server_comes_back_when_the_store_is_created_again_from_the_same_preferences, The_next_request_goes_to_the_taken_server_without_a_restart, The_token_refresh_goes_to_the_same_server_as_the_request |
| R6 | A6 | Tests/Duofy.Backend.Tests/InstanceTests.cs: Instance_answers_without_a_token_with_the_password_sign_in; Tests/Duofy.ApiClient.Tests/ServerSelectorTests.cs: After_a_server_is_taken_the_sign_in_methods_are_kept |
| R7 | A7 | Tests/Duofy.ApiClient.Tests/ServerSelectorTests.cs: Taking_a_different_server_removes_the_stored_tokens |

## Threats

- Can someone pretend to be someone else? A malicious server could ask for the password; the person chooses the server knowingly and release builds require https.
- Can someone change data? No new write endpoint; the instance endpoint is read-only.
- Can someone deny an action? Not affected; nothing is changed on a server.
- Can someone read what they should not? Tokens are removed when the server changes, so they never reach another server; the instance endpoint reveals only the sign-in ways.
- Can someone bring it to a halt? The health check has a timeout; the instance endpoint is small and falls under the normal limits.
- Can someone gain rights they should not have? No; the endpoint grants nothing.

## Non-goals

- Google sign-in, several stored servers, a server list, the server choice in the web version, discovering servers on the network.

## Risks

- Changing the base address at run time touches the token refresh; it must go to the same server as the request.

## Open questions

None.

## Approval

Approved by the human on 2026-10-23

Decided on 2026-10-21 (card P-003, issue 61 text) and on 2026-10-23 (hosted address from configuration). The lead's additions (timeout of 10 seconds, paths allowed, tokens removed on change, the endpoint /api/v1/instance) follow from those decisions.

Amendment 2026-10-27 (human, kickoff): the hosted service is called Plagevo and lives at https://api.plagevo.de. The release app uses it as its default server (Api:BaseAddress in Mobile/Duofy/appsettings.json); debug builds keep the local API from appsettings.Debug.json. The server line reads "Server: Plagevo (gehostet)", the dialog offers "Plagevo" and "Eigener Server"; an own server is still entered by its address.
