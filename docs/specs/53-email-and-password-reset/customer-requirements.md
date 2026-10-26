# Customer requirements: Email confirmation and password reset

Format: 2

## Background

Issues #53 (email confirmation, forgotten password) and #54 (change password and email, delete account) were written for the .NET rebuild; the original Duofy had neither. Changing the password and deleting the account exist since spec 57; changing the email is what is left of #54. On 2026-10-24 the human decided that only the hosted version (SaaS) sends mail, with confirmation of addresses, while a self-hosted server has no mail path at all and resets forgotten passwords through its administrator. The human approved MailKit 4.18.1 (MIT, with MimeKit) for sending mail. Earlier the same day the human said an invitation link should go out by mail "sofern User existiert"; the lead read that as: by mail only to an address that already has a confirmed account. Builds on #67 (household invitations, share and QR code) and #70 (administrator, registration invitations).

## Goal

On the hosted version every account belongs to a confirmed address and a forgotten password can be reset by mail; on a self-hosted server a forgotten password is reset through the administrator without any mail server; everybody can change their email address.

## Users

Everybody with an account; the operator of a self-hosted server when somebody forgot their password.

## Requirements

R1 (must): One setting says whether the server is the hosted version; only then is mail sent, and a hosted server without complete mail settings does not start and says which setting is missing — because a hosted version that silently sends no mail would lock people out.
R2 (must): On the hosted version a new account gets a confirmation link by mail, valid 24 hours; signing in before confirmation is refused with a hint and a button to send the link again, at most once every five minutes; accounts that existed before count as confirmed — because only a confirmed address proves that the account belongs to its owner.
R3 (must): On the hosted version "Passwort vergessen" sends a link valid one hour to the address; the answer is the same whether the address exists or not; setting a new password through the link signs the person out everywhere — because a forgotten password must not need the operator, and the answer must not reveal who has an account.
R4 (must): On a self-hosted server "Passwort vergessen" says to ask the operator; the administrator enters the person's email on the admin page and gets a reset link valid 24 hours, usable once, as link, QR code and share button; an unknown address is answered as such only to the administrator — because there is no mail server, and the operator knows their family.
R5 (must): Everybody can change their email address with their current password: on the hosted version the new address gets a confirmation link and the change happens only after confirming, and the old address is told about it; on a self-hosted server the change happens at once — because an address that changes unconfirmed would let a typo lock the account.
R6 (should): On the hosted version an administrator of a household (#67) or of the instance (#70) can enter an email address when inviting; the link is mailed only if that address belongs to a confirmed account, and the answer is the same either way — because mail to arbitrary addresses would make Duofy a spam tool and would reveal who has an account.
R7 (wont): Two-factor sign-in (#55), Google sign-in (#62), mail on a self-hosted server, newsletters or reminders, the web pages — because they come with their own issues, the human chose no mail for self-hosting, or they are the human's part.

## Constraints

- Uses ASP.NET Core Identity's confirmation, reset and change-email tokens; links point to the server's public address from the environment (`Duofy:PublicUrl`).
- Mail settings come only from environment variables (`.env.example` documents them); no secret in a file in the repository.
- Needs a migration only if a column is missing for "confirmed before this feature"; existing accounts are marked confirmed after the human's yes.

## Open questions

None.
