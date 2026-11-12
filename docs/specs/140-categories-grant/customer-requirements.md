# Customer requirements: Categories as a grant area

Format: 2

## Background

Categories belong to one person. Another household member can list them only with some view grant (CategoryEndpoints `?owner=`) and can never create, rename or delete them, neither in the app nor through the assistant. When the human manages Jasmin's bookings he needs categories in her data too (Duofy#138, #140, 12.11.2026).

## Goal

A member can release her categories to another member at a chosen level, like plan, bookings and accounts, and the app and the API respect it.

## Users

Household members who manage money for each other; the human and Jasmin first.

## Requirements

R1 (must): The grants get a new area "Kategorien" with the levels none, view, create, edit and delete — because the human wants a separate permission for categories (12.11.2026: "eigener Bereich").
R2 (must): With the matching level a member can list, create, rename and delete the owner's categories through the API (`?owner=`) and in the app through the person switch; without it the refusal names the missing right — because the assistant and the app must work the same way.
R3 (must): Categories are on by default: every household member has Kategorien at the highest level (delete) for every other member, existing and new members alike, until the owner reduces it in the grant dialog to view or none; an explicit choice, including none, always decides — because the human wants it open for everybody and closable per owner (amended 12.11.2026, "Ja, alles an für alle"; replaces the earlier rule that nobody gains create, edit or delete).
R4 (must): The grant dialog, the grants overview and the refusal texts show the new area in German ("Kategorien") on tablet and phone — because the owner must see and set it.

## Constraints

Repository: Duofy (grants are core); the Plagevo MCP category tools (#138) pass owner to create once this is merged. #127 (simplify grants) will include this area later. A database migration is merged only with the human's yes.

## Open questions

None.
