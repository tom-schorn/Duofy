# Customer requirements: Instance

Format: 2

## Background

The original Duofy (github.com/tom-schorn/Duofy: `backend/app/api/v1/admin.py`, `legal.py`, `core/config.py`, `models/instance_invitation.py`, `frontend/src/pages/AdminPage.tsx`, `LegalPage.tsx`) lets the operator of an instance decide who may register, hand out registration links and publish imprint, privacy policy and terms. On 2026-10-24 the human decided to rebuild the rest of the original as it is, without the statement import, and to ask only real questions. Asked how the first administrator comes about, the human chose a one-time setup code from the environment (as issue #33 proposed for the original), because the original's rule "whoever registers with ADMIN_EMAIL becomes admin" lets anybody who knows that address become admin while email addresses are not confirmed. The settings page of the original needs no specification of its own: its quotas came with #66, account deletion exists, and its curve setting belongs to the V2 curve.

## Goal

Whoever runs a Duofy server becomes its administrator safely, decides who may register, can invite people by link, and publishes the legal texts the server needs.

## Users

The operator of an instance: the human for the hosted version, a family member for a self-hosted one; visitors who read the legal texts without an account.

## Requirements

R1 (must): While the instance has no administrator, whoever registers with the setup code from the environment, or enters it in the app with an existing account, becomes its administrator, in every registration mode; a wrong or missing code makes nobody administrator; once an administrator exists, the code has no effect any more — because only the operator has the environment file, and the special path must close after use.
R2 (must): Without a configured setup code the server starts, says in its log that no administrator can be created, and offers no special path — because a silent missing administrator is hard to find.
R3 (must): The registration mode comes from the environment: open (anybody), invite (only with an invitation, the default) or closed (nobody); the server tells the app which mode applies, and the app says so on the registration page — because a family server should not be open to the world by default.
R4 (must): An administrator creates registration invitations, optionally for one email address only, valid for seven days and usable once; they are shown as link and QR code with the share button of #67, listed while open, and revoked by deleting them — because the operator hands the link over personally and Duofy sends no mail.
R5 (must): The last administrator cannot delete their account — because nobody could run the instance any more.
R6 (must): The administrator page shows only who may come onto the instance; nothing there or anywhere gives an administrator plans, bookings or accounts of other people — because an administrator runs the instance and does not look into it.
R7 (must): Imprint, privacy policy and terms are configured per instance as text files; a text that is not configured or unreadable does not exist, neither as link nor as page; the texts are readable without signing in and shown as plain text — because the operator is liable for them, a family server usually needs none, and the store needs a public privacy policy.
R8 (must): The app shows links to the configured legal texts on the sign-in and registration pages and under "Mehr" — because they have to be reachable from everywhere.
R9 (wont): Making further administrators, changing the registration mode in the app, sending invitations by mail, rich text in legal texts, the web pages — because the original has none of these either, mail comes with #53, and the web pages are the human's part.

## Constraints

- Builds on the registration of spec 57, the share and QR code of #67 and the server choice of #61 (the app asks `/instance`).
- Needs a migration (administrator flag on users, table of registration invitations); it is applied to the human's database only after the yes.
- Settings come from the environment like every other setting (`.env.example` documents them).

## Open questions

None.
