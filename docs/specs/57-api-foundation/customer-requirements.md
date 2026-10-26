# Customer requirements: API foundation, users and design

Format: 2

## Background

The .NET rebuild of Duofy was set up as Blazor Hybrid: the web host reads the database directly and signs people in with a browser cookie, the MAUI app shares the pages but knows no signed-in user (issue 57). The original Duofy (Python/React, github.com/tom-schorn/Duofy) has its own API that the frontend talks to.

On 2026-10-21 the human decided a new split: every component is rebuilt after the original and discussed again first; the team builds the backend and the Android app, the human builds part of the web pages to learn. Web and app share one user interface in `Duofy.Shared`, and the shared pages are split per feature between the human and the team. Before any feature, "registration, users and design must be right"; after that the lead checks how far a first usable app gets with simple means.

## Goal

A person can register, sign in, sign out, manage and delete their account in the browser and in the Android app, both talking to Duofy's own API, on a shared user interface that already looks like Duofy, and the Android app meets the Google Play rules from the first build.

## Users

People who manage their household money with Duofy, on a phone (Android app) and on a computer (browser), several times a week. During development the human tests on their own Android phone over Wi-Fi.

## Requirements

R1 (must): Web and app get and change data only through Duofy's own API; only the API knows the database — because an app from the store must never hold database credentials, and the business rules should live in one place.
R2 (must): Anybody can register with first name, last name, email and password, and must agree to the privacy policy and the terms — because registration is open to everybody (decided 2026-10-21) and the name is needed once households arrive.
R3 (must): A person can sign in and sign out in the browser and in the app, with the same API sign-in — because web and app share one account.
R4 (must): The app stays signed in after it is closed and opened again, without asking for the password — because typing the password on every start would make the app unusable.
R5 (must): A person can delete their own account with all their data, in the app and on a web page that works without the app — because Google Play requires both for apps with accounts, and the GDPR requires deletion.
R6 (should): A person can change their first name, last name and password — because the account must be manageable without help (part of issue 54).
R7 (should): A person can sign out on all devices at once — because a lost phone must not stay signed in.
R8 (should): Guessing passwords is slowed down — because registration is public and the server will be reachable from the internet (issue 25).
R9 (must): Web and app share one user interface in Duofy.Shared, built with MudBlazor, with colours and font close to the original, a light and a dark mode — because the human chose one shared UI and MudBlazor, whose Material Design fits Android.
R10 (must): Every text of the user interface is German and comes from resource files, not from the code — because Duofy should be translatable later without touching every page.
R11 (must): The Android app meets the Google Play rules from the first build: package name de.tomschorn.duofy, target API level 36, 16 KB memory pages, a signed release bundle (AAB) — because the human does not want to resubmit the app many times.
R12 (should): The app can be installed and tested on the human's Android phone over Wi-Fi against the API on the development computer — because the human offered their phone for testing.
R13 (wont): Email confirmation and "forgot password" — because there is no email system yet; they come with the hosting decision (issue 53), and the foundation must allow adding them without rebuilding.

## Constraints

- .NET 10, ASP.NET Core Identity with its built-in token endpoints (decided 2026-10-21), PostgreSQL, Entity Framework Core with the existing `Duofy.Data` project and migrations of branch `feat/data-database`.
- Code comments in German, identifiers and commits in English.
- Google Play (checked 2026-10-21): new apps must target API level 36 since 2026-08-31; apps targeting Android 15+ must support 16 KB pages; apps with accounts must offer account deletion in the app and on the web.
- The human's commitments page on `feat/data-database` stays as a file; the human rewires it to the API in the commitments feature.

## Open questions

None.
