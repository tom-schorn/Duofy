# Component: Category

## Purpose

What money was for, more precisely than the pot. Built-in categories come from the original's list; own categories are a transition until a fixed schema exists.

## Terms

| Term | Meaning |
| --- | --- |
| Built-in category | One of the original's categories, the same for everybody, with a stable key. |
| Own category | A category a person added, with a group and a pot; visible to them and to members they granted the area Kategorien (by default every household member, until the owner reduces it). |
| Group | The heading a category appears under, such as Wohnen or Kinder. |
| Suggested pot | The pot preselected when the category is chosen; changeable per booking. |

## Data

- Built-in: `key`, `group`, `suggested_pot`, German name from the resource file. Own: `owner_id`, `name` (1–60), `group`, `suggested_pot`.

## Rules

- Names are unique per person among built-in and own names, without case.
- An own category in use by a booking cannot be deleted.
- With `?owner=`, a member lists with Kategorien view, creates with Anlegen (it belongs to the owner), renames with Ändern and deletes with Löschen. Without a categories row, every household member may do all of it (delete). A person outside the household gets not found (Spec 140).
- Long-term a fixed schema replaces own categories; each own category must then be mapped (issue 40).

## Interfaces

`/api/v1/categories` (GET, POST, PUT, DELETE, all with optional `?owner=`), see `docs/specs/59-bookings` and `docs/specs/140-categories-grant`.

## Open questions

None.
