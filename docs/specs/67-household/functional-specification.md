# Functional specification: Household

Format: 2

## Summary

Founder-managed households with invitation links: a person has no household until they found one, the founder creates invitation links shown as QR code and shared through the Android share dialog, whoever holds a link and is in no household joins once, members leave, the founder removes members, renames the household and sets its quotas, and dissolves it by leaving alone. The app gets a page Haushalt under "Mehr" and a page for opening an invitation link. Grants and the household plan follow in #68 and #69.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R10. R11 is out of scope.

## Components

- [Household](docs/components/household.md)
- [User account](docs/components/user-account.md)

## Solution

- **Domain**: `Household` (id, name 1–100, quota targets needs/wants/savings as whole percents), `HouseholdMember` (household, user, role `founder` or `member`, joined at; exactly one founder per household), `HouseholdInvitation` (id, household, created by, token of 32 random bytes URL-safe, status `pending`, `accepted`, `revoked`, expires at = created + 14 days, used by, used at).
- **Data**: tables `households`, `household_members` (unique user), `household_invitations` (unique token); the migration creates no household for existing users. Changes to memberships lock the household row first (`SELECT … FOR UPDATE`), as decision 68 of the original.
- **API** (`Duofy.Api`): endpoints below. Registration creates no household. Account deletion of a founder hands the household to the earliest joined member before the user is removed (R9).
- **Client and pages**: `IHouseholdApi` in `Duofy.ApiClient`; in `Duofy.Shared` the page `/haushalt` (without household: "Haushalt gründen" with a name field and the field to paste an invitation link; with household: card with name, member count, quotas, leave; member rows; for the founder invite, quotas, open invitations with revoke and remove per member) and the page `/einladung/{token}` (preview, sign-in or registration, Beitreten). The invite dialog shows the link, its QR code as SVG from QRCoder 1.8.0 and a button Teilen that calls a host service: the app uses the MAUI share API, the web host copies the link. Entry "Haushalt" on the page Mehr. The link is `<PublicUrl>/einladung/<token>` with `Duofy:PublicUrl` from the environment (documented in `.env.example`); the API returns the full link with the invitation.

## Behaviour

- R1: `POST /households` with an optional name (default "Haushalt von <first name>", cut to 100 characters) creates the household with the caller as founder; a caller already in a household gets `409 already_in_household`. `GET /households/mine` without a household answers `404 no_household`.
- R3: `GET /households/mine` answers the caller's household with members ordered by joined at; each member carries user id, first name, last name, email, role (`founder` or `member`) and `isMe`.
- R4: Creating an invitation needs the founder (`403 not_household_founder`) and answers id, token, expiry and `url`; without `Duofy:PublicUrl` it answers `409 public_url_missing` and the page says "Für Einladungen fehlt die öffentliche Adresse des Servers."; at most 10 pending, unexpired invitations per household (`409 too_many_invitations`). `GET /households/{id}/invitations` lists the pending, unexpired ones for the founder only; revoking sets `revoked`.
- R5: `GET /invitations/{token}` needs no sign-in and answers only household name, the inviter's first and last name and the expiry; an unknown token answers `404 invitation_not_found`, a used or revoked one `409 invitation_not_open`, an expired one `409 invitation_expired`. The page `/einladung/{token}` shows that preview, asks to sign in or register when needed and returns to itself afterwards. A pasted link is accepted when it ends in `/einladung/<token>`.
- R6: `POST /invitations/{token}/accept` with sign-in; a caller in any household answers `409 already_in_household` and the invitation stays pending; otherwise the caller becomes `member` and the invitation becomes `accepted` with used by and used at.
- R7: Rename, quotas, invitations and removal need the founder (`403 not_household_founder`); quotas are whole numbers 0–100 summing to 100, otherwise `400 quota_invalid`. There is no endpoint that changes a role.
- R8: A member leaving or being removed is deleted from the household and is in none afterwards; the founder leaving while others are in answers `409 founder_cannot_leave`; the founder leaving alone deletes the household with its invitations; removing oneself answers `400 cannot_remove_self`; an unknown member `404 member_not_found`.
- R9: `DELETE /users/me` of a founder makes the earliest joined member founder; a household with nobody else is deleted; a member's deletion just removes the membership.
- R10: Without a household the page shows "Haushalt gründen" and the paste field; the founder sees invite, quotas, open invitations and remove, and "Austreten" greyed out with "Als Gründer kannst du erst austreten, wenn du allein im Haushalt bist." while others are in (Auflösen while alone); members see the rows read-only and Austreten; consequential actions (leave, dissolve, remove, revoke) ask once.
- Households of others answer `404 household_not_found` on every household path.

