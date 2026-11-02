# Functional specification: Where bookings live, the bottom bar, positions and booking kinds

Format: 2

## Summary

The bottom bar gets the four targets of the human. The page Buchungen goes; `/buchungen` leads to the current month, new bookings start from the month, and every account gets its movements with edit and delete. The dialog also offers ticked positions, and a position shows its bookings. All booking lists carry a coloured word for the kind, and the lists of a month name what a booking was for.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R10; R11 is out of scope.

## Components

- [Booking](docs/components/booking.md)
- [Plan](docs/components/plan.md)
- [Account](docs/components/account.md)
- [Household](docs/components/household.md)

## Solution

- **Navigation**: `AppDestinations.All` holds the four targets; Mehr gets an entry for Sparen and keeps Verträge. `/haushaltsplan` is a new page around `HouseholdPlanView`; it does not touch the switch "wessen Zahlen". The side bar keeps Haushalt at `/haushalt` (assumption, reported to the lead), and that page links to the household plan.
- **Month**: `/plan/{y}/{m}?buchen=1` opens the booking dialog once and takes the parameter out of the address. The start page and its plus navigate there.
- **Account**: `/konten/{id}` lists the movements of a month by date, newest first, with arrows, sums, edit and delete. It has no way to add a booking.
- **Position**: `PlanView.OfferedPositions` offers ticked positions too, marked as ticked. `PositionBookings` is a closed `details` element on the row with the bookings of that position and a button that opens the dialog with this position chosen.
- **Kinds**: `BookingKindBadge` draws a Radzen badge with the word. `MonthBookingRow` carries kind, purpose and sign. The household answer gets kind, purpose and the lines with their position, for bookings the viewer may see anyway.

## Behaviour

- R1: The bar has the links `/`, `/plan`, `/haushaltsplan`, `/mehr`, in this order, with the words Start, Plan, Haushalt, Mehr. Mehr lists Sparen and Verträge; on those pages Mehr lights up.
- R2: With a household, `/haushaltsplan` shows the household plan of the current month, also for a household of one, and the arrows change month at `/haushaltsplan/{y}/{m}`. Without a household the address becomes `/haushalt`, which offers to found or to paste an invitation. Haushalt lights up on both.
- R3: No entry Buchungen in the bottom bar, side bar or on Mehr. `/buchungen` replaces itself with `/plan/{y}/{m}` of today.
- R4: "Neue Buchung" on the start page and the plus of the app on the start page go to `/plan/{y}/{m}?buchen=1` of today. Without the grant to book they stay locked as before. Back does not open the dialog again.
- R5: A row shows the date, kind, purpose, note, signed amount, edit and delete. Delete asks first. Closed accounts open the same way, because their bookings remain. The grants of the page Buchungen apply unchanged.
- R6: The dialog still has date and plan month; `BookingValidator` does not change.
- R7: A ticked position stays selectable, labelled "(abgehakt)". Booking on it does not change the tick. Removing the tick still only removes the booking the tick created. Category and pot of a line remain free.
- R8: Every position row of the plan tab shows "n Buchungen"; opened, it lists date, purpose or category, and the amount of this line (not the whole booking). The household plan shows the same without the button, only for bookings the viewer may see.
- R9: Einnahme green, Ausgabe red, Umbuchung blue, Übertrag yellow, each with the word in the row; income has a plus and expense a minus sign in front of the amount. Applies to Letzte Buchungen, the tab Buchungen and the movements at the account, for a person and a household.
- R10: The label of a row is the position's name, otherwise the category, otherwise the note, otherwise the word of the kind; a split booking adds "aufgeteilt"; a transfer of a person names its accounts. Below stand date and account or first name.

## Data and interfaces

No new endpoint, no migration. `HouseholdBookingResponse` gets three optional fields: `kind`, `purpose` and `lines` (`positionId`, `amount`); existing clients ignore them. `GET /api/v1/bookings` is used with `accountId` by the account page.

External input: yes

The input is the id in `/konten/{id}` and `buchen` in the address; the id is only passed to the API, which checks the owner.

## Acceptance criteria

