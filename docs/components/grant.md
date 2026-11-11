# Component: Grant

## Purpose

What one person allows one other member of their household to do with their data, per area. It is not the household itself (who plans together) and not an admin right.

## Terms

| Term | Meaning |
| --- | --- |
| Grant | One owner allows one other member one level in one area. |
| Granter | The owner of the data, the only one who sets the grant. |
| Grantee | The member who receives the grant. |
| Area | Plan, Buchungen (`book`), Konten (`accounts`) or Verträge (`commitments`). |
| Level | Nichts, Ansehen, Anlegen, Ändern, Löschen (`none`, `view`, `create`, `edit`, `delete`), each including the ones before. |
| Preset | A ready set of four levels: Partner, Mitlesen, Kind, Nichts. |
| Active member | The person whose figures the app shows, chosen with the switch; "Ich" for oneself. |
| Central check | The one service every endpoint asks whether the caller may act on an owner's data in an area at a level. |

## Data

- `granter_id`, `grantee_id`, `area`, `level`; key (granter, grantee, area); granter ≠ grantee; no row means `none`, setting `none` deletes the row.

## Rules

- Only the granter sets a grant; nobody grants themselves.
- A grant counts only while granter and grantee share a household.
- Leaving, removal or account deletion deletes every grant from and to the person.
- Levels are compared by rank only.
- One's own data always passes the check.
- Without `view` foreign records answer 404; with `view` a missing step answers 403 with the step's code.

## Interfaces

`PUT /api/v1/households/{id}/grants/{granteeId}`; member entries of `GET /api/v1/households/mine` carry `grantsToMe` and `myGrants`; the endpoints of accounts, bookings, contracts, plans and categories take an optional owner; see `docs/specs/68-grants`.

## Open questions

None.
