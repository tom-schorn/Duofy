# Component: Household

## Purpose

Who plans together, for those who want to. A household owns nothing: accounts, contracts, plans and bookings always belong to one person. It is not about what one person may do with another's data; that is the grant (#68).

## Terms

| Term | Meaning |
| --- | --- |
| Household | The group of people who plan together; a person is in at most one, and in none until they found or join one. |
| Founder | The person who founded the household and alone manages it; exactly one per household. |
| Member | A person in a household other than the founder. |
| Invitation | An offer to join as a link `<server>/einladung/<token>`, shown as QR code; whoever holds it and is in no household may join once within 14 days unless the founder revoked it. |
| Preview | What the link shows before joining: household name, inviter's name, expiry; never the members. |
| Household quotas | Targets for Bedürfnisse, Wünsche and Sparen of the household plan, separate from each person's own. |

## Data

- Household: `id`, `name` (1–100), `quota_needs`, `quota_wants`, `quota_savings` (whole percents, sum 100, default 50/30/20).
- Member: `household_id`, `user_id` (unique), `role` (`founder`, `member`), `joined_at`.
- Invitation: `id`, `household_id`, `created_by_id`, `token` (32 random bytes, URL-safe, unique), `status` (`pending`, `accepted`, `revoked`), `expires_at`, `used_by_id`, `used_at`.

## Rules

- A person is in no household until they found one, and in at most one.
- Only the founder manages the household; there are no roles to give.
- A member who leaves or is removed is in no household afterwards; no data moves.
- The founder can leave only while alone, which dissolves the household.
- Joining is possible only while the person is in no household.
- An invitation is used once; at most 10 are open per household; only the founder creates, lists and revokes them.
- On the hosted version the founder may name an address when inviting; the link is mailed only to a confirmed account with that address, and the answer is the same either way (spec 53).
- Deleting the founder's account makes the earliest joined member founder; an empty household is deleted.
- Every change of memberships locks the household row first.
- The household plan of a month is the central workplace (spec 125): it has the toolbar of the personal plan (print, add booking, add position). Adding a position or a booking asks for the person as step 1 of the same step dialog (skipped when only one person is possible; the title names the person); a person is offered only when their grant reaches the needed level. Ticking, unticking, editing and deleting a row act as that member under their grants (spec 68), with the same confirmation as in the personal plan; a refusal names the grant that is really missing. The rules sit in one place, `PositionActions`, used by the personal and the household plan.
- Printing in the household plan asks for one person or the whole household. In the Android app printing creates a PDF of the month view and opens the system share dialog; on the web and in Windows the system print dialog opens (spec 125, R6).

## Interfaces

API under `/api/v1/households`: `POST` (found), `mine`, `{id}` (PATCH), `{id}/members/{userId}` (DELETE), `{id}/members/me` (DELETE), `{id}/invitations` (GET, POST, DELETE by id); `/api/v1/invitations/{token}` (preview without sign-in) and `/accept`; see `docs/specs/67-household`. Account deletion of [User account](user-account.md) hands households on.

## Open questions

None.
