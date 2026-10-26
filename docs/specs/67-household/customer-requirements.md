# Customer requirements: Household

Format: 2

## Background

The original Duofy (Python/React, github.com/tom-schorn/Duofy, `backend/app/api/v1/households.py`, `services/households.py`, `services/account_deletion.py`, `frontend/src/pages/HouseholdPage.tsx`) lets several people plan together in a household. On 2026-10-24 the human decided that the remaining features of the original are rebuilt as they are there, without going through every rule again, that the statement import is left out, and that questions come only where the original is unclear or does not fit. The household is cut into three parts: this one (household, members, invitations), the grants (#68) and the household plan (#69). The team builds the API and the app; the web page is the human's part.

Deviations from the original, decided by the human on 2026-10-24: the original bound an invitation to an email address and showed it in an in-app inbox. Because the rebuild does not confirm email addresses yet (#53), anybody could register with somebody else's address and take that invitation. The human chose invitation links instead: whoever holds the link may join, shown as a QR code in the app and passed on by the Android share dialog, so no mail server is needed. Sending the link by mail comes with the mail setup of #53. QRCoder 1.8.0 (MIT) was approved as a new dependency for the QR code. After the first build the human decided on 2026-10-24 that a person has no household until they found one, and that only the founder manages it; the original's own household for everybody and its equal admins are dropped.

## Goal

A person can found a household, invite their partner into it by a link or QR code, see who belongs to it and leave it again, without anybody's own data changing hands; whoever does not want a household simply has none.

## Users

Every Duofy user who plans together with somebody else, usually two partners; once when setting up, rarely afterwards.

## Requirements

R1 (must): A person has no household until they found one; founding asks for a name ("Haushalt von <Vorname>" suggested) and makes the person its founder; a person is in at most one household — because everybody has only their own things, and a household is a deliberate step.
R2 (must): A household owns nothing: accounts, contracts, plans and bookings stay with their person, and founding, joining, leaving or being removed changes none of them — because the household only says who plans together.
R3 (must): Every member sees the household's name, its quotas and its members with first name, last name, email and whether they are founder or member, marked "du" for oneself — because the people who plan together must know who that is.
R4 (must): The founder creates an invitation and gets a link to the server's public address from the environment, shown with a QR code and a button that opens the Android share dialog; an invitation is valid for 14 days, can be used once and can be revoked by the founder; only the founder sees the open ones — because the partner can scan it from the screen or receive it by any messenger, and no mail server is needed.
R5 (must): Whoever opens the link sees, also before signing in, only the household name, who invited and until when; after signing in or registering they can join; in the app a link can also be pasted on the household page — because the link is the proof, and holding it must not reveal the member list.
R6 (must): Joining is possible only while the person is in no household; the invitation is then used up — because a person is in at most one household.
R7 (must): Only the founder manages the household: renames it, changes its quotas (whole percents that sum to 100, 50/30/20 by default), invites, revokes invitations and removes members; there are no roles to give or take — because the human decided on 2026-10-24 that the founder manages what they founded.
R8 (must): A member can leave at any time and a removed member is out at once; either way they are in no household afterwards and keep all their data; the founder can leave only while alone, which dissolves the household; removing oneself is leaving — because nobody should be left in a household without its founder.
R9 (must): When the founder deletes their account, the member who joined earliest becomes founder; a household with nobody left goes with it — because one person's deletion must not lock the others out.
R10 (must): The app shows the household on a page reachable from "Mehr": without a household it offers "Haushalt gründen" and the field for a pasted link; a button the person may not use is not shown or shown greyed out with the reason, so the founder sees "Austreten" greyed out while others are in — because the household is set up rarely and needs no tab of its own, and a button that only leads to a refusal is confusing.
R11 (wont): Grants per person and area and the switch whose data is shown (#68), the household plan (#69), sending invitations by mail (#53), handing the household over while the founder stays, several households per person, Android app links that open the app directly, the web page of the household — because they come with their own specifications, were not wanted, or are the human's part.

## Constraints

- Builds on the API foundation (spec 57): owner from the token, error codes as `{ code }`, German texts in the shared resources.
- Needs a migration (households, members, invitations; no household is created for existing users); it is applied to the human's database only after the yes.
- New dependency QRCoder 1.8.0 (MIT), pinned; its license notice ships with the app.
- Taken over from the original: quotas per household (decision 60) and the lock while memberships change (decision 68). Changed by the human on 2026-10-24: no household from registration on, and no admin roles, only the founder.

## Open questions

None.
