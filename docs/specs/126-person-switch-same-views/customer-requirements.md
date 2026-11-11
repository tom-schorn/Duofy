# Customer requirements: Person switch: act in the other person's name with the same views

Format: 2

## Background

On 11.11.2026 the human found: 'Haushalt' must not be in the profile menu (replaces spec 44 R2/R5); clicking a person does nothing until going to the start page, which greets 'Hallo Tom' instead of the chosen person; the other person's start page lacks blocks (guidelines in the household, the household) although all grants exist; components are not reused the same way; evaluations look nearly identical and must be checked. The human's rule: switching to a person means working in their name and seeing the same information as for oneself, within the grants.

## Goal

Switching to a person works at once on every page and shows exactly the views one has for oneself, with that person's data.

## Users

Household members who look after each other's finances.

## Requirements

R1 (must): The profile menu lists 'Ich' and the persons with grants only; 'Haushalt' is not in it — because the household is reached through the navigation (human, 11.11.2026, replaces spec 44 R2/R5).
R2 (must): Choosing a person switches the current page at once to that person's data, on every page, without going to the start page — because nothing happened on click (11.11.2026).
R3 (must): The start page greets the chosen person ('Hallo Jasmin') and shows the same blocks as for oneself (upcoming and overdue, pots, guidelines in the household, the household, the year), each filled with that person's data within the grants — because blocks were missing (11.11.2026).
R4 (must): Every page and evaluation shown for the chosen person uses the same components as for oneself, fed with that person's id; figures are that person's own — because components must be reused and the figures looked identical (11.11.2026).

## Constraints

- Grants of spec 68 unchanged; a block without the needed grant shows a short hint instead of disappearing silently.

## Open questions

None.
