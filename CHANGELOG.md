# Changelog

All notable changes to this project are documented in this file. The format
follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project
uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Fixed

- "Neue Buchung" on the plan page starts in the month on the screen
- Without access to someone's bookings, the month overview says so instead of showing an empty list, a false gap and false open amounts
- The arrow keys on the month tabs count from the active tab and move the focus along
- The household overview names members who did not grant their bookings instead of saying there are none
- The web image ships `_framework/blazor.web.js` again, so its pages become interactive: the Docker restore with only the project files skipped the package that carries the script (spec 79)
- `docker compose` no longer demands `TUNNEL_TOKEN` when the `own-tunnel` profile is not used; it defaults to empty (spec 79, P-032)

### Changed

- A new transfer carries a required source pot ("Von Topf") and a required target pot ("Zu Topf") from Grundbedarf, Wünsche and Sparen, in the dialog, the API and the booking list; they are only remembered and change no figure of the plan. The target pot is a new nullable column `bookings.target_pot` (additive migration `AddTransferTargetPot`); older transfers stay readable and the dialog asks for the missing pots when one is changed. The rule that a transfer may only be in Sparen is gone; the box "Das Geld wird gespart" is replaced by the two fields. Ticking a position with a target account books a transfer with the pot of the position on both sides (spec 72, R9)
- Bookings without a position count as booked in their pot, so the gap chart and the actual of the quotas show unplanned spending; the booking dialog starts an expense without a pot and marks the field as required (spec 72)
- The app's default server is the hosted service Plagevo at `https://api.plagevo.de/`; the server line reads "Plagevo (gehostet)" and the dialog offers "Plagevo" or an own server (spec 61, amendment 2026-10-27)
- A hosted server no longer starts with an http public address or with mail TLS switched off (spec 79)
- Repository restructured without any change of behaviour: `Duofy.Api`, `Duofy.Data` and `Duofy.Domain` are one project `Backend/Duofy.Backend` (folders `Api`, `Data`, `Domain`), the data formats are called `Duofy.Schemas`, projects lie in the folders `Backend`, `Shared`, `Web`, `Mobile` and `Tests`, and the tests of the API and the domain are one project `Duofy.Backend.Tests` (spec 77)

### Added

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
- `/health` on the web version for the container health check
- Blazor Hybrid solution with `Duofy.Shared`, `Duofy.Web` and the MAUI app
- `Duofy.Domain` for the business rules and `Duofy.Domain.Tests` for their tests
- Commitment with its due month, due day and last due date
- Month plan created from the commitments that fall due, as independent copies
- Account with a balance that is computed from its start amount and bookings
