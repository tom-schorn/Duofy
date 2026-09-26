# Changelog

## [0.3.0](https://github.com/tom-schorn/Duofy/compare/v0.2.0...v0.3.0) (2026-09-26)


### ⚠ BREAKING CHANGES

* group the categories under a parent ([#49](https://github.com/tom-schorn/Duofy/issues/49))

### Features

* add a shared dialog frame with focus, discard guard and one footer ([8057fe0](https://github.com/tom-schorn/Duofy/commit/8057fe007dc024387ab2572345341e1ec8436048))
* add a translation unit so every visible text comes from a catalog ([#105](https://github.com/tom-schorn/Duofy/issues/105)) ([ac6d7f2](https://github.com/tom-schorn/Duofy/commit/ac6d7f25ea564af1c457d566a6cf30aa0d47284d))
* add backend logging with a configurable level and no private data ([#122](https://github.com/tom-schorn/Duofy/issues/122)) ([2c420d7](https://github.com/tom-schorn/Duofy/commit/2c420d763210dee25ef4d4d7ea3d0a773ff0208c))
* add the amount parser and the AmountField component ([a898122](https://github.com/tom-schorn/Duofy/commit/a8981229e5f8ba139aa4d38ca25bebc333b68042))
* add the shared list row and row menu components ([e68db8f](https://github.com/tom-schorn/Duofy/commit/e68db8f156600721f839ff475a9aa09367593973))
* call the book remainder Bleibt übrig and the income base Verplanbar everywhere ([a7e007d](https://github.com/tom-schorn/Duofy/commit/a7e007da673082c9e30b59214f434807173ca2de))
* click a booking row in the book to edit it ([2b0ed81](https://github.com/tom-schorn/Duofy/commit/2b0ed8196b033022468232dde39651261da38020))
* complete the permission ladder with a delete level ([#69](https://github.com/tom-schorn/Duofy/issues/69)) ([1fdbfd4](https://github.com/tom-schorn/Duofy/commit/1fdbfd4ecdacfb79ef0f55dc507d66c4d50ef86e))
* delete an unused commitment or account, refuse a used one ([396c3a8](https://github.com/tom-schorn/Duofy/commit/396c3a83eaf0cbf6ea4783dedb12645b78fa641c))
* delete positions and bookings with undo and show one message at a time, bottom center ([1a69726](https://github.com/tom-schorn/Duofy/commit/1a69726340cf7b04c70348c7d36539a0ff23d787))
* derive plan hints on read, starting with overdue positions ([cfebf7a](https://github.com/tom-schorn/Duofy/commit/cfebf7a1604c30ddbd870b707fa48729442b0dc2))
* end a commitment on a date instead of switching it off ([#123](https://github.com/tom-schorn/Duofy/issues/123)) ([581b6e1](https://github.com/tom-schorn/Duofy/commit/581b6e16efb2d9d289070cd7309ad02c2687093f))
* explain each page in a column on the right ([#59](https://github.com/tom-schorn/Duofy/issues/59)) ([b6d160b](https://github.com/tom-schorn/Duofy/commit/b6d160b93c31ab7d20a6fc8fd584943ef67a68a5))
* give the book its own page and sharpen what the import suggests ([#80](https://github.com/tom-schorn/Duofy/issues/80)) ([a2e08fc](https://github.com/tom-schorn/Duofy/commit/a2e08fc9ac9d04f9ffa0136c5ff0c3fecbcc6c93))
* group the categories under a parent ([#49](https://github.com/tom-schorn/Duofy/issues/49)) ([165cf81](https://github.com/tom-schorn/Duofy/commit/165cf81e15c7ea797458a70b4b0f903d07180e6d))
* import bank statements into the parking area ([#70](https://github.com/tom-schorn/Duofy/issues/70)) ([5424f5a](https://github.com/tom-schorn/Duofy/commit/5424f5a555c941c7eb87a1a9c118c76b26932db8))
* let the amount field take a typed minus and zero for an opening balance ([2166e4a](https://github.com/tom-schorn/Duofy/commit/2166e4a3c9cb2cdb7301022b7105beeaac52a295))
* link and show the operator's legal pages when configured ([#64](https://github.com/tom-schorn/Duofy/issues/64)) ([947c70e](https://github.com/tom-schorn/Duofy/commit/947c70e6345c37a97ada5a41d48bcb45d914872a))
* make income its own commitment type ([#50](https://github.com/tom-schorn/Duofy/issues/50)) ([3d62299](https://github.com/tom-schorn/Duofy/commit/3d622995246dbc5e6c23f6f2ee383e1e9a821cda))
* name the position and amount in the message after ticking off, fix the untick title ([063c9af](https://github.com/tom-schorn/Duofy/commit/063c9afcc3aaa5a9c010ccf4bab83550e30881f2))
* offer delete on own unused commitments and accounts ([efe653f](https://github.com/tom-schorn/Duofy/commit/efe653f192f80b429c98fcf29230f539a258b636))
* offer the couple preset after joining or creating a household ([#91](https://github.com/tom-schorn/Duofy/issues/91)) ([8d96058](https://github.com/tom-schorn/Duofy/commit/8d96058872629e1701fa2b001806261d1adcfbca))
* open the help from a fixed button as a side sheet, optionally pinned beside the page ([5d40b79](https://github.com/tom-schorn/Duofy/commit/5d40b79275f2e0a75119cdf466afd8d89b4970de))
* read bank statements into a parking area ([#68](https://github.com/tom-schorn/Duofy/issues/68)) ([4e92ac9](https://github.com/tom-schorn/Duofy/commit/4e92ac95e6e2ce1d7cd1db97ea003ab75750b625))
* read CSV statements, and keep lists from reshuffling ([#81](https://github.com/tom-schorn/Duofy/issues/81)) ([f77f41e](https://github.com/tom-schorn/Duofy/commit/f77f41e7ec53249b5507662426f4ce95e3d55756))
* recognise transfers between own accounts on import ([#82](https://github.com/tom-schorn/Duofy/issues/82)) ([402d891](https://github.com/tom-schorn/Duofy/commit/402d8914f735dcd71d4fcd701c76431a3203f3d5))
* say whose grant is missing where an edit action is not offered ([#91](https://github.com/tom-schorn/Duofy/issues/91)) ([309e72a](https://github.com/tom-schorn/Duofy/commit/309e72ac574a48ce1598700d4158df85f5a23971))
* serve the operator's imprint, privacy policy and terms from configured files ([#64](https://github.com/tom-schorn/Duofy/issues/64)) ([ee80c53](https://github.com/tom-schorn/Duofy/commit/ee80c53085c8fb949ba69fd3b58ee13044f38081))
* show plan hints at their position, translated through the catalog ([b2265a7](https://github.com/tom-schorn/Duofy/commit/b2265a7055014adaefd1e1c3e8e1ea87dcdd26de))
* slim the tick dialog and say when the date is outside the plan month ([ad2d3db](https://github.com/tom-schorn/Duofy/commit/ad2d3db2500ef19e344ab5f9e1bff1e501ad589c))
* stop planning a savings goal once its target is reached ([#87](https://github.com/tom-schorn/Duofy/issues/87)) ([0f89b3e](https://github.com/tom-schorn/Duofy/commit/0f89b3e69a6cdf331fe4442841778acf16f009a9))
* suggest categories on import, and stop losing what a booking means ([#77](https://github.com/tom-schorn/Duofy/issues/77)) ([2e7b3e1](https://github.com/tom-schorn/Duofy/commit/2e7b3e17d71e439ed4b1ef50f21cd5e54c4b4208))
* switch to another member, with permissions per area ([#55](https://github.com/tom-schorn/Duofy/issues/55)) ([c4e90df](https://github.com/tom-schorn/Duofy/commit/c4e90df6823ef76c996a850afb4be26c43b86726))
* use Grundbedarf, Frei and Anstehend as the one word for each number ([88b3095](https://github.com/tom-schorn/Duofy/commit/88b30952feca2bcfb46b01c45005fa70802cdab6))
* use the amount field for the commitment amount, target amount and opening balance ([fc9a821](https://github.com/tom-schorn/Duofy/commit/fc9a821985ce7efa9b43a49232c996dd5bd2a071))
* use the amount field in the paid dialog, booking edit, quick entry and position dialog ([38eed5c](https://github.com/tom-schorn/Duofy/commit/38eed5c7004a72d36ed6df34dbecdb348d59b4b0))


### Bug Fixes

* allow a planned amount of 0,00 for a commitment and its target ([b88ff2f](https://github.com/tom-schorn/Duofy/commit/b88ff2f457bdb94cd8b8ccdb25ed9b6c2a00ad2f))
* allow a zero planned amount, accept ',50' and spaces, focus the first invalid amount ([c233af9](https://github.com/tom-schorn/Duofy/commit/c233af9c2d5bc4d11293a3d770665ba39c0f0648))
* answer 409 for a used commitment or account, lock the commitment before deleting ([592cb89](https://github.com/tom-schorn/Duofy/commit/592cb8956d7e73049e04098b860eddbc9c3a3e84))
* catch failed changes without an error path of their own ([c384a92](https://github.com/tom-schorn/Duofy/commit/c384a922683e4637b7a4190601adb67a637d2607))
* detach a leaver's positions from the household in every month ([4f0d674](https://github.com/tom-schorn/Duofy/commit/4f0d674ca5ad03bc983bff878eade7a2ae1ad45b))
* detach the leaver's commitments from the household too, so a new month does not bring the positions back ([e470928](https://github.com/tom-schorn/Duofy/commit/e470928ca70ccbb61c6ca65d65f36209b6419d58))
* drop the delete hints, confirm an account delete in its own dialog ([33ab47f](https://github.com/tom-schorn/Duofy/commit/33ab47fee7617fd8a4dd358a65419e47ea6792d0))
* drop the unused positionUpdated text and keep umlauts out of the tests ([f0580be](https://github.com/tom-schorn/Duofy/commit/f0580bea4531127054f89ea539a130ef66b83f88))
* give the interval select a name and ignore a click on the chosen type ([2c03d4d](https://github.com/tom-schorn/Duofy/commit/2c03d4df941ce56e0873f53fffde9448a3416387))
* hide the household rename and account settings entries that do nothing ([7374a6a](https://github.com/tom-schorn/Duofy/commit/7374a6aacdf51f8720dac4ffb873aa1e057635f4))
* keep a visible focus outline in forced colors and let screen readers hear the row amount ([bb9e121](https://github.com/tom-schorn/Duofy/commit/bb9e12156efb3b09b0bb991b7577373ebabcf072))
* keep one header help button so the focus survives pinning, and cover the narrow and refused-storage paths ([d128ef2](https://github.com/tom-schorn/Duofy/commit/d128ef2158bb0b330e1a69c948f8484490bc5119))
* keep test files out of the app typecheck so the image builds ([459c845](https://github.com/tom-schorn/Duofy/commit/459c8451a35cd0bb8f4a646f50d4d0d19a741adc))
* keep the dialog locked on Esc while saving and skip disabled fields for focus ([d4f523f](https://github.com/tom-schorn/Duofy/commit/d4f523ff194c1e64f37879b75dadff6d73ecd194))
* keep the position and commitment dialogs open until the server answers ([250c252](https://github.com/tom-schorn/Duofy/commit/250c25212354cf30e40337db7ec2520e36591927))
* keep the undo message until it is closed, restore rows from a snapshot and flush on pagehide and visibilitychange ([2743666](https://github.com/tom-schorn/Duofy/commit/2743666b4116d791a1b7c05a0e98dc8e94c1ee63))
* let the month overview follow the member switcher ([#57](https://github.com/tom-schorn/Duofy/issues/57)) ([2a293c3](https://github.com/tom-schorn/Duofy/commit/2a293c3cebecc5c1af8fcaa4ada1af5e85dbb93c))
* let the standing error toast be dismissed by keyboard ([7138ce1](https://github.com/tom-schorn/Duofy/commit/7138ce1e5151ea6cf71e48f9b6fe4751c4c452c1))
* load only the used account ids, not every booking of the listed accounts ([5cd838b](https://github.com/tom-schorn/Duofy/commit/5cd838b0c72e69b1eba96c97e9dfe30cacb9f598))
* lock the save dialogs while the server has not answered ([7eef54a](https://github.com/tom-schorn/Duofy/commit/7eef54a776f45ad76ada2c5fa21a62de42c1caee))
* make each sidebar entry one link with one tab stop ([dd988f9](https://github.com/tom-schorn/Duofy/commit/dd988f90fdf6112284c4b8f16ded09f31f1e9f7c))
* mark useSetMyAccess as showing its error inline ([56edb4e](https://github.com/tom-schorn/Duofy/commit/56edb4e8fe9887e210004d5e87e7394c3352304f))
* mount a read-only config folder for the legal texts and cap their size ([#64](https://github.com/tom-schorn/Duofy/issues/64)) ([ba0c0e5](https://github.com/tom-schorn/Duofy/commit/ba0c0e567283501a238d732aa8332dd5d89d1881))
* move the booking edit onto DialogFrame, send only changed fields, give it its own save state ([b3859a5](https://github.com/tom-schorn/Duofy/commit/b3859a5d2d9e5d6370b25d7388963cb4f1035dd3))
* name the Aktiv switch of the account dialog for screen readers ([3d7ba66](https://github.com/tom-schorn/Duofy/commit/3d7ba6682c8a1239e1fc657ae440a42200063f49))
* name the month arrows in the book ([a287c8f](https://github.com/tom-schorn/Duofy/commit/a287c8fab8e99eca5367b4f7079dcd4f537cb49e))
* offer to create a valid month without a plan, show not-found for an invalid month ([381c2df](https://github.com/tom-schorn/Duofy/commit/381c2df79ef00251412d5e374df5b42dd81ecc42))
* reject a limit outside contracts and explicit nulls on required fields ([#114](https://github.com/tom-schorn/Duofy/issues/114)) ([4ed4964](https://github.com/tom-schorn/Duofy/commit/4ed4964abcfbf7a41f77a27a2b9ccac63bb71865))
* reject a tick that cannot be booked and say when date and amount are unused ([#120](https://github.com/tom-schorn/Duofy/issues/120)) ([fb32e4d](https://github.com/tom-schorn/Duofy/commit/fb32e4d0b2168cdb545369930a76c75112f324bd))
* reset the account dialog mutations on open and show only the latest error ([6547766](https://github.com/tom-schorn/Duofy/commit/6547766482838f69f839b6bd19a429f40f0cdb50))
* reset the plan page when the month in the address changes, use a body-only not-found inside the app ([2986f9e](https://github.com/tom-schorn/Duofy/commit/2986f9ebd2009c39039b34fc037a150d400ab43c))
* run the area permission tests in the shared event loop ([#104](https://github.com/tom-schorn/Duofy/issues/104)) ([395c176](https://github.com/tom-schorn/Duofy/commit/395c1764753cb11de7b0ce4be4a83d9d144d42e7))
* say honestly that nothing was booked when ticking a position with bookings, and name the position on untick ([1e45528](https://github.com/tom-schorn/Duofy/commit/1e45528283c32300b4930c1eee79cc64d165a820))
* say in the leave confirmation and its help that positions leave every month and do not return ([d8edfb3](https://github.com/tom-schorn/Duofy/commit/d8edfb32bf4d19f0b4c96174f288d9c61b7e1750))
* send the tick messages through the single message slot ([9ec4429](https://github.com/tom-schorn/Duofy/commit/9ec4429cf4204d6e1bb132c3890638b2a68e9264))
* show a failed tick once when the dialog already shows it ([a77de2d](https://github.com/tom-schorn/Duofy/commit/a77de2dcfeac4d881aed811694d930c9ea3f5ea1))
* show form errors as a bordered box ([4918f9e](https://github.com/tom-schorn/Duofy/commit/4918f9e8f7c4e4ae54f41550841b28f3735ca225))
* show the legal footer on the not-found page and below the sign-in form, tell errors from 404 ([#64](https://github.com/tom-schorn/Duofy/issues/64)) ([aa5bc3e](https://github.com/tom-schorn/Duofy/commit/aa5bc3e45a67eb6eacb2bc599aa8d2ef976947ea))
* show the page title in the header instead of a placeholder ([027e494](https://github.com/tom-schorn/Duofy/commit/027e49473ebab0cf5906cab780ea6bcf01ce301b))
* show the paid state of a read-only row as a symbol instead of a greyed box ([b9d915a](https://github.com/tom-schorn/Duofy/commit/b9d915a4ead7f4c984f769e50acbab67118ea778))
* start household, invite and create-month dialogs fresh on every open ([357325f](https://github.com/tom-schorn/Duofy/commit/357325f64c1a57ae0232dd63f236a472a58011c7))
* stop the dev compose file from shadowing the self-hosting one ([#60](https://github.com/tom-schorn/Duofy/issues/60)) ([e6d73ee](https://github.com/tom-schorn/Duofy/commit/e6d73eec1a02422168f86791a5dbf5bda42f0993))
* tell the truth in the leave confirmation, name the button after the household and keep the last owner from leaving ([dba75e1](https://github.com/tom-schorn/Duofy/commit/dba75e150a421987c02b9dddb0f3bbc36327a192))
* tint and ring the whole list row on keyboard focus only ([cd55ca2](https://github.com/tom-schorn/Duofy/commit/cd55ca247ec8a83c39a88e999b9c51935a970738))
* use the highest grant across shared households in the frontend ([#91](https://github.com/tom-schorn/Duofy/issues/91)) ([8d661c3](https://github.com/tom-schorn/Duofy/commit/8d661c38165ebf10ae0360f414e633a80989edbb))
* validate a booking change against its final shape and keep a ticked booking on its position ([9b00161](https://github.com/tom-schorn/Duofy/commit/9b001618b3bb64467774fd4e91eb908899eab342))


### Refactoring

* build the account dialog on the shared frame ([71d552d](https://github.com/tom-schorn/Duofy/commit/71d552dfb8853a4c2da14e8f7e79f3930383dc60))
* build the commitment dialog on the shared frame ([44b2e43](https://github.com/tom-schorn/Duofy/commit/44b2e43cd84838514299614c84eea8b5f20d2df5))
* build the create-month dialog on the shared frame and name its selects ([803cbf8](https://github.com/tom-schorn/Duofy/commit/803cbf89c59cae1ce7b205cf7be35b67fa698134))
* build the household and invite dialogs on the shared frame ([39f9b32](https://github.com/tom-schorn/Duofy/commit/39f9b32199e6811a4fd8ca6cbdd53d411d8385e0))
* build the position dialog on the shared frame ([054236c](https://github.com/tom-schorn/Duofy/commit/054236c8b3b78fe6ef8fe234dbb87eeedeb938ca))
* build the tick dialog on the shared frame ([11d15b7](https://github.com/tom-schorn/Duofy/commit/11d15b70d7f27acf0408476bcee1adfe7848b857))
* count a recurrence in months instead of four fixed rhythms ([#115](https://github.com/tom-schorn/Duofy/issues/115)) ([26252e2](https://github.com/tom-schorn/Duofy/commit/26252e239f8aa60a792117f26b6cbaf9edcb6d22))
* delete a plan position from its edit dialog and focus the section heading afterwards ([d7c9780](https://github.com/tom-schorn/Duofy/commit/d7c97808397496c6298fbfd9b2cb65df23d1938e))
* drop the unmaintained remaining debt of a commitment ([#124](https://github.com/tom-schorn/Duofy/issues/124)) ([de7401a](https://github.com/tom-schorn/Duofy/commit/de7401a6301b9c0c60bf58371a7332068743a21c))
* keep a commitment's due day only in its first due date ([#121](https://github.com/tom-schorn/Duofy/issues/121)) ([8d3a964](https://github.com/tom-schorn/Duofy/commit/8d3a964811647d96184e53d1bb7f107ca7002b40))
* make leaving a household a visible button with a confirmation instead of a menu entry ([ffbdb1f](https://github.com/tom-schorn/Duofy/commit/ffbdb1f3f395fa59e1b4c4ecc0cfec180c23bc5a))
* move the create-month dialog into its own component and let it preselect a month ([263033c](https://github.com/tom-schorn/Duofy/commit/263033c0e3d29acc394304b39e97f32554d29d1c))
* put the doc comments back on their functions and word the grant hint as one sentence ([#91](https://github.com/tom-schorn/Duofy/issues/91)) ([4dd0bb5](https://github.com/tom-schorn/Duofy/commit/4dd0bb561e5ebae0d87e6a7aabddd3003734b732))
* remove the row menu component and its catalog text ([526c6a6](https://github.com/tom-schorn/Duofy/commit/526c6a6d940740a650dcc184228318ed2de1d53f))
* removed Terraform configuration ([#48](https://github.com/tom-schorn/Duofy/issues/48)) ([9fa7932](https://github.com/tom-schorn/Duofy/commit/9fa793228d8a95f9a3a6af2421f8919c6bf880ad))
* rename Block to Budget and replace the budget type with a limit flag ([#112](https://github.com/tom-schorn/Duofy/issues/112)) ([c97e5f1](https://github.com/tom-schorn/Duofy/commit/c97e5f1e1fc88f497ee16a5f9f757439de6b6ef6))
* show accounts as list rows and focus the page heading after a delete ([e0c1bfb](https://github.com/tom-schorn/Duofy/commit/e0c1bfb47e9a6ae7166ec42edd7e013b32d25c19))
* show bookings as list rows and delete from the edit dialog ([e9894a3](https://github.com/tom-schorn/Duofy/commit/e9894a363bf4c004865bb8f8c5c2c9d0ff3fb3b5))
* show commitments as list rows and delete from the edit dialog ([cc642e6](https://github.com/tom-schorn/Duofy/commit/cc642e6294ba770de18b7061657220863c686c0a))
* show plan positions as list rows and move delete into the row menu ([b05c633](https://github.com/tom-schorn/Duofy/commit/b05c63328eea7a166f49f571bcb4194c4b1baeff))

## [0.2.0](https://github.com/tom-schorn/Duofy/compare/v0.1.0...v0.2.0) (2026-08-14)


### ⚠ BREAKING CHANGES

**Sessions now last a month instead of an hour** ([#38](https://github.com/tom-schorn/Duofy/issues/38)).
The access token is still a JWT but only lives fifteen minutes; beside it sits a
refresh token in an `HttpOnly` cookie that is renewed on every use. What you have
to do when updating:

* The migration runs on start and creates the `refresh_tokens` table. Nothing to do
  by hand.
* Two new values in your `.env`, both with defaults that suit most people:
  `REFRESH_LIFETIME_SECONDS` (30 days) and `COOKIE_SECURE` (`true`). **Set
  `COOKIE_SECURE=false` if you reach Duofy over plain http**, for instance on your
  home network only — browsers throw away secure cookies on http addresses, and
  without the cookie nobody stays signed in.
* Everyone signs in once more. Sessions from before this release do not carry over.

**The plan status and the confirmation are gone**
([#40](https://github.com/tom-schorn/Duofy/issues/40),
[#41](https://github.com/tom-schorn/Duofy/issues/41)).
A month is never "finished": planning happens on the last Saturday and things still
get added during the week after. The state also only existed in your own plan — the
shared household plan is composed on the fly and never had an object to confirm.

* `POST /plans/{plan_id}/confirm` no longer exists.
* Plan responses no longer carry `status` or `confirmedAt`.
* The migration drops `plans.status` and `plans.confirmed_at`. **Which months were
  confirmed cannot be recovered afterwards.** Nothing in Duofy depended on it, so
  nothing else has to change — but a backup before updating is the usual advice and
  applies here.

### Features

* keep sessions alive with refresh tokens ([#38](https://github.com/tom-schorn/Duofy/issues/38)) ([f9181c7](https://github.com/tom-schorn/Duofy/commit/f9181c777217d7ce2151365f47e9b2955ac7a938))


### Bug Fixes

* remove plan status and confirmation ([#40](https://github.com/tom-schorn/Duofy/issues/40)) ([e95bf3b](https://github.com/tom-schorn/Duofy/commit/e95bf3b6f2fecf17d61762356e5d992b223c9847))
* remove the last plan status references from the frontend types ([#41](https://github.com/tom-schorn/Duofy/issues/41)) ([424f458](https://github.com/tom-schorn/Duofy/commit/424f45866ef827b9d47b7db31ad61944691ffdf9))
