# Customer requirements: Household bookings only with grant

Format: 2

## Background

The month page can be shown for the whole household. It lists the recent bookings of all members. Individual bookings are personal data; the grants (area Buchungen) exist to protect them. At the check-in of 2026-10-30 the human decided the open question P-045 (decision 1, minutes `.workday/minutes/2026-10-30-check-in.md`, issue comment on #96).

## Goal

In the household month, a member's individual bookings appear only if that member granted the area Buchungen to the viewer (or the viewer is that member). For everyone else the page says "nicht freigegeben".

## Users

Members of a household with two or more people, looking at the household month (switch "Haushalt") on the overview and on the tab Buchungen.

## Requirements

R1 (must): In the household month, individual bookings of a member are listed only if the viewer is that member or that member granted the viewer at least `view` in the area Buchungen — because individual bookings are personal data that the grants protect.
R2 (must): For every member whose bookings are not listed, the page says that they are "nicht freigegeben", naming the first name — because otherwise a missing list looks like a month without bookings.
R3 (must): There is no other way for a viewer to learn the individual bookings of a member without that grant (not in the household plan response, not in another endpoint of the household) — because a hidden list is worth nothing if the data leaves the server anyway.
R4 (must): Sums, quotas and planned versus booked amounts of the household month stay as decided in the household plan specification — because the human decided only the individual bookings.

## Constraints

Decided by the human on 2026-10-30 (check-in, decision 1). No new dependency, no migration. Private positions (#41) are out of scope.

## Open questions

None.
