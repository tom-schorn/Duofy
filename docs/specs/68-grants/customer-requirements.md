# Customer requirements: Grants

Format: 2

## Background

The original Duofy (github.com/tom-schorn/Duofy: `backend/app/models/grant.py`, `services/grants.py`, `core/permissions.py`, `core/scope.py`, `frontend/src/pages/GrantsPage.tsx`, `components/MemberSwitcher.tsx`, `lib/grants.ts`) lets each person decide, for every other member of their household and per area, what that member may do with their data. On 2026-10-24 the human decided to rebuild the rest of the original as it is, without the statement import, and to ask only real questions. In the same talk the lead proposed one central permission check instead of the original's hand-written check in every endpoint; the human agreed to take that into this specification. This builds on the household (#67). The team builds the API and the app; the web page is the human's part.

## Goal

Partners can work in each other's month, book and contracts as far as each of them allows, and the app shows whose figures are on screen.

## Users

Members of a household with more than one person, usually partners; a child who may only look at the plan.

## Requirements

R1 (must): Each person decides for every other member of their household separately, per area — Plan, Buchungen, Konten, Verträge — one of the levels Nichts, Ansehen, Anlegen, Ändern, Löschen, where each level includes the ones before; nothing is granted until the person chooses — because trust differs per person and per kind of data.
R2 (must): Only the owner sets grants on their own data; nobody, not even the founder, sets them for somebody else, and nobody grants themselves — because the data belongs to its person.
R3 (must): A grant counts only while both people are in the same household; leaving or being removed deletes every grant from and to that person, and joining again starts at Nichts — because a grant is trust inside a household.
R4 (must): With Ansehen a member sees the owner's pages of that area; with Anlegen they also add, with Ändern they also change, with Löschen they also delete; when a level is missing, the answer says which step is missing — because the levels must hold everywhere the same way.
R5 (must): Every endpoint asks one central check "may this person act on that owner's data in this area at this level?"; one's own data always passes — because a check written by hand in each endpoint can be forgotten.
R6 (must): The grants page under Haushalt shows one card per other member with presets "Partner: alles", "Mitlesen", "Kind" and "Nichts" and a choice per area; every change applies at once; it shows "eigene Auswahl" when the levels match no preset — because the common cases should take one tap.
R7 (must): The grants page shows hints, never automatic changes, where one right is of little use without another: booking without seeing the accounts, the book without seeing the plan — because a page that quietly raises a second level gives away more than was chosen.
R8 (must): The app shows a switch "whose figures" on every page while somebody granted the person at least Ansehen in some area; choosing a member makes Plan, Buchungen, Konten and Verträge show that member's data, and buttons the level does not allow are shown greyed out with the reason — because working on somebody else's month must be visible from every page.
R9 (must): Data of a person without any grant to the asker stays invisible as before: foreign ids answer "not found" — because the existence of other people's records must not leak.
R10 (wont): The household plan and household totals (#69), private positions (#41), a history of changes made by others, the area import, the web page — because they come with their own specification, in V2, or are left out.

## Constraints

- Builds on #67 (households) and on the existing owner filter of every endpoint (specs 58, 59, 64, 66).
- Issue #56 (the database layer enforces the owner filter by itself) stays its own issue; the central check of R5 must leave room for it.
- Needs a migration (table `grants`); it is applied to the human's database only after the yes.

## Open questions

None.