- [x] A1 (R1): Given a phone, when any page is open, then the bar shows Start, Plan, Haushalt, Mehr, and Mehr leads to Sparen and Verträge.
- [x] A2 (R1): Given a width of 360 px, when the bar is measured in the browser, then no label is cut and every target is at least 44 px tall.
- [x] A3 (R2): Given a household, when Haushalt is chosen, then the household plan of the current month shows and Haushalt is marked.
- [x] A4 (R2): Given no household, when `/haushaltsplan` opens, then the address becomes `/haushalt` with the offer to found or join.
- [x] A5 (R3): Given any menu, when it is read, then it has no entry Buchungen; given `/buchungen`, then the address becomes the current month.
- [x] A6 (R4): Given the start page, when "Neue Buchung" is chosen, then the current month opens with the booking dialog, with this plan month, and the parameter is gone from the address.
- [x] A7 (R5): Given an account with a booking, when `/konten/{id}` opens, then the booking of that account shows and edit and delete work; no button adds a booking.
- [x] A8 (R6): Given a booking dated 25 September in the dialog, when October is chosen, then both are sent separately.
- [x] A9 (R7): Given a ticked position GEZ, when a second booking with the category Mahnkosten is saved on it, then it counts in the booked sum and the position stays ticked.
- [x] A10 (R8): Given a position with two bookings, when its row is opened, then both show with date, purpose and the amount of the line.
- [x] A11 (R9): Given one booking of each kind and a carry-over, when a list shows them, then each row has its word in its own colour.
- [x] A12 (R10): Given a booking on a position, when "Letzte Buchungen" shows it, then the row names the position.
- [x] A13 (R9): Given the household plan with bookings of a member, when the tab Buchungen shows them, then kind and purpose show.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | `Tests/Duofy.Shared.Tests/SideNavigationTests.cs`: `The_bottom_bar_has_the_four_targets_of_the_human` |
| R1 | A2 | Browser pass, pictures named in the report; `Tests/Duofy.Shared.Tests/NarrowLayoutStyleTests.cs`: `All_bottom_bar_labels_fit_on_a_narrow_phone` |
| R2 | A3, A4 | `Tests/Duofy.Shared.Tests/HouseholdPlanPageTests.cs`: `The_target_household_opens_the_plan_of_the_current_month`, `A_household_of_one_also_opens_its_plan`, `The_arrows_stay_in_the_household_plan`, `Without_a_household_the_target_leads_to_founding_or_joining` |
| R3 | A5 | `Tests/Duofy.Shared.Tests/AccountBookingsPageTests.cs`: `The_old_address_leads_to_the_current_month`, `The_page_has_no_way_to_add_a_booking_and_names_the_account`; `Tests/Duofy.Shared.Tests/SideNavigationTests.cs`: `No_menu_has_an_entry_for_bookings` |
| R4 | A6 | `Tests/Duofy.Shared.Tests/MonthPageTests.cs`: `The_address_with_buchen_opens_the_dialog_once_and_clears_itself`; `Tests/Duofy.Shared.Tests/StartPageTests.cs`: `A_new_booking_goes_to_the_current_month` |
| R5 | A7 | `Tests/Duofy.Shared.Tests/AccountBookingsPageTests.cs` (the former tests of the page Buchungen) |
| R6 | A8 | `Tests/Duofy.Shared.Tests/BookingDialogPlanTests.cs`: `Choosing_the_next_month_loads_its_positions_and_sends_it_as_plan_month` |
| R7 | A9 | `Tests/Duofy.Shared.Tests/BookingDraftPlanTests.cs`: `Ticked_positions_stay_on_offer`; `Tests/Duofy.Shared.Tests/BookingDialogPlanTests.cs`: `A_second_booking_can_go_on_a_ticked_position_with_its_own_category`; `Tests/Duofy.Backend.Tests/PlanTests.cs`: `A_second_booking_with_another_category_counts_on_a_ticked_position` |
| R8 | A10 | `Tests/Duofy.Shared.Tests/MonthPageTests.cs`: `A_position_lists_the_bookings_that_belong_to_it`, `A_position_opens_the_dialog_with_itself_chosen` |
| R9 | A11 | `Tests/Duofy.Shared.Tests/MonthPageTests.cs`: `Booking_lists_name_the_kind_as_a_word`; colour: browser pass |
| R10 | A12 | `Tests/Duofy.Shared.Tests/MonthPageTests.cs`: `Booking_lists_name_the_kind_as_a_word` |
| R9 | A13 | `Tests/Duofy.Backend.Tests/HouseholdPlanTests.cs`: `Household_bookings_carry_kind_purpose_and_lines` |

## Threats

- Can someone pretend to be someone else? No new sign-in path; `/konten/{id}` uses the session.
- Can someone change data? Only through the existing booking endpoints with their checks.
- Can someone deny an action? Existing logs stay.
- Can someone read what they should not? An account id of someone else gives an empty or refused answer from the API. The household answer adds details only to bookings that the viewer may already see (spec 68); notes stay out.
- Can someone bring it to a halt? The household query loads category names of bookings already loaded.
- Can someone gain rights they should not have? No access check changes. `buchen=1` only opens a dialog that the same grant locks.

## Non-goals

- Changing rules of `BookingValidator`, also not the question of far plan months (spec 66 R7 applies).
- Removing the booking-like dialogs of savings and debts; they are payments of their own pages (specs 29 to 31).
- Splitting a position itself.

## Risks

Red, green, blue and yellow Radzen badges must reach 4.5:1 in both themes; this is measured in the browser pass.

## Open questions

None.

## Approval

Approved by the human on 2026-11-01 through his decisions (P-072, P-073, issue #51), which the lead passed on word by word; he has not read this text himself.