## Data and interfaces

| Method and path | Body | Answers |
| --- | --- | --- |
| `POST /api/v1/households` | `{ name? }` | `201 HouseholdResponse`; `400`; `409 already_in_household` |
| `GET /api/v1/households/mine` | – | `200 HouseholdResponse`; `404 no_household` |
| `PATCH /api/v1/households/{id}` | `{ name?, quotaNeeds?, quotaWants?, quotaSavings? }` | `200`; `400`; `403 not_household_founder`; `404` |
| `DELETE /api/v1/households/{id}/members/{userId}` | – | `204`; `400 cannot_remove_self`; `403`; `404` |
| `DELETE /api/v1/households/{id}/members/me` | – | `204`; `404`; `409 founder_cannot_leave` |
| `GET /api/v1/households/{id}/invitations` | – | `200 [InvitationResponse]`; `403`; `404` |
| `POST /api/v1/households/{id}/invitations` | – | `201 InvitationResponse` (id, token, expiresAt, url); `403`; `404`; `409 too_many_invitations`, `409 public_url_missing` |
| `DELETE /api/v1/households/{id}/invitations/{invitationId}` | – | `204`; `403`; `404` |
| `GET /api/v1/invitations/{token}` (no sign-in) | – | `200 InvitationPreview`; `404`; `409` |
| `POST /api/v1/invitations/{token}/accept` | – | `200 HouseholdResponse`; `404`; `409` |

External input: yes

## Acceptance criteria

