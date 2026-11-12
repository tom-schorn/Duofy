# Functional specification: Categories as a grant area

Format: 2

## Summary

A new grant area "Kategorien" (key `categories`) with the levels none to delete, on at delete by default for every household member until the owner sets it. The category endpoints accept `?owner=` for create, update and delete, checked against it. The grant dialog and the refusal texts know the new area. Existing grants of the other areas stay as they are.

## Customer requirements

[customer-requirements.md](customer-requirements.md): R1, R2, R3, R4.

## Components

- [Grant](docs/components/grant.md)
- [Category](docs/components/category.md)

## Solution

- Area (R1): `GrantAreas.Categories = "categories"` added to `GrantAreas.All`, `GrantLevels` and the Grant entity/area enum; a migration widens the check constraints so an explicit `none` can be stored for categories only.
- Endpoints (R2): CategoryEndpoints list keeps `?owner=` and checks categories view; create, update and delete accept `?owner=` and check categories create, edit and delete for that owner; refusals use the existing grant refusal format naming "Kategorien: <Stufe>".
- Default (R3): when the owner has no row for the new area, every member of the owner's household has delete (list, create, edit, delete); a person outside the household has none (not found). Once the owner sets the area, the row alone decides; setting `none` is stored as an explicit row, so it hides the categories and is not the default again. The member lists in the grant API show `delete` for categories without a row; presets (Partner, Mitlesen, Kind, Nichts) do not contain Kategorien, choosing one leaves it as it is and a stored categories level does not turn a matching preset into "Eigene Auswahl". The data protection net (owner filter, save check) lets household members through for categories; the central check decides the level.
- App (R4): grant dialog row "Kategorien" with the same level choice (Löschen preselected without a row); grants overview and not-shared hints name it; the category page works with the person switch within the grant.
- Docs: docs/components/grant.md and category.md updated.

## Behaviour

- R1: the grant dialog offers Kategorien with Keine, Ansehen, Anlegen, Ändern, Löschen.
- R2: Jasmin grants Tom Kategorien: Anlegen → Tom creates "Haushaltsausgleich" in Jasmin's categories via the API with ?owner=Jasmin and in the app through the person switch; renaming is refused with "Es fehlt: Kategorien: Ändern".
- R3: before Jasmin sets the area, Tom lists, creates, renames and deletes her categories; when Jasmin sets Kategorien to Ansehen he can only list, when she sets Nichts he sees nothing.
- R4: the dialog fits at 412, 800 and 1024 px.

## Data and interfaces

New grant area key `categories` in the grant API and responses. Category POST/PUT/DELETE accept `?owner=`. Migration if the grant storage needs it.

External input: yes

## Acceptance criteria

- [ ] A1 (R1): Given the grant API, when the areas are read or set, then `categories` exists with the levels none, view, create, edit, delete.
- [ ] A2 (R2): Given B granted A categories create, when A creates a category with ?owner=B, then it belongs to B; when A renames it, then the answer is a refusal naming "Kategorien: Ändern"; given edit and delete, rename and delete succeed.
- [ ] A3 (R3): Given B never set the categories area, when A (same household) lists, creates, renames and deletes B's categories, then it works; given B set Kategorien to none, when A lists or creates, then the answer is not found; given B set view, when A creates, then it is refused with the missing step.
- [ ] A4 (R4): Given the grant dialog and the category page with the person switch at 412, 800 and 1024 px, when Kategorien is set and used, then the row fits and the page acts within the grant.
- [ ] A5 (R2): Given a person C outside the household, when A calls any category endpoint with ?owner=C, then the answer is not found without data.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1 | GrantTests.The_categories_area_is_stored_and_seen_from_both_sides_without_touching_the_others; Domain GrantTests (default delete without a row, explicit none); GrantsPageTests.The_categories_area_is_in_no_preset_so_existing_grants_keep_their_preset; GrantsPageTests.The_categories_row_offers_the_five_levels_and_sends_only_that_area |
| R2 | A2 | GrantedCategoryTests.Create_edit_and_delete_follow_the_categories_level_and_belong_to_the_owner; Change_through_the_id_without_owner_is_judged_by_the_owner_of_the_category |
| R2 | A5 | GrantedCategoryTests.An_outsider_gets_not_found_on_every_category_endpoint |
| R3 | A3 | GrantedCategoryTests.Without_a_categories_row_ben_may_create_rename_and_delete_annas_categories; Ben_reads_annas_categories_by_default_and_an_outsider_does_not; Once_the_categories_row_exists_it_alone_decides_and_view_alone_lists; When_the_owner_sets_categories_to_none_the_member_sees_nothing_despite_other_view_grants |
| R4 | A4 | GrantsPageTests.The_categories_row_offers_the_five_levels_and_sends_only_that_area (row); widths 412/800/1024 and the category page with the person switch: pending, needs the browser check |

## Threats

- Can someone pretend to be someone else? Owner only through the checked grant.
- Can someone change data? Only at the granted level, which by the human's decision is delete for household members until the owner reduces it (R3); each owner can close it.
- Can someone deny an action? Category changes keep their owner; MCP calls are audited.
- Can someone read what they should not? Outside the household not found (A5).
- Can someone bring it to a halt? No new load.
- Can someone gain rights they should not have? Household members gain category rights by default (amended); outsiders never.

## Non-goals

- No simplification of grants (#127), no merge of areas.
- No change to system categories.

## Risks

A forgotten check on one endpoint would let a member change someone's categories: A2 and A5 test every endpoint.

## Open questions

None.

## Approval

Approved by the human on 2026-11-12 (session answers: "auch für Mitglieder aus dem Haushalt mit neuer Berechtigung", "Eigener Bereich", after the explanation of equal names across members "Ja + Auswertung als Issue").

Amended 2026-11-12: categories on (delete) by default for all members (human, "Ja, alles an für alle").
