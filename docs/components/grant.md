# Component: Grant

## Purpose

What one person allows one other member of their household to do with their data, per area. It is not the household itself (who plans together) and not an admin right.

## Terms

| Term | Meaning |
| --- | --- |
| Grant | One owner allows one other member one level in one area. |
| Granter | The owner of the data, the only one who sets the grant. |
| Grantee | The member who receives the grant. |
| Area | Plan, Buchungen (`book`), Konten (`accounts`), Verträge (`commitments`), Sparen und Schulden (`savings`) or Kategorien (`categories`). |
| Level | Nichts, Ansehen, Anlegen, Ändern, Löschen (`none`, `view`, `create`, `edit`, `delete`), each including the ones before. |
| Preset | A ready set of levels for all areas: Partner (Löschen), Mitlesen (Ansehen), Kind (Plan ansehen), Nichts. |
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
- Categories (Spec 140): while the owner has no row for `categories`, any view grant in another area lets the member list the owner's categories and create, edit and delete are refused (403 with the step's code, shown as "Es fehlt: Kategorien: Ändern"). Once the owner has a row, it alone decides. Setting `none` deletes the row, so it returns to the compatibility rule.
- Without `view` foreign records answer 404; with `view` a missing step answers 403 with the step's code.

## Interfaces

`PUT /api/v1/households/{id}/grants/{granteeId}`; member entries of `GET /api/v1/households/mine` carry `grantsToMe` and `myGrants`; the endpoints of accounts, bookings, contracts, plans and categories take an optional owner (for categories on create, update and delete as well); see `docs/specs/68-grants` and `docs/specs/140-categories-grant`.

## Open questions

None.
