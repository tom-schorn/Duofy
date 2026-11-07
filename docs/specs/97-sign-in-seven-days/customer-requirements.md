# Customer requirements: A sign-in holds seven days and is extended by use

Format: 2

## Background

The human reported (P-096, 05.11.2026) having to sign in again in the apps every time. The cause was the reinstalls of 04.11.2026: the apps keep their tokens in the secure storage of the device, and a reinstall deletes them. With the app reopened without a reinstall the human stayed signed in. Today a refresh token is valid 30 days and every refresh issues a new one, so the sign-in is extended by use.

The human, 05.11.2026: "mach 7 tage wenn benutzt entsprechend verlängern". Fingerprint or passkey sign-in comes later in its own issue.

## Goal

A sign-in that nobody uses ends after seven days; a sign-in that is used stays.

## Users

Everyone who uses the apps or the web version.

## Requirements

R1 (must): A sign-in ends when it was not used for seven days — because the human chose seven days.
R2 (must): Every use within the seven days extends the sign-in by another seven days from that use — because the human chose "wenn benutzt entsprechend verlängern".
R3 (must): Apps and web follow the same rule — because both sign in with the same tokens (Program.cs) and a different web rule was not asked for.
R4 (must): After the sign-in ended, the app or web shows the sign-in page without an error message about a failure — because an ended sign-in is normal.

## Constraints

- No database change.

## Open questions

None.
