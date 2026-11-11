# Changelog

All notable changes to this project are documented in this file. The format
follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project
uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- README describes the current Duofy, and SECURITY.md asks for reports through GitHub private vulnerability reporting (issue #113).
- Each account shows its movements by date, with edit and delete, at `/konten/{id}` (spec 51).
- A position shows which bookings belong to it and offers a booking on this position, also in the household plan (spec 51).
- `/haushaltsplan`: the household plan as its own page, also for a household of one (spec 51).

### Changed

- The bottom bar has exactly four targets: Start, Plan, Haushalt, Mehr. Haushalt opens the household plan, and without a household the way to found or join one; Sparen and Verträge lie under Mehr (decision of 2026-11-01).
- The page Buchungen and its menu entry are gone: new bookings start in the month, `/buchungen` leads to the current month, and the start page and its plus open the current month with the booking dialog (decision of 2026-11-01).
- A ticked position stays selectable for further bookings, so a fee can still count on it (decision of 2026-11-01).
- Booking lists name each kind as a coloured word (Einnahme, Ausgabe, Umbuchung, Übertrag) with a sign, and "Letzte Buchungen" says what a booking was for (issue #51).

- The whole interface runs on Radzen Blazor 12.1.8 and Material Symbols; MudBlazor and the Tabler icons are gone, and a test keeps them out. Account and payment icons keep their stored names.
- UI packages from ASP.NET Core and Microsoft.Extensions are on 10.0.14, as Radzen needs it; the backend stays on 10.0.12 because no advisory applies to its packages.
- The app window is named after the brand of the chosen server.
- The web container keeps its data protection keys on its own volume `web-keys`, so forms and page state survive a rebuild; browser sessions still end when the web container restarts, because they live in memory (issue #38)
- From 1280 px the side bar shows the full menu directly instead of "Mehr" (accounts, contracts, household, user account, admin for admins, licences, legal texts, sign-out); phone and tablet keep the bottom bar unchanged (spec 10, amendment 2026-11-01)
- From 1280 px plan, month overview, bookings, accounts, contracts and household use up to 1600 px: blocks in columns and lists as tables with columns; below 1280 px nothing changes

### Removed

- **Breaking:** Push reminders and notifications (specs 84 and 118) are removed from Duofy: the reminder and device endpoints, the reminder job, the push clients, the `Duofy.Push` projects, the Android and Windows push parts, the page "Benachrichtigungen", the grant area "reminders", the household event log and the postpone counter, the Firebase package and the `Duofy__Reminders__*` settings. They live on in Plagevo. The migration `RemovePushReminders` drops their tables and the grant rows of the area "reminders"; a database that never used reminders only loses empty tables (issue #129).

### Fixed

- Amounts no longer depend on the language of the machine: in a container 12,34 became 1234,00 in the account and contract dialogs. The web host now sets de-DE itself.
- Enter in the password field of sign-in and delete account sends what was just typed, not the value from when the field was last left.
- Links and buttons show the focus outline, also in the app bar; MudBlazor had reset it.
- Secondary text, notices and status text are readable: error, success and warning colours are only surfaces now, and the meaning stays in the word, the sign or the border.
- Every page has one main heading, the app bar title is no heading, and a skip link leads past the navigation to the content.
- Select fields are connected to their label; required and invalid step fields say so on the input itself.
- Row buttons name their row for screen readers, and the error bar is announced and closes with a real button.
- The app can be zoomed, and the input border in dark mode reaches a 3:1 contrast.
- On a narrow phone row buttons such as "Auflösen" and "Beenden" stay on the screen, table rows wrap instead of overflowing, amounts do not break before the currency, "Grundbedarf" stays in one piece, all five bottom bar labels fit and the plus button on bookings stays above the bottom bar.
- "Abbrechen" no longer breaks in the middle of the word in the dialog head.
- The consent banner, which can ask about two services, covers neither the navigation nor the profile button and scrolls by itself when it does not fit.
- After a new carry-over the balance matches the entered one on the last day of the month too: both bookings of the pair stand on that day, the income counts in the next month through its plan month only. Stored pairs keep their dates, so on the last day of their old month their balance stays as stored (spec 66, amendment 2026-11-01)
- A position with a target account can no longer be switched to Einnahmen, because it could not be ticked afterwards; one stored that way stays editable and explains the repair when ticked
- A plan year outside 2000 to 2100 is refused; a huge year got through the plan month check by an overflow
- The log line of a carry-over no longer contains the amount
- `docs/betrieb.md` uses placeholders instead of the real Tailscale address and root paths
- "Neue Buchung" on the plan page starts in the month on the screen
- Without access to someone's bookings, the month overview says so instead of showing an empty list, a false gap and false open amounts
- The arrow keys on the month tabs count from the active tab and move the focus along
- The household overview names members who did not grant their bookings instead of saying there are none
- The web image ships `_framework/blazor.web.js` again, so its pages become interactive: the Docker restore with only the project files skipped the package that carries the script (spec 79)
- `docker compose` no longer demands `TUNNEL_TOKEN` when the `own-tunnel` profile is not used; it defaults to empty (spec 79, P-032)

### Changed

- The free amount of a month contains the carry-over from the previous month: planned income minus planned expenses plus the carry-over, in the month overview and the list of months, for a person and for the household, with a text at the number as soon as one is included. Fixed, quotas, pots, booked sums and the suggestion for the next carry-over stay without it. Whoever does not see a person's bookings gets that person's free amount without the carry-over, also as a share of the household sum, so the household sum depends on the viewer. `GET /api/v1/plans` and `GET /api/v1/households/{id}/plans` carry the included amount as `carryOver`. No migration (spec 66, amendment 2026-11-01)
- A carry-over is no ordinary booking any more: the booking page, its sums and the booked sums of the plan show only real money, and the month shows one line "Übertrag nach <Monat>" in the old month and "Übertrag aus <Monat>" in the new one, in no pot and no quota, also on the tab Buchungen and under "Letzte Buchungen"; the household shows these lines only for members who granted their bookings. The balancing booking stays a normal booking. Stored pairs are treated the same way through the ids in the plan, without a migration (spec 66, amendment 2026-11-01)
- If an entered real balance differs from the computed one, the carry-over dialog shows the difference per account and offers "Buchungen nachtragen" (closes without a change) or "Als Ausgleich übernehmen" (books); without a difference it stays as it was (spec 66, amendment 2026-11-01)
- A balancing booking that lowers a balance counts in Wünsche instead of Grundbedarf; stored ones stay as they are (spec 72, amendment 2026-11-01)
- The first account created at registration opens the day before, so a new person can book on the first day; other accounts keep the rule of spec 59, R6
- A new transfer carries a required source pot ("Von Topf") and a required target pot ("Zu Topf") from Grundbedarf, Wünsche and Sparen, in the dialog, the API and the booking list; they are only remembered and change no figure of the plan. The target pot is a new nullable column `bookings.target_pot` (additive migration `AddTransferTargetPot`); older transfers stay readable and the dialog asks for the missing pots when one is changed. The rule that a transfer may only be in Sparen is gone; the box "Das Geld wird gespart" is replaced by the two fields. Ticking a position with a target account books a transfer with the pot of the position on both sides (spec 72, R9)
- Bookings without a position count as booked in their pot, so the gap chart shows unplanned spending, while the actual of the quotas counts the positions only (spec 72, R5); the booking dialog starts an expense without a pot and marks the field as required (spec 72)
- The app's default server is the hosted service Plagevo at `https://api.plagevo.de/`; the server line reads "Plagevo (gehostet)" and the dialog offers "Plagevo" or an own server (spec 61, amendment 2026-10-27)
- A hosted server no longer starts with an http public address or with mail TLS switched off (spec 79)
- Repository restructured without any change of behaviour: `Duofy.Api`, `Duofy.Data` and `Duofy.Domain` are one project `Backend/Duofy.Backend` (folders `Api`, `Data`, `Domain`), the data formats are called `Duofy.Schemas`, projects lie in the folders `Backend`, `Shared`, `Web`, `Mobile` and `Tests`, and the tests of the API and the domain are one project `Duofy.Backend.Tests` (spec 77)

### Added

- Embedding in an own site such as Home Assistant: `Duofy__FrameAncestors` lists the sites that may show the web version in an iframe (`https://host[:port]`, separated by spaces). With a list, `X-Frame-Options` is gone, `frame-ancestors` names `'self'` and exactly these sites on every response, and the cookies become `SameSite=None; Secure`. An invalid value stops the start; without the setting nothing changes
- Plan overview (spec 23): `/plan` lists the months with a plan, the current month and the three coming ones, newest first, with the current month highlighted and the free amount per month (or "noch nicht angelegt"); a tap opens the month at `/plan/{year}/{month}`, which links back. New read-only endpoint `GET /api/v1/plans` (months with plan and free amount) and the free amount in `GET /api/v1/households/{id}/plans`
- `TablerIcon` component with eight Tabler outline icons (`@tabler/icons` 3.50.0, MIT) shipped as embedded SVG files with their licence text; the icon inherits the text colour and is either labelled or hidden from screen readers (spec 7)

- `deploy/check-web.sh`: after every start or update it checks that the web version answers `/health` and serves `/_framework/blazor.web.js`, and fails otherwise (spec 79)
- Plagevo runs in its own Docker network `plagevo` (172.30.10.0/24) with fixed addresses (tunnel .2, api .11, web .12, backup .13); the tunnel points at addresses, the API trusts only the tunnel (`172.30.10.2/32`) and exempts only the web container; the cloudflared of a separate compose project joins the network as an external one (spec 79, amendment 2026-10-26)
- External database: `ConnectionStrings__Duofy` is required in `deploy/.env` (Plagevo uses a PostgreSQL server reached over Tailscale), the backup dumps that database and `backup.sh check` tests the connection; `deploy/own-db.yml` adds an own PostgreSQL in the internal network `plagevo-backend` for self-hosters
- `deploy/.env` accepts every setting in .NET's notation (`Duofy__Mail__Host`) as well as the old names (`DUOFY_MAIL_HOST`); the .NET notation wins
- `Duofy:Mail:FromName`: the sender name of mails, default `Plagevo`
- Operations of Plagevo behind a Cloudflare Tunnel (spec 79): container images for the API and the web version (multi-stage, non-root, health check, version and build date labels), `deploy/docker-compose.yml` with PostgreSQL, a daily backup kept 14 days and memory limits for a small server, joining the network of an existing `cloudflared` container, and the guide `docs/betrieb.md`
- `Duofy:TrustedProxies`: behind trusted proxies the API and the web version take the client address from `CF-Connecting-IP` and the scheme from `X-Forwarded-Proto`, so the sign-in limit counts per client
- `RateLimiting:ExemptAddresses`: connection addresses the sign-in limit does not apply to, for the web container
- `Duofy:KeysDirectory`: the keys of tokens and mailed links survive a restart or an update
- `Duofy:Analytics:Token`: Cloudflare Web Analytics on every page of the web version, without cookies
- `Crisp:WebsiteId`: chat by Crisp on the web version, loaded only after consent given for the chat on its own; empty means no chat
- `/health` on the web version for the container health check
- Blazor Hybrid solution with `Duofy.Shared`, `Duofy.Web` and the MAUI app
- `Duofy.Domain` for the business rules and `Duofy.Domain.Tests` for their tests
- Commitment with its due month, due day and last due date
- Month plan created from the commitments that fall due, as independent copies
- Account with a balance that is computed from its start amount and bookings