- [x] A1 (R1): Given a new registration of Anna Muster, when it succeeds, then GET /households/mine answers 404 no_household; when she founds a household without a name, then it is "Haushalt von Anna" with Anna as founder, isMe true; founding a second one answers 409 already_in_household.
- [x] A2 (R1): Given users who registered before the migration, when the migration has run, then none of them is in a household.
- [x] A3 (R4): Given founder Anna, when she creates an invitation, then it answers a token of at least 43 characters and an expiry 14 days ahead, and GET /households/{id}/invitations lists it; when she revokes it, then it is no longer listed and its preview answers 409 invitation_not_open.
- [x] A4 (R4, R7): Given member Ben, when he creates an invitation or lists the invitations, then the answer is 403 not_household_founder and no invitation exists.
- [x] A5 (R4): Given a household with 10 open invitations, when an eleventh is created, then the answer is 409 too_many_invitations.
- [x] A6 (R5): Given an open invitation of Anna Muster's household, when its preview is requested without sign-in, then it answers the household name, "Anna Muster" and the expiry, and no member list or email.
- [x] A7 (R2, R5, R6): Given Ben in no household and an open invitation, when he accepts it, then GET /households/mine answers Anna's household with Anna as founder and Ben as member, his accounts and contracts are unchanged, and a second accept of the same token by Clara answers 409 invitation_not_open.
- [x] A8 (R6): Given Ben who founded a household of his own or is member elsewhere, when he accepts an invitation, then the answer is 409 already_in_household and nothing changes.
- [x] A9 (R5): Given an invitation whose expiry has passed, when it is previewed or accepted, then the answer is 409 invitation_expired; an unknown token answers 404 invitation_not_found.
- [x] A10 (R7): Given founder Anna, when she sets quotas 60/20/20, then the household shows 60/20/20; 60/30/20 answers 400 quota_invalid; renaming to "Familie Muster" shows the new name to Ben; Ben renaming answers 403 not_household_founder.
- [x] A11 (R7): Given the API, when a request tries to change a member's role, then no such endpoint exists (404 or 405) and the roles are unchanged.
- [x] A12 (R8): Given founder Anna and member Ben, when Anna leaves, then the answer is 409 founder_cannot_leave; when Ben leaves, then Ben is in no household and Anna's household has only Anna; when Anna then leaves, then her household no longer exists and she is in none.
- [x] A13 (R8): Given founder Anna and member Ben, when Anna removes Ben, then Ben is in no household with his data unchanged; when Anna removes herself by id, then the answer is 400 cannot_remove_self.
- [x] A14 (R9): Given founder Anna, members Ben (joined first) and Clara, when Anna deletes her account, then the household still exists with Ben as founder and Clara as member; given Anna alone, when she deletes her account, then her household is gone.
- [x] A15 (R2, R3): Given Clara in another household, when she calls PATCH, invitations or members of Anna's household, then every answer is 404 household_not_found and Anna's household is unchanged.
- [x] A16 (R3, R10): Given the app page Mehr, when Haushalt is opened without a household, then it offers Haushalt gründen and the paste field; with a household it shows the name, the members with Gründer or Mitglied and "du", and for the founder the buttons Einladen and Richtwerte; for a member these buttons are missing.
- [x] A19 (R4): Given PublicUrl "https://duofy.example.de" and the app connected through 192.168.2.67, when founder Anna creates an invitation, then its url is "https://duofy.example.de/einladung/<token>"; without PublicUrl the answer is 409 public_url_missing and the page shows the notice.
- [x] A20 (R10): Given founder Anna with member Ben, when the page Haushalt is shown to Anna, then Austreten is greyed out with the reason text and cannot be pressed; after Ben left, Auflösen is offered.
- [x] A17 (R4): Given founder Anna on the page Haushalt, when she taps Einladen, then the dialog shows the link "<PublicUrl>/einladung/<token>", a QR code that encodes exactly that link, and a button Teilen that hands the link to the share service.
- [x] A18 (R5, R6): Given Ben signed in on the app and in no household, when he pastes the link on the page Haushalt or opens /einladung/<token>, then he sees "Haushalt von Anna", invited by Anna Muster, with Beitreten; after Beitreten the page Haushalt shows Anna's household.
- [x] A21 (R6, R10): Given Ben signed in and already in a household, when he opens /einladung/<token>, then Beitreten is greyed out with the reason text of already_in_household and cannot be pressed; without a household it is offered.
- [x] A22 (R4): Given Ben accepting an invitation while Anna revokes it at the same moment, when both are done, then the invitation is either accepted with Ben as member or revoked with Ben not a member, never revoked with used_by set.
- [x] A23 (R4, R10): Given a household with 10 open invitations, when founder Anna opens the page Haushalt, then Einladen is greyed out with the reason and cannot be pressed.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1, A2 | `Tests/Duofy.Backend.Tests/HouseholdTests.cs`: `A_new_registration_has_no_household_until_anna_founds_one`, `A_second_household_is_refused_and_changes_nothing`, `A_given_name_is_used`, `A_name_longer_than_100_characters_is_refused_and_founds_nothing`, `The_migration_puts_no_existing_user_into_a_household`; `Tests/Duofy.Backend.Tests/Domain/HouseholdTests.cs`: `A_founded_household_without_a_name_is_named_after_the_first_name_with_the_person_as_founder`, `A_long_first_name_is_cut_to_the_length_of_a_household_name`; `Tests/Duofy.ApiClient.Tests/HouseholdClientTests.cs`: `Founding_posts_the_name`, `Mine_without_a_household_is_not_found`; `Tests/Duofy.Shared.Tests/HouseholdPageTests.cs`: `Founding_with_a_name_shows_the_new_household`, `Founding_without_a_name_leaves_the_default_to_the_api` |
| R2 | A7, A15 | `Tests/Duofy.Backend.Tests/InvitationTests.cs`: `Accepting_moves_ben_into_annas_household_and_uses_the_invitation_up`; `Tests/Duofy.Backend.Tests/MembershipTests.cs`: `Clara_from_another_household_finds_nothing_in_annas`, `Anna_removes_ben_who_keeps_his_data_but_cannot_remove_herself` |
| R3 | A15, A16 | `Tests/Duofy.Backend.Tests/MembershipTests.cs`: `Clara_from_another_household_finds_nothing_in_annas`; `Tests/Duofy.Backend.Tests/HouseholdTests.cs`: `A_new_registration_has_no_household_until_anna_founds_one`; `Tests/Duofy.Shared.Tests/HouseholdPageTests.cs`: `The_founder_sees_name_members_roles_du_and_the_founder_buttons`, `A_member_sees_the_rows_read_only` |
| R4 | A3, A4, A5, A17, A19, A22, A23 | `Tests/Duofy.Backend.Tests/InvitationTests.cs`: `The_founder_creates_lists_and_revokes_an_invitation`, `A_member_may_neither_create_nor_list_nor_revoke_invitations`, `The_eleventh_open_invitation_is_refused`, `Expired_invitations_neither_count_nor_show`, `The_link_uses_the_public_address_and_not_the_one_the_request_came_through`, `A_public_address_with_a_slash_at_the_end_gives_the_same_link`, `Without_a_public_address_no_invitation_is_created`, `Accepting_and_revoking_at_the_same_time_never_revoke_a_used_invitation`; `Tests/Duofy.Backend.Tests/Domain/HouseholdTests.cs`: `An_invitation_has_a_url_safe_token_of_256_bits_and_lasts_14_days`, `A_used_or_revoked_invitation_is_no_longer_open`; `Tests/Duofy.ApiClient.Tests/HouseholdClientTests.cs`: `Changes_members_and_invitations_go_to_their_addresses`; `Tests/Duofy.Shared.Tests/HouseholdPageTests.cs`: `Einladen_shows_the_link_its_qr_code_and_shares_it`, `Without_a_public_address_the_page_says_what_is_missing`, `A_refused_invitation_shows_why`, `Open_invitations_are_listed_and_revoked_after_asking_once`, `The_link_points_to_the_public_address_and_not_to_the_connected_server`, `With_ten_open_invitations_einladen_is_greyed_out_with_the_reason`, `Below_ten_open_invitations_einladen_is_offered`; `Tests/Duofy.App.Tests/ShareServiceTests.cs`: `The_share_service_hands_the_link_to_the_android_share_dialog`; `Tests/Duofy.Web.Tests/ShareServiceTests.cs`: `Sharing_writes_the_link_to_the_clipboard` |
| R5 | A6, A7, A9, A18 | `Tests/Duofy.Backend.Tests/InvitationTests.cs`: `The_preview_without_sign_in_shows_only_household_inviter_and_expiry`, `An_expired_invitation_can_be_neither_previewed_nor_accepted`, `An_unknown_token_is_not_found`, `Accepting_needs_a_sign_in`; `Tests/Duofy.Backend.Tests/Domain/HouseholdTests.cs`: `An_invitation_is_expired_from_its_expiry_on`, `A_used_or_revoked_invitation_is_no_longer_open`; `Tests/Duofy.Shared.Tests/InvitationPageTests.cs`: `Without_sign_in_the_page_offers_sign_in_and_registration_that_come_back`, `An_expired_invitation_says_so_and_offers_no_join`; `Tests/Duofy.Shared.Tests/InvitationLinkTests.cs`; `Tests/Duofy.Shared.Tests/HouseholdPageTests.cs`: `A_pasted_link_opens_the_invitation_page`, `Something_else_pasted_is_explained`; `Tests/Duofy.Shared.Tests/LoginPageTests.cs`: `The_link_to_the_registration_keeps_the_return_address`; `Tests/Duofy.Shared.Tests/RegisterPageTests.cs`: `A_local_return_address_is_followed_after_the_automatic_sign_in`; `Tests/Duofy.Shared.Tests/ReturnAddressTests.cs`; `Tests/Duofy.Web.Tests/ShareServiceTests.cs`: `The_invitation_page_opens_without_sign_in` |
| R6 | A7, A8, A18, A21 | `Tests/Duofy.Backend.Tests/InvitationTests.cs`: `Accepting_moves_ben_into_annas_household_and_uses_the_invitation_up`, `A_founder_of_an_own_household_cannot_accept`, `A_member_elsewhere_cannot_accept`, `The_founder_accepting_an_own_invitation_leaves_it_open`; `Tests/Duofy.Shared.Tests/InvitationPageTests.cs`: `A_signed_in_person_sees_the_preview_and_joins`, `Someone_in_a_household_sees_beitreten_greyed_out_with_the_reason`, `A_refused_join_shows_why_and_stays`; `Tests/Duofy.Shared.Tests/HouseholdPageTests.cs`: `A_pasted_link_opens_the_invitation_page` |
| R7 | A4, A10, A11 | `Tests/Duofy.Backend.Tests/MembershipTests.cs`: `The_founder_sets_quotas_and_renames_and_ben_sees_it_but_may_not_rename`, `Invalid_changes_are_refused_and_change_nothing`, `A_name_longer_than_100_characters_is_refused`, `There_is_no_way_to_change_a_role`, `Ben_cannot_remove_anybody`; `Tests/Duofy.Backend.Tests/InvitationTests.cs`: `A_member_may_neither_create_nor_list_nor_revoke_invitations`; `Tests/Duofy.Shared.Tests/HouseholdPageTests.cs`: `Richtwerte_saves_quotas_and_name`, `Wrong_quotas_stay_in_the_dialog_with_the_reason` |
| R8 | A12, A13 | `Tests/Duofy.Backend.Tests/MembershipTests.cs`: `The_founder_cannot_leave_while_ben_is_in_and_dissolves_the_household_alone`, `Anna_removes_ben_who_keeps_his_data_but_cannot_remove_herself`, `Removing_somebody_who_is_not_a_member_is_not_found`; `Tests/Duofy.Shared.Tests/HouseholdPageTests.cs`: `Leaving_asks_once_and_then_offers_founding_again`, `The_founder_alone_dissolves_after_asking_once`, `A_refused_dissolving_shows_why`, `Removing_ben_asks_once` |
| R9 | A14 | `Tests/Duofy.Backend.Tests/HouseholdDeletionTests.cs`: `The_earliest_member_becomes_founder_when_the_founder_deletes_the_account`, `A_household_with_nobody_left_goes_with_the_account`, `A_member_deleting_the_account_just_leaves`, `Somebody_in_no_household_deletes_the_account_as_before`; `Tests/Duofy.Backend.Tests/Domain/HouseholdTests.cs`: `The_next_founder_is_the_earliest_joined_of_the_others` |
| R10 | A16, A20, A21, A23 | `Tests/Duofy.Shared.Tests/HouseholdPageTests.cs`: `Mehr_leads_to_the_household`, `Without_a_household_the_page_offers_founding_and_the_paste_field`, `The_founder_sees_name_members_roles_du_and_the_founder_buttons`, `A_member_sees_the_rows_read_only`, `The_founder_with_others_sees_austreten_greyed_out_with_the_reason`, `After_ben_left_the_founder_is_offered_to_dissolve`, `Leaving_asks_once_and_then_offers_founding_again`, `The_founder_alone_dissolves_after_asking_once`, `Removing_ben_asks_once`, `Open_invitations_are_listed_and_revoked_after_asking_once`, `With_ten_open_invitations_einladen_is_greyed_out_with_the_reason`, `Below_ten_open_invitations_einladen_is_offered`; `Tests/Duofy.Shared.Tests/InvitationPageTests.cs`: `Someone_in_a_household_sees_beitreten_greyed_out_with_the_reason`, `A_signed_in_person_sees_the_preview_and_joins` |

