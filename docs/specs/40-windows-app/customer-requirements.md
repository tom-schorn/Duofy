# Customer requirements: Windows app on Tom's PC and Android app on Tom's phone

Format: 2

## Background

The app already has a Windows target (unpackaged for tests, MSIX for the Microsoft Store, issue #40) and an Android target. Since the Blazor Blueprint interface (#82) landed on main, neither has been built as a release and installed. Issue #40 was closed as not planned when V1 was stamped and reopened in the kickoff of 2026-11-04.

The human, kickoff 2026-11-04: "wir werden heute die microsoft app bauen und bei mir querladen. microsoft braucht ja eh noch einiges an zeit. So kann ich die app selbst mal testen und nutzen. das gleiche auf meinem android handy". And: "wen microsoft kostenlos is würde ich sagen wir rollen erstmal auf microsoft aus".

## Goal

Tonight the human uses Duofy as a Windows app on his PC and as an Android app on his phone, both against the live Plagevo server, with the state of main.

## Users

The human himself, daily, to use and test the app before it goes to the Microsoft Store.

## Requirements

R1 (must): The Windows app is installed on the human's PC as an MSIX package signed with a self-made test certificate, appears in the start menu and can be uninstalled cleanly — because the human chose "MSIX mit Testzertifikat" so that the store package is proven at the same time.
R2 (must): The Windows app starts, connects to the live Plagevo server and the human can sign in and work with his data — because he wants to "selbst mal testen und nutzen".
R3 (must): An Android release build of the same state of main is installed on the human's phone and connects to Plagevo — because "das gleiche auf meinem android handy".
R4 (must): Both apps show the current interface of main (Blazor Blueprint, #82) — because the human wants to test what was built, including booking only in the month and several bookings per position.
R5 (should): The steps to build and install both apps again are written down, so the next version can be installed the same way — because the store submission follows later.
R6 (wont): Submission to the Microsoft Store or Google Play — because that comes after the support is in place (#88) and needs the human's accounts.

## Constraints

- Builds of Windows and Android run one after the other, never at the same time (PC memory, 16 GB).
- Trusting the test certificate needs administrator rights on the human's PC: the human confirms that step himself.
- No change to the backend, no migrations, nothing on the live server.
- Store values in `Platforms/Windows/Package.appxmanifest` stay placeholders; a local identity for the test package is allowed.

## Open questions

None.
