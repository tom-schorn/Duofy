# Functional specification: Categories as a grant area

Format: 2

## Summary

A new grant area "Kategorien" (key `categories`) with the levels none to delete. The category endpoints accept `?owner=` for create, update and delete, checked against it. The grant dialog and the refusal texts know the new area. Existing grants stay as they are.

## Customer requirements

[customer-requirements.md](customer-requirements.md): R1, R2, R3, R4.

## Components

- [Grant](docs/components/grant.md)
- [Category](docs/components/category.md)

## Solution

- Area (R1): `GrantAreas.Categories = "categories"` added to `GrantAreas.All`, `GrantLevels` and the Grant entity/area enum; migration only if the storage needs it (new rows default to none).
- Endpoints (R2): CategoryEndpoints list keeps `?owner=` and checks categories view (R3 fallback below); create, update and delete accept `?owner=` and check categories create, edit and delete for that owner; refusals use the existing grant refusal format naming "Kategorien: <Stufe>".
- Compatibility (R3): when the owner has no row for the new area, listing falls back to the current rule (any view grant lets the member list), and create, edit and delete are refused. Once the owner sets the area, it alone decides.
- App (R4): grant dialog row "Kategorien" with the same level choice; grants overview and not-shared hints name it; the category page works with the person switch within the grant.
- Docs: docs/components/grant.md and category.md updated.

## Behaviour

- R1: the grant dialog offers Kategorien with Keine, Ansehen, Anlegen, Ändern, Löschen.
- R2: Jasmin grants Tom Kategorien: Anlegen → Tom creates "Haushaltsausgleich" in Jasmin's categories via the API with ?owner=Jasmin and in the app through the person switch; renaming is refused with "Es fehlt: Kategorien: Ändern".
- R3: before Jasmin sets the area, Tom lists her categories as today and cannot create.
- R4: the dialog fits at 412, 800 and 1024 px.

## Data and interfaces

New grant area key `categories` in the grant API and responses. Category POST/PUT/DELETE accept `?owner=`. Migration if the grant storage needs it.

External input: yes

## Acceptance criteria

- [ ] A1 (R1): Given the grant API, when the areas are read or set, then `categories` exists with the levels none, view, create, edit, delete.
- [ ] A2 (R2): Given B granted A categories create, when A creates a category with ?owner=B, then it belongs to B; when A renames it, then the answer is a refusal naming "Kategorien: Ändern"; given edit and delete, rename and delete succeed.
- [ ] A3 (R3): Given B never set the categories area but granted A plan view, when A lists B's categories, then they are returned; when A creates one, then it is refused.
- [ ] A4 (R4): Given the grant dialog and the category page with the person switch at 412, 800 and 1024 px, when Kategorien is set and used, then the row fits and the page acts within the grant.
- [ ] A5 (R2): Given a person C outside the household, when A calls any category endpoint with ?owner=C, then the answer is not found without data.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | pending |
| R2 | A2 | pending |
| R2 | A5 | pending |
| R3 | A3 | pending |
| R4 | A4 | pending |

## Threats

- Can someone pretend to be someone else? Owner only through the checked grant.
- Can someone change data? Only at the granted level; no right is granted by the change itself (R3).
- Can someone deny an action? Category changes keep their owner; MCP calls are audited.
- Can someone read what they should not? Outside the household not found (A5).
- Can someone bring it to a halt? No new load.
- Can someone gain rights they should not have? Create, edit and delete need the explicit new grant.

## Non-goals

- No simplification of grants (#127), no merge of areas.
- No change to system categories.

## Risks

A forgotten check on one endpoint would let a member change someone's categories: A2 and A5 test every endpoint.

## Open questions

None.

## Approval

Approved by the human on 2026-11-12 (session answers: "auch für Mitglieder aus dem Haushalt mit neuer Berechtigung", "Eigener Bereich").
