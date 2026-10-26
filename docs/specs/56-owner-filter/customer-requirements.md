# Customer requirements: Owner filter in the database layer

Format: 2

## Background

Issue #56 was written for the .NET rebuild: all customers of the hosted version share one database, and today every query restricts itself to the signed-in person; a forgotten restriction would show foreign data. On 2026-10-24 the human decided that everything left is built without walking through every rule. The grants (#68) bring one central check per endpoint; this specification adds the safety net beneath it, in the database layer, so that a forgotten check still cannot read or write foreign rows. It follows #68, because with grants "foreign" means "neither own nor shared with me".

## Goal

No query and no save in the API can touch rows of a person who has nothing to do with the signed-in person, even if an endpoint forgets its check.

## Users

Everybody on the hosted version, indirectly; the team, which can no longer leak data by forgetting a filter.

## Requirements

R1 (must): Every query on person-owned data (accounts, bookings and their lines, contracts, plans and positions, own categories) returns only rows of the signed-in person and of members of the same household who granted the person at least Ansehen in any area, without the query saying so — because a forgotten filter must not show data of strangers.
R2 (must): Without a signed-in person such a query returns nothing — because an anonymous request must never see personal data.
R3 (must): Saving a new or changed row whose owner is neither the signed-in person nor a member who granted at least Anlegen in any area is refused with an error, and nothing is stored — because a wrong owner on a write is as bad as a wrong read.
R4 (must): The few places that must see more on purpose — account deletion, the composed household plan, the household's member months, migrations and startup checks — switch the filter off explicitly and in one visible place each — because a safety net with hidden holes is worse than none.
R5 (must): A test proves that person A, without any grant, sees none of person B's contracts, accounts, bookings or plans even through a query without its own owner condition — because the net must be proven, not assumed.
R6 (wont): Row-level security in PostgreSQL, a filter per area (the area stays the central check's job), the household plan's visibility of all members' positions — because the database-side policy would need a database role per request, and the area and the household plan rules already live in #68 and #69.

## Constraints

- Builds on #68 (grants, central check `IAccess`) and #67 (households); implemented with EF Core global query filters and a save check.
- No migration.

## Open questions

None.
