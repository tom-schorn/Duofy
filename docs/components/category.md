# Component: Category

## Purpose

What money was for, more precisely than the pot. Built-in categories come from the original's list; own categories are a transition until a fixed schema exists.

## Terms

| Term | Meaning |
| --- | --- |
| Built-in category | One of the original's categories, the same for everybody, with a stable key. |
| Own category | A category a person added, with a group and a pot; visible only to them. |
| Group | The heading a category appears under, such as Wohnen or Kinder. |
| Suggested pot | The pot preselected when the category is chosen; changeable per booking. |

## Data

- Built-in: `key`, `group`, `suggested_pot`, German name from the resource file. Own: `owner_id`, `name` (1–60), `group`, `suggested_pot`.

## Rules

- Names are unique per person among built-in and own names, without case.
- An own category in use by a booking cannot be deleted.
- Long-term a fixed schema replaces own categories; each own category must then be mapped (issue 40).

## Interfaces

`/api/v1/categories` (GET, POST, PUT, DELETE), see `docs/specs/59-bookings`.

## Open questions

None.
