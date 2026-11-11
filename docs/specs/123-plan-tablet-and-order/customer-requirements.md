# Customer requirements: Plan on tablet and carry-over order

Format: 2

## Background

On 11.11.2026 the human found on his tablet that the bookings table of the personal plan is too wide and cannot be swiped, while the same table in the household plan fits; and that the carry-over from the previous month stands at the top of the bookings although the newest bookings stand at the top and the carry-over belongs to the 1st of the month. Spec 101 (responsive widths) required no sideways page scroll except real tables, which must then scroll.

## Goal

Every plan page is usable on phone and tablet, and bookings are listed strictly newest first with the carry-over on the 1st.

## Users

Every person using the app or portal on a tablet or phone.

## Requirements

R1 (must): The bookings table of the personal plan fits the width at 360, 412, 600, 800 and 840 px or scrolls sideways inside its own area by swiping, exactly like the household plan's table — because the human cannot use it on his tablet (11.11.2026).
R2 (must): Bookings are sorted newest first by booking date in the personal and the household plan; the incoming carry-over ("Übertrag aus" the previous month) counts as booked on the 1st of the month and stands at the end of the month's list; the outgoing carry-over ("Übertrag nach" the next month) keeps its booking date at the end of the month and therefore stands at the top — because the carry-over happens on the 1st (11.11.2026).
R3 (must): The personal plan and the household plan use the same table component with the same layout rules — because components are reused (human's standing rule).

## Constraints

- Checks are measured at the widths of spec 101 plus 800 px (the human's tablet, Lenovo TB330FU, portrait).
- No new dependency.

## Open questions

None.
