# Functional specification: Move a position to the next month

Format: 2

## Summary

A position menu entry and an API call move an open, unpaid position to the next month. The old month keeps it as "in Dezember verschoben" without counting it; the next month shows it as an extra position "aus November" next to the regular one. It can be undone and moved again. The Plagevo MCP gets the same as a tool.

## Customer requirements

[customer-requirements.md](customer-requirements.md): R1, R2, R3, R4, R5, R6, R7, R8.

## Components

- [Plan](docs/components/plan.md)

## Solution

- Data: position gets `moved_to_position_id` (the copy in the next month, set on the old position) and `moved_from_position_id` (set on the copy). Migration adds both nullable columns with foreign keys (on delete set null) and an index.
- API: `POST /api/v1/plans/{year}/{month}/positions/{id}/postpone` (with `?owner=` like the other position calls) creates the next month's plan if needed, copies the position (R2; due day clamped to the month's length; `changed_by_hand` true so contract sync does not overwrite it), links both, and returns both. `POST …/positions/{id}/postpone/undo` on the old position deletes the copy if it has no bookings and is not ticked, and clears the link. Refusals: 409 with a German message (ticked, has bookings, already moved, copy has bookings).
- Figures: a position with `moved_to_position_id` counts nowhere (plan figures, free amount, quotas, pots, start page, reminders, household sums); the copy counts like any position of its month.
- Contract sync and change notice (R6): positions with `moved_from_position_id` are never matched to the contract's month position, never removed by a contract change or end, and not listed as differences.
- App (R7): position menu entry "In <Monat> verschieben" (shared PositionActions, personal and household plan) with one confirmation; the old row shows "in <Monat> verschoben" with "Zurücknehmen", greyed; the copy shows "aus <Monat>". Grants as in R3 (plan edit), refusal text names the missing right like the other position actions.
- MCP (R8, Plagevo): tool `position_postpone(year, month, positionId, owner?)` and `position_postpone_undo`, calling the endpoints above; guide section "Posten verschieben oder löschen".

## Behaviour

- R1: moving GitHub 4,00 € from November: November row "in Dezember verschoben", November figures without it; December has the contract's GitHub and "GitHub aus November", December free amount lower by 4,00 €.
- R2: contract link, amount, pot, category, account, payment method equal; due day 31 in a 30-day month becomes 30.
- R3: without plan edit for the owner: refused with the missing right; ticked or with bookings: refused "Bezahlte Posten lassen sich nicht verschieben."; already moved: refused.
- R4: undo removes the December copy and November counts it again; refused when the copy has bookings or is ticked.
- R5: moving the copy from December to January works the same; each copy names only its direct origin month ("aus Dezember").
- R6: a contract amount change in December updates the regular December position and leaves the copy; ending the contract keeps the copy.
- R7: menu entry and confirmation at 412, 800 and 1024 px in both plans.
- R8: the MCP tool gives the same results and errors as the API.

## Data and interfaces

New columns `plan_positions.moved_to_position_id`, `plan_positions.moved_from_position_id` (migration). New endpoints `POST …/positions/{id}/postpone` and `…/postpone/undo`. Position responses get `movedToMonth` and `movedFromMonth` (year/month or null). MCP tools in Plagevo.

External input: yes

## Acceptance criteria

- [ ] A1 (R1, R2): Given an open contract position in November, when it is postponed, then November marks it moved and its figures exclude it, and December holds a copy with the same contract link, amount, pot, category, account and payment method next to the contract's regular December position.
- [ ] A2 (R2): Given a position due on the 31st, when it is moved into a 30-day month, then the copy is due on the 30th.
- [ ] A3 (R3): Given a member without plan edit for the owner, a ticked position, a position with bookings or an already moved one, when postpone is called, then it is refused with a German message and nothing changes.
- [ ] A4 (R3): Given a member with plan edit for the owner through the person switch (?owner=), when postpone is called, then it works on the owner's plan.
- [ ] A5 (R4): Given a moved position whose copy is unpaid, when undo is called, then the copy is gone and the old month counts the position again; given a copy with a booking, then undo is refused.
- [ ] A6 (R5): Given a copy in December, when it is postponed, then January gets a copy "aus Dezember".
- [ ] A7 (R6): Given a copy in December, when the contract's amount changes or the contract ends, then the copy stays unchanged and the change notice does not list it.
- [ ] A8 (R7): Given the personal and the household plan at 412, 800 and 1024 px, when the menu entry is used, then one confirmation names the target month, and the rows show "in <Monat> verschoben" with Zurücknehmen and "aus <Monat>".
- [ ] A9 (R8): Given the Plagevo MCP, when position_postpone and position_postpone_undo are called, then they behave as A1-A5, and concept_and_guide explains moving versus deleting.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | pending |
| R2 | A1 | pending |
| R2 | A2 | pending |
| R3 | A3 | pending |
| R3 | A4 | pending |
| R4 | A5 | pending |
| R5 | A6 | pending |
| R6 | A7 | pending |
| R7 | A8 | pending |
| R8 | A9 | pending |

## Threats

- Can someone pretend to be someone else? Owner comes from the token or the checked ?owner= grant, as for other position calls.
- Can someone change data? Only with plan edit for the owner; paid positions never move.
- Can someone deny an action? Positions keep both links; MCP calls stay audited.
- Can someone read what they should not? Responses contain only the owner's positions the caller may see.
- Can someone bring it to a halt? One copy per position; a moved position cannot be moved twice.
- Can someone gain rights they should not have? No new rights; plan edit as for editing a position.

## Non-goals

- No moving of partly paid positions or of the open remainder.
- No moving to a month other than the next one, no moving backwards.
- No merging into the regular position of the next month.
- Reminder postponing (postponeCount) stays as it is.

## Risks

Figures that forget the moved flag would count a payment twice: contained by A1 checking November and December figures and a test per figure (free, quotas, pots, start page, household sum).

## Open questions

None.

## Approval

Approved by the human on 2026-11-12 (session answers "Mitnehmen, beides fällig" and "Ja, freigeben und bauen"; migration merges only with the human's yes).
