# Functional specification: Legal texts reachable from every page

Format: 2

## Summary

A small footer line with "Impressum · Datenschutz · Nutzungsbedingungen" on every page (and directly under the form on sign-in and registration), the registration consent with links, and three optional settings for external addresses. Without settings the links go to the existing /rechtliches pages, and texts that do not exist are left out.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R5.

## Components

None.

## Solution

- Backend: GET /api/v1/legal already lists the available texts; extend its response with the optional external address per text from the settings Duofy:ImprintUrl, Duofy:PrivacyUrl, Duofy:TermsUrl (validated https at start). A text with an address counts as available.
- Shared: one LegalLinks block (footer line) used in the app shell and the auth layout; the registration consent renders the two names as links. Each link uses the external address when present, else /rechtliches/<name>; a text that is neither present nor set is left out.
- Apps: external addresses open in the system browser (decision per address in `ExternalLinks.Decide`: own surface stays, https elsewhere opens the system browser, everything else is cancelled). App links carry no `target`, because `target="_blank"` does not reach `UrlLoading`. On the registration page in the app an own text (no external address) opens as an overlay above the form, from the consent sentence and from the legal line; the form stays filled.
- Web: every legal link (line, consent, menu entries) uses the same address source (`LegalPages.HrefOf`); the line and the consent open a new tab.
- The chat consent link of F2 is not part of this (own finding).
- Plagevo ops (private repo): Duofy__ImprintUrl=https://plagevo.de/impressum/, Duofy__PrivacyUrl=https://plagevo.de/datenschutz/, Duofy__TermsUrl=https://plagevo.de/nutzungsbedingungen/ — set by the human in the .env once the texts exist.

## Behaviour

- R1: Sign-in at 360 px shows the line directly under the form.
- R2: The consent sentence has two links.
- R3: With Duofy:PrivacyUrl set, "Datenschutz" points to that address everywhere.
- R4: With no terms file and no address, "Nutzungsbedingungen" is not shown and the consent sentence names only the privacy policy.
- R5: On the web the line and the consent links open a new tab. In the app an external link opens the system browser; on the registration page an own text opens as an overlay and the filled form stays (decision of the human, 07.11.2026).

## Data and interfaces

GET /api/v1/legal: the response gains an optional "urls" object (name to https address, only for texts that have one); "documents" stays the list of names, so the instance response and older clients are unchanged. No other change.

External input: yes

## Threats

- Can someone pretend to be someone else? No new sign-in path.
- Can someone change data? No: the settings come only from the server configuration, never from a request.
- Can someone deny an action? Not relevant: nothing is written.
- Can someone read what they should not? No: the endpoint stays anonymous and returns only public addresses.
- Can someone bring it to a halt? A wrong address stops the start with a clear message instead of serving broken links.
- Can someone gain rights they should not have? No; links are rendered as plain anchors, the address must be https (no javascript: URLs).

## Acceptance criteria

- [x] A1 (R1): Given the sign-in, registration, start and plan pages, when rendered, then each shows the legal links line; on sign-in at 360 px it sits directly under the form.
- [x] A2 (R2): Given the registration page, when rendered, then the consent sentence links privacy policy and terms.
- [x] A3 (R3): Given Duofy:ImprintUrl set to https://example.org/impressum, when any legal line renders, then "Impressum" links there; given no setting, then it links to /rechtliches/impressum.
- [x] A4 (R3): Given a setting that is not an absolute https URL, when the API starts, then it stops with a message naming the setting.
- [x] A5 (R4): Given no terms file and no terms address, when the line and the consent render, then terms are not shown.
- [x] A6 (R5): Given the app and an external address, when the link is tapped, then the system browser opens.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | LegalLinksTests.A1_every_layout_shows_the_legal_line, A1_on_the_auth_pages_the_line_is_the_next_element_after_the_form |
| R2 | A2 | RegisterPageTests.A2_the_consent_links_privacy_and_terms |
| R3 | A3, A4 | LegalLinksTests.A3_an_external_address_replaces_the_page_of_the_server_for_that_text_only, A3_without_an_address_the_link_goes_to_the_page_of_the_server; LegalEndpointTests.A3_external_addresses_are_listed, A4_an_invalid_address_stops_the_start |
| R4 | A5 | LegalLinksTests.A5_missing_texts_are_left_out; RegisterPageTests.A5_the_consent_names_only_the_privacy_policy_when_there_are_no_terms |
| R5 | A6 | ExternalLinkDecisionTests (concrete addresses), ExternalLinkTests.A6_the_app_asks_the_decision_function_for_every_address (source check); LegalLinkPlacesTests (new tab on the web, no target in the app, overlay on the registration); MehrPageTests / SideNavigationTests (external address in the menu entries). Behaviour on the device only in the emulator, not yet seen |

## Non-goals

- Writing the legal texts themselves (done by Chatti as a draft for the human).
- The chat consent link (F2) and the banner (F3).

## Risks

None.

## Open questions

None.

## Approval

Approved by the human on 2026-11-07 ("Ja" to: links to the texts on plagevo.de in every footer and in the registration consent).
