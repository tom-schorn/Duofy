# Functional specification: App layout

Format: 2

## Summary

The Android app gets its own layout `AppLayout`: a top bar with the page title, a bottom navigation bar with Start, Plan, Buchungen and Mehr, a plus button per page, and full-screen forms. The web keeps `MainLayout` unchanged.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R8. R9 is out of scope.

## Components

None.

## Solution

- **Layout** (`Duofy.Shared/Layout/AppLayout.razor`): MudLayout with a MudAppBar (title only), the page body and a fixed bottom bar built from MudBlazor buttons with icon and label, the active destination highlighted. `Routes.razor` uses it as default layout; `WebRoutes.razor` and `MainLayout` stay untouched.
- **Page title and plus button**: a small scoped service (for example `IAppChrome`) through which a page sets its title and, optionally, the action of the plus button; the layout renders both. Pages clear the action when they are left. In the web the service has no visible effect.
- **Full-screen forms**: one place decides the dialog options; in the app the booking and account dialogs open with `FullScreen = true` and a header with close and "Speichern"; in the web they stay as they are. Detecting the app is done by a service registered only in `MauiProgram` (for example `IFormFactor`), not by checking the user agent.
- **Pages**: new `/plan` (note "Der Plan kommt bald") and `/mehr` (links to Konten, Verträge, Benutzerkonto, the light and dark mode switch, and sign-out).
- **Safe areas**: bottom bar and plus button add the bottom system inset, as the top bar already does since the fix of 2026-10-21.

## Behaviour

- R1: The bottom bar shows four destinations with icon and label; tapping one navigates there; the destination of the current address is highlighted (Konten, Verträge and Benutzerkonto highlight Mehr).
- R2: `/mehr` lists Konten (`/konten`), Verträge (`/vertraege`), Benutzerkonto (`/konto`), the mode switch and Abmelden.
- R3: `/plan` shows the note and no plus button.
- R4: Start and Buchungen open the booking dialog; Konten opens the account dialog; Verträge opens the contract dialog once spec 64 is merged; Plan, Mehr and Benutzerkonto show no plus button.
- R5: In the app, create and edit forms of bookings, accounts and (after spec 64) contracts open full-screen with close top left and Speichern top right; Speichern runs the same checks as before.
- R6: The top bar shows the page title from the page; no menu button and no mode switch in the app.
- R7: In the web, `MainLayout` with drawer, menu button, mode switch and centered dialogs is unchanged.
- R8: With gesture navigation, the bottom bar and the plus button sit above the system gesture area.

## Data and interfaces

None. No API or database change.

External input: no

## Acceptance criteria

- [x] A1 (R1): Given the app router, when a signed-in person opens /buchungen, then the page is rendered inside AppLayout with a bottom bar of exactly four entries Start, Plan, Buchungen and Mehr, and Buchungen is highlighted.
- [x] A2 (R1): Given the bottom bar, when the person taps Mehr, then the address is /mehr; when they then open /konten, then Mehr stays highlighted.
- [x] A3 (R2): Given /mehr, when it is rendered, then it shows links to /konten, /vertraege and /konto, the mode switch and Abmelden.
- [x] A4 (R3): Given /plan, when it is rendered, then it shows the note that the plan is coming and no plus button.
- [x] A5 (R4): Given /buchungen in the app, when the plus button is tapped, then the booking dialog opens; given /konten, then the account dialog opens; given /mehr, then there is no plus button.
- [x] A6 (R5): Given the app, when the booking dialog opens from the plus button, then it is full-screen with a close button and Speichern in its header; given the web, then the same dialog is not full-screen.
- [x] A7 (R6): Given the app on /konten, when it is rendered, then the top bar shows the title Konten and has no menu button.
- [x] A8 (R7): Given the web router, when /buchungen is rendered, then it uses MainLayout with the drawer and no bottom bar.
- [x] A9 (R8): Given the app layout, when it is rendered, then the bottom bar and the plus button reserve the bottom safe-area inset in their styles.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1, A2 | `Tests/Duofy.Shared.Tests/AppLayoutTests.cs`: `AppLayoutTests.Bookings_open_inside_the_app_layout_with_four_entries_and_bookings_highlighted`, `AppLayoutTests.Mehr_leads_to_mehr_and_stays_highlighted_for_konten` |
| R2 | A3 | `Tests/Duofy.Shared.Tests/AppLayoutTests.cs`: `AppLayoutTests.Mehr_lists_konten_vertraege_konto_the_mode_switch_and_sign_out` |
| R3 | A4 | `Tests/Duofy.Shared.Tests/AppLayoutTests.cs`: `AppLayoutTests.Plan_shows_the_month_and_its_plus_button_adds_a_position` (A4 replaced by spec 66, the plan page exists) |
| R4 | A5 | `Tests/Duofy.Shared.Tests/AppLayoutTests.cs`: `AppLayoutTests.The_plus_button_opens_the_booking_dialog_on_bookings`, `AppLayoutTests.The_plus_button_opens_the_account_dialog_on_konten`, `AppLayoutTests.Pages_without_a_main_action_show_no_plus_button` |
| R5 | A6 | `Tests/Duofy.Shared.Tests/AppLayoutTests.cs`: `AppLayoutTests.In_the_app_the_booking_dialog_is_full_screen_with_close_and_save_in_its_header`, `AppLayoutTests.In_the_browser_the_same_dialog_is_not_full_screen` |
| R6 | A7 | `Tests/Duofy.Shared.Tests/AppLayoutTests.cs`: `AppLayoutTests.Konten_show_their_title_in_the_top_bar_and_the_bar_has_no_menu_button` |
| R7 | A8 | `Tests/Duofy.Shared.Tests/AppLayoutTests.cs`: `AppLayoutTests.The_browser_keeps_main_layout_with_drawer_and_no_bottom_bar` |
| R8 | A9 | `Tests/Duofy.Shared.Tests/AppLayoutTests.cs`: `AppLayoutTests.Bottom_bar_and_plus_button_reserve_the_bottom_safe_area_inset` |

## Non-goals

- Swipe gestures, a navigation rail for tablets, own animations.
- Changes to the web layout, the plan itself, new features on any page.
- Wiring the contracts page before spec 64 is merged.

## Risks

- The contracts page is built in parallel; its plus button and full-screen dialog are a small follow-up once both branches are merged.
- Full-screen dialogs must still be usable with the on-screen keyboard open.

## Open questions

None.

## Approval

Approved by the human on 2026-10-23

The destinations (Start, Plan, Buchungen, Mehr), the plus button per page and full-screen forms were decided one at a time on 2026-10-23. The lead's additions (Plan shows a note until the plan exists, mode switch moves to Mehr, Konten and Verträge and Benutzerkonto under Mehr) follow from those answers.
