# Customer requirements: App layout

Format: 2

## Background

The Android app shows the shared pages inside the same layout as the browser: a top bar with a menu button, a side drawer and centered dialogs. On 2026-10-23, during the phone test of the accounts, the human asked for the app to look like a proper Android app. The lead looked up Material Design 3 and how finance apps place their main action; the human then decided the details one at a time. The web version is the human's own part and stays as it is.

## Goal

The app feels like an Android app: navigation at the bottom, one round plus button for the main action of each page, and forms that fill the screen.

## Users

Every person using the Duofy app on an Android phone, several times a week, mostly to record a booking quickly.

## Requirements

R1 (must): The app has a bottom navigation bar with four destinations: Start, Plan, Buchungen and Mehr — because Material Design puts three to five top-level destinations at the bottom, where the thumb reaches them.
R2 (must): "Mehr" lists Konten, Verträge and Benutzerkonto, and sign-out — because these are visited less often than the four main destinations.
R3 (must): Plan shows a short note that the plan is coming until the plan feature exists — because the destination is decided now and the page follows later.
R4 (must): A round plus button at the bottom right does the main action of the page: on Start and Buchungen a new booking, on Konten a new account, on Verträge a new contract; pages without such an action show no button — because recording is the most frequent action and should take one tap.
R5 (must): Forms to create or change a booking, an account or a contract open full-screen in the app, with a close button top left and "Speichern" top right — because this is the Android pattern for forms and the booking and contract forms are long.
R6 (must): The top bar shows the title of the current page and no menu button; the light and dark mode switch moves to "Mehr" — because the bottom bar replaces the drawer.
R7 (must): The web version keeps its current layout, side menu and dialogs — because the web is built by the human separately.
R8 (should): The bottom bar and the plus button stay clear of the Android system bars (gesture bar and status bar) — because otherwise they are covered, as happened with the top buttons on 2026-10-21.
R9 (wont): Swipe gestures between tabs, a navigation rail for tablets, animations beyond what MudBlazor brings — because they are not needed for the first usable app.

## Constraints

- The app uses `Duofy.Shared/Routes.razor`; the web uses its own `WebRoutes.razor` with `MainLayout`. The app-only layout can therefore live in `Duofy.Shared` and be used only by `Routes.razor`.
- MudBlazor has no bottom navigation component; it is built from MudBlazor parts.
- The contracts page (spec 64) is built at the same time by another person; its plus button and full-screen dialog are wired after both are merged.

## Open questions

None.