## Threats

- Can someone pretend to be someone else? Only with a stolen access token; the caller always comes from the token. An invitation link is a credential on purpose: whoever holds it may join once, so it is single-use, valid 14 days and revocable, and only the founder sees the open ones.
- Can someone change data? Only the founder changes a household; others get 404, members 403. Joining changes no data of anybody.
- Can someone deny an action? Memberships carry their joined time; an invitation keeps who created it, who used it and when.
- Can someone read what they should not? The preview shows only household name, inviter's name and expiry; members see names and emails of their own household only; tokens are listed to the founder only.
- Can someone bring it to a halt? At most 10 open invitations per household; tokens have 256 bits so guessing is hopeless; the rate limit of the API foundation applies to the preview without sign-in.
- Can someone gain rights they should not have? Joining gives the role member; there is no way to change a role; the founder role passes only on the founder's account deletion; no household path reveals or changes accounts, plans or bookings.

## Non-goals

- Grants, the member switch and any access to another person's data (#68).
- The household plan and the use of the household quotas in a plan (#69).
- Sending the invitation by mail (#53), Android app links that open the app from the link, a QR scanner inside the app.
- Instance invitations, the first user from the environment, the registration mode (admin feature).
- Several households per person, merging households, transferring data, handing the household over while the founder stays.
- The web page of the household (the human's part); the shared pages are reachable in the web host as they are.

## Risks

- Account deletion and leaving touch the same rows; the lock on the household row keeps a founder hand-over and a leaving member from crossing.
- A link passed on carelessly lets somebody else join; the founder sees the members and removes a stranger, and the link works only once.
- The migration creates no household for existing users; it is applied to the human's database only after the yes.

## Open questions

None.

## Approval

Approved by the human on 2026-10-24

Presented in the lead session on Saturday 2026-10-24. The human replaced the email-bound inbox of the original with single-use invitation links (QR code, share dialog, no mail server; mail follows with #53), approved QRCoder 1.8.0 (MIT) as a new dependency and approved the migration. The first user from the environment at first run is noted for the admin feature.

Amendment 2026-10-24 (after the first build; card P-014 became obsolete): the human decided that a person has no household until they found one and that only the founder manages it: no admin roles, no own household from registration on, a member who leaves or is removed is in no household. R1, R4 and R6 to R10 and their criteria were changed; criteria still ticked are unchanged by the amendment.

Amendment 2026-10-24 (second, by the human): links use the server's public address from the environment (`Duofy:PublicUrl`), not the address a client is connected to; a button a person may not use is either not shown or shown greyed out with a short reason, never offered and then refused.

Amendment 2026-10-25 (final review round 3, decided by the human): the invitation page greys out Beitreten for someone in a household (M2), revoking cannot overwrite an accepted invitation (m1), Einladen is greyed out at 10 open invitations (m8). Criteria already met are to be ticked.
