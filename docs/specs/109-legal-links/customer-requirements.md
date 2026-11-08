# Customer requirements: Legal texts reachable from every page

Format: 2

## Background

Design check of 07.11.2026 (finding F4, `.workday/ui-recherche/2026-11-07-design-check.md`): on the hosted instance the imprint, privacy policy and terms pages show "Diesen Text gibt es auf diesem Server nicht", no page links to them, and the registration check box names privacy policy and terms without linking them. Imprint must be easy to reach from every page; a consent needs access to the text.

The human, 07.11.2026: imprint, privacy policy and terms of the hosted instance live on its website (landing page); the portal links there. Self-hosted instances keep their own text files (spec 70).

## Goal

From every page and from the registration consent one click leads to the imprint, the privacy policy and the terms.

## Users

Everyone, signed in or not.

## Requirements

R1 (must): Every page, signed in or not, shows links to imprint, privacy policy and terms in its footer area, visible without scrolling past the content on the sign-in and registration pages — because the imprint must be easy to reach and the human chose links.
R2 (must): The registration consent sentence links "Datenschutzerklärung" and "Nutzungsbedingungen" to their texts — because a consent needs access to the text.
R3 (must): Each of the three texts can be set to an external address (settings Duofy:ImprintUrl, Duofy:PrivacyUrl, Duofy:TermsUrl); when set, every link goes there; when not set, the links go to the server's own text pages as today — because the hosted instance links to its website while self-hosters keep their files.
R4 (must): A text that is neither set as an address nor available as a file shows no dead link — because a link to "gibt es nicht" helps nobody.
R5 (should): The links open in a new tab on the web and in the system browser in the apps — because leaving the app for a website should be visible, and a filled form must stay. (Changed by the human, 07.11.2026: before, "same tab on the web".) In the apps the registration form is never left: with an external address the system browser opens; without one (self-host) the own text opens as an overlay above the form.

## Constraints

- No new dependency. The addresses are validated as absolute https URLs at start; anything else stops the start with a clear message.

## Open questions

None.
