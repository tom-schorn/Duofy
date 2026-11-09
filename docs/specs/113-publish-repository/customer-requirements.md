# Customer requirements: Publish duofy-dotnet as the public repository Duofy

Format: 2

## Background

Duofy is developed in the private repository tom-schorn/duofy-dotnet. The old Python and React version lives in the public repository tom-schorn/Duofy, which was archived on 09.11.2026 together with duofy-dotnet-archiv. The README of duofy-dotnet still says "Stand: ganz am Anfang" and points to the old Python version as the current Duofy. There is no SECURITY.md. The full git history was never scanned for secrets.

## Goal

duofy-dotnet is public under the name "Duofy", with a README that describes the current state, a way to report security problems privately, and a history that holds no secrets. The release v2.5 comes later, together with the store upload.

## Users

People who find Duofy on GitHub (possible users, contributors, people the human promotes the project to) and anyone who wants to report a security problem.

## Requirements

R1 (must): The complete git history of duofy-dotnet contains no working secret (password, token, key, connection string with credentials) when it becomes public — because anyone can read every commit of a public repository.
R2 (must): The README describes the current state of Duofy (what works today, how to start it) and no longer calls the project "ganz am Anfang" or the old Python version the current Duofy — because the README is the first impression for the people the human promotes the project to.
R3 (must): A SECURITY.md tells people to report security problems through GitHub's private vulnerability reporting, and that reporting is switched on — because problems must reach the human without being public and without exposing an email address.
R4 (must): The repository is public under the name "Duofy" only after R1 to R3 are met — because publishing first and cleaning up later leaks what the clean-up removes.
R5 (should): The archived old repository is renamed to "Duofy-python", so the name "Duofy" is free — because the human wants the current project under the plain name.
R7 (must): The address example.org appears neither in the files nor anywhere in the history of the public repository, including commit author and committer addresses — because a public repository exposes it to anyone, including address collectors (human, 09.11.2026).
R6 (wont): A version tag, a GitHub release or a store upload — because v2.5 is released together with the store upload.

## Constraints

- No release or version tag today (decision of the human, 09.11.2026).
- Renaming and switching visibility are done by the lead, each with the human's yes.
- The history of main is rewritten once for R7 and force-pushed, after a backup bundle and the human's yes right before the push; plagevo is reconnected once without changing its content (human, 09.11.2026).
- If the scan finds a secret, the human decides whether the key is replaced or the history is rewritten; nothing is rewritten without that decision.

## Open questions

None.
