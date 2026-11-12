# Customer requirements: Move a position to the next month

Format: 2

## Background

The human cannot pay some November positions (Wiso Steuer, GitHub, noez, Lidl Connect) and wants them to move to December (Duofy#136, 12.11.2026). Neither the app nor the MCP can do that: changing the due day stays inside the month, and deleting plus re-creating loses the contract link and may duplicate the contract's own December position. `postponeCount` belongs to reminders (moving a reminder by days), not to moving a position.

## Goal

An open position can be moved to the next month in one step; the old month shows where it went, the new month shows it next to the regular positions, and both stay linked.

## Users

Every Duofy user planning a month, also a household member acting for another with grants; the assistant through the MCP (Plagevo).

## Requirements

R1 (must): An open position without bookings can be moved to the next month: in the old month it stays visible as "in <Monat> verschoben" and no longer counts; the next month gets it as an additional position "aus <Monat>", next to the contract's regular position of that month — because both payments are really due then (human, 12.11.2026: "Mitnehmen, beides fällig").
R2 (must): The moved position keeps its contract link, amount, pot, category, account and payment method; its due day stays (or the last day of the shorter month) — because it is the same payment, only later.
R3 (must): Moving is allowed with the right to edit the plan of the owner, for oneself and for a household member through the person switch; a ticked position or one with bookings cannot be moved, and the refusal says why — because moving changes the plan, and paid money must not move.
R4 (must): Moving can be undone from the old month as long as the moved position has no bookings and is not ticked — because a wrong tap must not stay.
R5 (must): A moved position can be moved again from the new month — because some bills wait more than one month.
R6 (must): Contract updates and the change notice never treat a moved position as a duplicate of the contract's regular position and never remove or merge it — because then a debt would vanish.
R7 (must): In the app the action sits in the position's menu as "In <nächster Monat> verschieben" with one confirmation naming the target month; it works on tablet and phone in the personal and the household plan — because the human's UI rules (one place, same in both plans).
R8 (must): The MCP of Plagevo gets a tool position_postpone with the same rules, and concept_and_guide says when to move and when to delete — because the assistant must do it the app's way (Duofy#136).

## Constraints

Repository: Duofy for the app and backend (R1-R7); Plagevo for the MCP tool (R8) after Duofy main is merged into plagevo. Human, 12.11.2026: option "Mitnehmen, beides fällig".
A database migration is needed; it is merged only with the human's yes.

## Open questions

None.
